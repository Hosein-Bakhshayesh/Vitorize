using Microsoft.EntityFrameworkCore;
using System.Data;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class WalletService : IWalletService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public WalletService(
            VitorizeDbContext dbContext,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        public async Task<WalletDto> GetMyWalletAsync(Guid userId)
        {
            return await GetUserWalletAsync(userId);
        }

        public async Task<List<WalletTransactionDto>> GetMyTransactionsAsync(Guid userId)
        {
            return await GetUserTransactionsAsync(userId);
        }

        public async Task<WalletDto> GetUserWalletAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var wallet = await GetOrCreateWalletAsync(userId);

            return MapWallet(wallet);
        }

        public async Task<List<WalletTransactionDto>> GetUserTransactionsAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var wallet = await GetOrCreateWalletAsync(userId);

            return await _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(x => x.WalletId == wallet.Id && x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WalletTransactionDto
                {
                    Id = x.Id,
                    WalletId = x.WalletId,
                    UserId = x.UserId,
                    Type = x.Type,
                    Amount = x.Amount,
                    BalanceAfter = x.BalanceAfter,
                    ReferenceType = x.ReferenceType,
                    ReferenceId = x.ReferenceId,
                    Description = x.Description,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<WalletDto> AdminChargeAsync(WalletChargeRequestDto request)
        {
            if (request.UserId == Guid.Empty)
                throw new BusinessException("شناسه کاربر معتبر نیست.");

            return await CreditAsync(
                request.UserId,
                request.Amount,
                (byte)WalletReferenceType.ManualAdminCharge,
                null,
                request.Description ?? "شارژ دستی کیف پول توسط ادمین");
        }

        public async Task<WalletDto> AdminWithdrawAsync(WalletWithdrawRequestDto request)
        {
            if (request.UserId == Guid.Empty)
                throw new BusinessException("شناسه کاربر معتبر نیست.");

            return await DebitAsync(
                request.UserId,
                request.Amount,
                (byte)WalletReferenceType.ManualAdminWithdraw,
                null,
                request.Description ?? "برداشت دستی از کیف پول توسط ادمین");
        }

        public async Task<WalletDto> CreditAsync(
            Guid userId,
            decimal amount,
            byte? referenceType,
            Guid? referenceId,
            string? description)
        {
            return await ChangeBalanceAsync(
                userId,
                amount,
                (byte)WalletTransactionType.Credit,
                referenceType,
                referenceId,
                description);
        }

        public async Task<WalletDto> DebitAsync(
            Guid userId,
            decimal amount,
            byte? referenceType,
            Guid? referenceId,
            string? description)
        {
            return await ChangeBalanceAsync(
                userId,
                amount,
                (byte)WalletTransactionType.Debit,
                referenceType,
                referenceId,
                description);
        }

        private async Task<WalletDto> ChangeBalanceAsync(
            Guid userId,
            decimal amount,
            byte transactionType,
            byte? referenceType,
            Guid? referenceId,
            string? description)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (amount <= 0)
                throw new BusinessException("مبلغ تراکنش باید بیشتر از صفر باشد.");

            var hasCurrentTransaction =
                _dbContext.Database.CurrentTransaction != null;

            await using var transaction = hasCurrentTransaction
                ? null
                : await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                await SqlServerTransactionLock.AcquireAsync(
                    _dbContext,
                    $"wallet:user:{userId:N}");

                var userExists = await _dbContext.Users
                    .AnyAsync(x => x.Id == userId);

                if (!userExists)
                    throw new NotFoundException("کاربر یافت نشد.");

                var wallet = await _dbContext.Wallets
                    .FirstOrDefaultAsync(x => x.UserId == userId);

                if (wallet == null)
                {
                    wallet = new Wallet
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Balance = 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _dbContext.Wallets.AddAsync(wallet);
                    await _dbContext.SaveChangesAsync();
                }

                // Financial references are idempotency keys. A gateway callback or refund
                // replay must return the already-applied balance instead of crediting twice.
                if (referenceType.HasValue && referenceId.HasValue &&
                    await _dbContext.WalletTransactions.AnyAsync(x =>
                        x.UserId == userId && x.Type == transactionType &&
                        x.ReferenceType == referenceType && x.ReferenceId == referenceId))
                {
                    if (transaction != null)
                        await transaction.CommitAsync();
                    return MapWallet(wallet);
                }

                if (transactionType == (byte)WalletTransactionType.Credit)
                {
                    wallet.Balance += amount;
                }
                else if (transactionType == (byte)WalletTransactionType.Debit)
                {
                    if (wallet.Balance < amount)
                        throw new BusinessException("موجودی کیف پول کافی نیست.");

                    wallet.Balance -= amount;
                }
                else
                {
                    throw new BusinessException("نوع تراکنش کیف پول معتبر نیست.");
                }

                var now = DateTime.UtcNow;
                wallet.UpdatedAt = now;

                var walletTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    UserId = userId,
                    Type = transactionType,
                    Amount = amount,
                    BalanceAfter = wallet.Balance,
                    ReferenceType = referenceType,
                    ReferenceId = referenceId,
                    Description = description,
                    CreatedAt = now
                };

                await _dbContext.WalletTransactions.AddAsync(walletTransaction);
                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = transactionType == (byte)WalletTransactionType.Credit ? "WalletCredited" : "WalletDebited",
                    EntityType = "WalletTransaction",
                    EntityId = walletTransaction.Id,
                    UserId = userId,
                    Amount = amount,
                    CorrelationId = referenceId ?? walletTransaction.Id,
                    Detail = referenceType.HasValue ? $"reference-type:{referenceType}" : "manual",
                    CreatedAt = now
                });

                if (transactionType == (byte)WalletTransactionType.Credit)
                {
                    await _notificationService.CreateAsync(
                        userId,
                        (byte)NotificationType.WalletCharged,
                        "شارژ کیف پول",
                        $"کیف پول شما به مبلغ {amount:N0} شارژ شد.");
                }
                else if (transactionType == (byte)WalletTransactionType.Debit)
                {
                    await _notificationService.CreateAsync(
                        userId,
                        (byte)NotificationType.WalletDebited,
                        "برداشت از کیف پول",
                        $"مبلغ {amount:N0} از کیف پول شما کسر شد.");
                }

                await _dbContext.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                return MapWallet(wallet);
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();

                throw;
            }
        }

        private async Task<Wallet> GetOrCreateWalletAsync(Guid userId)
        {
            var userExists = await _dbContext.Users
                .AnyAsync(x => x.Id == userId);

            if (!userExists)
                throw new NotFoundException("کاربر یافت نشد.");

            var wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (wallet != null)
                return wallet;

            wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Balance = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Wallets.AddAsync(wallet);
            await _dbContext.SaveChangesAsync();

            return wallet;
        }

        private static WalletDto MapWallet(Wallet wallet)
        {
            return new WalletDto
            {
                WalletId = wallet.Id,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                CreatedAt = wallet.CreatedAt,
                UpdatedAt = wallet.UpdatedAt
            };
        }
    }
}
