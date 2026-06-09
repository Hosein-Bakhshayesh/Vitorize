using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class IdempotencyService : IIdempotencyService
    {
        private readonly VitorizeDbContext _dbContext;

        public IdempotencyService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task StartAsync(
            Guid? userId,
            string key,
            string requestHash)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var existing = await _dbContext.IdempotencyKeys
                .FirstOrDefaultAsync(x => x.Key == key);

            if (existing != null)
            {
                if (existing.RequestHash != requestHash)
                    throw new BusinessException("این Idempotency-Key قبلاً برای درخواست دیگری استفاده شده است.");

                throw new BusinessException("این درخواست قبلاً ثبت شده است.");
            }

            await _dbContext.IdempotencyKeys.AddAsync(new IdempotencyKey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Key = key,
                RequestHash = requestHash,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        public Task CompleteAsync(string key)
        {
            return Task.CompletedTask;
        }

        public Task FailAsync(string key)
        {
            return Task.CompletedTask;
        }
    }
}