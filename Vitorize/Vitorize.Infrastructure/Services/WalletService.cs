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

        public WalletService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
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

                wallet.UpdatedAt = DateTime.UtcNow;

                await _dbContext.WalletTransactions.AddAsync(new WalletTransaction
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
                    CreatedAt = DateTime.UtcNow
                });

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