using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class AdminPaymentReadService : IAdminPaymentReadService
    {
        private readonly VitorizeDbContext _dbContext;
        public AdminPaymentReadService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task<List<AdminPaymentDto>> GetAllAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.Payments.AsNoTracking().AsQueryable();

            if (filter.Status.HasValue)
                query = query.Where(x => x.Status == filter.Status.Value);

            if (filter.DateFrom.HasValue)
                query = query.Where(x => x.RequestedAt >= filter.DateFrom.Value);

            if (filter.DateTo.HasValue)
                query = query.Where(x => x.RequestedAt < filter.DateTo.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x =>
                    x.Gateway.Contains(s) ||
                    (x.TransactionId != null && x.TransactionId.Contains(s)) ||
                    (x.ReferenceNumber != null && x.ReferenceNumber.Contains(s)) ||
                    x.Order.OrderNumber.Contains(s) ||
                    x.User.FullName.Contains(s) ||
                    x.User.Mobile.Contains(s));
            }

            return await query
                .OrderByDescending(x => x.RequestedAt)
                .Take(filter.PageSize <= 0 ? 100 : Math.Min(filter.PageSize, 300))
                .Select(x => new AdminPaymentDto
                {
                    Id = x.Id,
                    OrderId = x.OrderId,
                    OrderNumber = x.Order.OrderNumber,
                    UserId = x.UserId,
                    UserFullName = x.User.FullName,
                    UserMobile = x.User.Mobile,
                    Amount = x.Amount,
                    Gateway = x.Gateway,
                    Authority = x.Authority,
                    GatewayTrackingCode = x.GatewayTrackingCode,
                    TransactionId = x.TransactionId,
                    ReferenceNumber = x.ReferenceNumber,
                    Status = x.Status,
                    ProviderStatusCode = x.ProviderStatusCode,
                    CallbackVerified = x.CallbackVerified,
                    RequestedAt = x.RequestedAt,
                    VerifiedAt = x.VerifiedAt,
                    UpdatedAt = x.UpdatedAt,
                    ErrorMessage = x.ErrorMessage
                })
                .ToListAsync();
        }

        public async Task<AdminPaymentDto> GetByIdAsync(Guid id)
        {
            var item = await _dbContext.Payments
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AdminPaymentDto
                {
                    Id = x.Id,
                    OrderId = x.OrderId,
                    OrderNumber = x.Order.OrderNumber,
                    UserId = x.UserId,
                    UserFullName = x.User.FullName,
                    UserMobile = x.User.Mobile,
                    Amount = x.Amount,
                    Gateway = x.Gateway,
                    Authority = x.Authority,
                    GatewayTrackingCode = x.GatewayTrackingCode,
                    TransactionId = x.TransactionId,
                    ReferenceNumber = x.ReferenceNumber,
                    Status = x.Status,
                    ProviderStatusCode = x.ProviderStatusCode,
                    CallbackVerified = x.CallbackVerified,
                    RequestedAt = x.RequestedAt,
                    VerifiedAt = x.VerifiedAt,
                    UpdatedAt = x.UpdatedAt,
                    ErrorMessage = x.ErrorMessage
                })
                .FirstOrDefaultAsync();

            return item ?? throw new KeyNotFoundException("پرداخت پیدا نشد.");
        }
    }
}
