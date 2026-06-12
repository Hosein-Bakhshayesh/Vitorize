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
                    throw new BusinessException(
                        "این Idempotency-Key قبلاً برای درخواست دیگری استفاده شده است.");

                if (existing.Status == (byte)IdempotencyStatus.Processing)
                    throw new BusinessException(
                        "درخواست قبلاً در حال پردازش است.");

                if (existing.Status == (byte)IdempotencyStatus.Completed)
                    throw new BusinessException(
                        "این درخواست قبلاً با موفقیت انجام شده است.");

                if (existing.Status == (byte)IdempotencyStatus.Failed)
                {
                    _dbContext.IdempotencyKeys.Remove(existing);
                    await _dbContext.SaveChangesAsync();
                    existing = null;
                }
            }

            await _dbContext.IdempotencyKeys.AddAsync(new IdempotencyKey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Key = key,
                RequestHash = requestHash,
                CreatedAt = DateTime.UtcNow,
                Status = (byte)IdempotencyStatus.Processing,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task CompleteAsync(
            string key,
            string? responseJson = null,
            int? statusCode = null)
        {
            var record = await _dbContext.IdempotencyKeys
                .FirstOrDefaultAsync(x => x.Key == key);

            if (record == null)
                return;

            record.Status = (byte)IdempotencyStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            record.ResponseJson = responseJson;
            record.StatusCode = statusCode;

            await _dbContext.SaveChangesAsync();
        }

        public async Task FailAsync(
            string key,
            string? errorMessage = null)
        {
            var record = await _dbContext.IdempotencyKeys
                .FirstOrDefaultAsync(x => x.Key == key);

            if (record == null)
                return;

            record.Status = (byte)IdempotencyStatus.Failed;
            record.FailedAt = DateTime.UtcNow;
            record.ErrorMessage = errorMessage;

            await _dbContext.SaveChangesAsync();
        }
    }
}