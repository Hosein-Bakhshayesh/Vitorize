using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Data;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class WalletTopUpService : IWalletTopUpService
    {
        private const string ZarinpalGatewayName = "Zarinpal";
        private const string MockGatewayName = "Mock";

        private readonly VitorizeDbContext _dbContext;
        private readonly IWalletService _walletService;
        private readonly INotificationService _notificationService;
        private readonly IZarinpalGatewayService _zarinpalGatewayService;
        private readonly ISmsOutboxEnqueuer _smsOutbox;

        public WalletTopUpService(
            VitorizeDbContext dbContext,
            IWalletService walletService,
            INotificationService notificationService,
            IZarinpalGatewayService zarinpalGatewayService,
            ISmsOutboxEnqueuer smsOutbox)
        {
            _dbContext = dbContext;
            _walletService = walletService;
            _notificationService = notificationService;
            _zarinpalGatewayService = zarinpalGatewayService;
            _smsOutbox = smsOutbox;
        }

        public async Task<WalletTopUpStartResultDto> StartAsync(
            Guid userId,
            WalletTopUpRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (request.Amount <= 0)
                throw new BusinessException("مبلغ شارژ معتبر نیست.");

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var now = DateTime.UtcNow;

            var topUp = new WalletTopUp
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
                Gateway = MockGatewayName,
                Status = (byte)PaymentStatus.Pending,
                RequestedAt = now
            };

            await _dbContext.WalletTopUps.AddAsync(topUp);
            await _dbContext.SaveChangesAsync();

            var description = "شارژ کیف پول در Vitorize";

            (bool Success, string Authority, string PaymentUrl) gatewayResult;

            try
            {
                gatewayResult = await _zarinpalGatewayService.CreatePaymentAsync(
                    topUp.Amount,
                    description,
                    user.Mobile,
                    user.Email,
                    $"TOPUP-{topUp.Id:N}");
            }
            catch
            {
                // درگاه پیکربندی نشده یا در دسترس نیست — مسیر Mock فعال می‌ماند
                gatewayResult = (false, string.Empty, string.Empty);
            }

            if (gatewayResult.Success)
            {
                topUp.Gateway = ZarinpalGatewayName;
                topUp.Authority = gatewayResult.Authority;
                topUp.RawResponseData = JsonSerializer.Serialize(gatewayResult);
                topUp.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return new WalletTopUpStartResultDto
                {
                    TopUpId = topUp.Id,
                    Amount = topUp.Amount,
                    Gateway = topUp.Gateway,
                    Authority = topUp.Authority,
                    PaymentUrl = gatewayResult.PaymentUrl
                };
            }

            // درگاه در دسترس نیست — تاپ‌آپ به صورت Mock معلق می‌ماند (مسیر توسعه)
            return new WalletTopUpStartResultDto
            {
                TopUpId = topUp.Id,
                Amount = topUp.Amount,
                Gateway = topUp.Gateway,
                Authority = null,
                PaymentUrl = null
            };
        }

        public async Task<WalletTopUpVerifyResultDto> VerifyMockAsync(
            Guid userId,
            Guid topUpId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"wallet-topup:{topUpId:N}");
                var topUp = await _dbContext.WalletTopUps
                    .FirstOrDefaultAsync(x =>
                        x.Id == topUpId &&
                        x.UserId == userId);

                if (topUp == null)
                    throw new NotFoundException("درخواست شارژ یافت نشد.");

                if (topUp.Status == (byte)PaymentStatus.Paid)
                {
                    var currentWallet = await _walletService.GetMyWalletAsync(userId);

                    await transaction.CommitAsync();

                    return CreateResult(topUp, true, currentWallet.Balance);
                }

                if (topUp.Status != (byte)PaymentStatus.Pending)
                    throw new BusinessException("وضعیت درخواست شارژ قابل تایید نیست.");

                var now = DateTime.UtcNow;

                topUp.Status = (byte)PaymentStatus.Paid;
                topUp.ReferenceNumber = $"MOCK-TOPUP-{now:yyyyMMddHHmmss}";
                topUp.VerifiedAt = now;
                topUp.UpdatedAt = now;

                var wallet = await CreditWalletAsync(topUp);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreateResult(topUp, true, wallet.Balance);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> IsTopUpAuthorityAsync(string authority)
        {
            if (string.IsNullOrWhiteSpace(authority))
                return false;

            return await _dbContext.WalletTopUps
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Authority == authority &&
                    x.Gateway == ZarinpalGatewayName);
        }

        public async Task<WalletTopUpVerifyResultDto> VerifyZarinpalAsync(
            string authority,
            string status)
        {
            if (string.IsNullOrWhiteSpace(authority))
                throw new BusinessException("Authority معتبر نیست.");

            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? "NOK"
                : status.Trim();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                await SqlServerTransactionLock.AcquireAsync(
                    _dbContext, $"wallet-topup-callback:{authority.Trim().ToUpperInvariant()}");
                var topUp = await _dbContext.WalletTopUps
                    .FirstOrDefaultAsync(x =>
                        x.Authority == authority &&
                        x.Gateway == ZarinpalGatewayName);

                if (topUp == null)
                    throw new NotFoundException("درخواست شارژ یافت نشد.");

                if (topUp.Status == (byte)PaymentStatus.Paid)
                {
                    var currentWallet = await _walletService.GetMyWalletAsync(topUp.UserId);

                    await transaction.CommitAsync();

                    return CreateResult(topUp, true, currentWallet.Balance);
                }

                if (!string.Equals(normalizedStatus, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    topUp.Status = (byte)PaymentStatus.Cancelled;
                    topUp.ErrorMessage = "پرداخت توسط کاربر لغو شد یا ناموفق بود.";
                    topUp.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return CreateResult(topUp, false, 0);
                }

                if (topUp.Status != (byte)PaymentStatus.Pending)
                    throw new BusinessException("وضعیت درخواست شارژ قابل تایید نیست.");

                var verifyResult = await _zarinpalGatewayService.VerifyPaymentAsync(
                    authority,
                    topUp.Amount);

                topUp.RawResponseData = JsonSerializer.Serialize(new
                {
                    Type = "ZarinpalTopUpVerify",
                    Authority = authority,
                    topUp.Amount,
                    Result = verifyResult,
                    VerifiedAt = DateTime.UtcNow
                });

                if (!verifyResult.Success)
                {
                    topUp.Status = (byte)PaymentStatus.Failed;
                    topUp.ErrorMessage = "تایید پرداخت زرین‌پال ناموفق بود.";
                    topUp.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return CreateResult(topUp, false, 0);
                }

                var now = DateTime.UtcNow;

                topUp.Status = (byte)PaymentStatus.Paid;
                topUp.ReferenceNumber = verifyResult.RefId.ToString();
                topUp.VerifiedAt = now;
                topUp.UpdatedAt = now;

                var wallet = await CreditWalletAsync(topUp);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreateResult(topUp, true, wallet.Balance);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<WalletDto> CreditWalletAsync(WalletTopUp topUp)
        {
            var wallet = await _walletService.CreditAsync(
                topUp.UserId,
                topUp.Amount,
                (byte)WalletReferenceType.WalletTopUp,
                topUp.Id,
                "شارژ کیف پول از درگاه پرداخت");

            await _notificationService.CreateAsync(
                topUp.UserId,
                (byte)NotificationType.PaymentSucceeded,
                "شارژ کیف پول",
                "کیف پول شما با موفقیت شارژ شد.");

            var mobile = await _dbContext.Users
                .Where(x => x.Id == topUp.UserId)
                .Select(x => x.Mobile)
                .FirstOrDefaultAsync();

            await _smsOutbox.EnqueueTemplateAsync(
                mobile,
                Vitorize.Application.Common.SmsTemplateKeys.WalletTopUpSuccess,
                Vitorize.Application.Models.Sms.SmsBusinessNotificationParameters.WalletTopUp(
                    Vitorize.Application.Common.SmsPublicReference.ForWalletTopUp(
                        topUp.Id,
                        topUp.ReferenceNumber)),
                purpose: "WalletTopUpSuccess",
                aggregateId: topUp.Id,
                userId: topUp.UserId,
                relatedEntityType: "WalletTopUp",
                relatedEntityReference: Vitorize.Application.Common.SmsPublicReference.ForWalletTopUp(
                    topUp.Id,
                    topUp.ReferenceNumber));

            return wallet;
        }

        private static WalletTopUpVerifyResultDto CreateResult(
            WalletTopUp topUp,
            bool isPaid,
            decimal balance)
        {
            return new WalletTopUpVerifyResultDto
            {
                TopUpId = topUp.Id,
                IsPaid = isPaid,
                ReferenceNumber = topUp.ReferenceNumber,
                Amount = topUp.Amount,
                Balance = balance
            };
        }
    }
}
