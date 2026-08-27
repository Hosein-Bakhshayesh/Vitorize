using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;

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
                    MaskedCardPan = x.MaskedCardPan,
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

        public async Task<PagedResult<AdminPaymentDto>> GetPagedAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 100);
            var query = ApplyFilter(_dbContext.Payments.AsNoTracking(), filter);
            var totalCount = await query.CountAsync(cancellationToken);

            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("amount", "asc") => query.OrderBy(x => x.Amount).ThenBy(x => x.Id),
                ("amount", "desc") => query.OrderByDescending(x => x.Amount).ThenBy(x => x.Id),
                ("status", "asc") => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
                ("status", "desc") => query.OrderByDescending(x => x.Status).ThenBy(x => x.Id),
                ("requestedat", "asc") => query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            };

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(MapListItem()).ToListAsync(cancellationToken);
            return new PagedResult<AdminPaymentDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        private static IQueryable<Domain.Entities.Payment> ApplyFilter(IQueryable<Domain.Entities.Payment> query, AdminQueryFilterDto filter)
        {
            if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
            if (filter.DateFrom.HasValue) query = query.Where(x => x.RequestedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue) query = query.Where(x => x.RequestedAt < filter.DateTo.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                if (s.Length > 250) s = s[..250];
                query = query.Where(x => x.Gateway.Contains(s) ||
                    (x.TransactionId != null && x.TransactionId.Contains(s)) ||
                    (x.ReferenceNumber != null && x.ReferenceNumber.Contains(s)) ||
                    x.Order.OrderNumber.Contains(s) || x.User.FullName.Contains(s) || x.User.Mobile.Contains(s));
            }
            return query;
        }

        private static Expression<Func<Domain.Entities.Payment, AdminPaymentDto>> MapListItem() => x => new AdminPaymentDto
        {
            Id = x.Id, OrderId = x.OrderId, OrderNumber = x.Order.OrderNumber, UserId = x.UserId,
            UserFullName = x.User.FullName, UserMobile = x.User.Mobile, Amount = x.Amount, Gateway = x.Gateway,
            Authority = x.Authority, GatewayTrackingCode = x.GatewayTrackingCode, TransactionId = x.TransactionId,
            ReferenceNumber = x.ReferenceNumber, MaskedCardPan = x.MaskedCardPan,
            Status = x.Status, ProviderStatusCode = x.ProviderStatusCode,
            CallbackVerified = x.CallbackVerified, RequestedAt = x.RequestedAt, VerifiedAt = x.VerifiedAt,
            UpdatedAt = x.UpdatedAt, ErrorMessage = x.ErrorMessage
        };

        public async Task<AdminPaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
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
                    MaskedCardPan = x.MaskedCardPan,
                    Status = x.Status,
                    ProviderStatusCode = x.ProviderStatusCode,
                    CallbackVerified = x.CallbackVerified,
                    RequestedAt = x.RequestedAt,
                    VerifiedAt = x.VerifiedAt,
                    UpdatedAt = x.UpdatedAt,
                    ErrorMessage = x.ErrorMessage
                })
                .FirstOrDefaultAsync(cancellationToken);

            return item ?? throw new KeyNotFoundException("پرداخت پیدا نشد.");
        }

        public async Task<PagedResult<Vitorize.Application.DTOs.Payments.PaymentRefundDto>> GetRefundsPagedAsync(
            Guid paymentId, PaymentDetailHistoryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PaymentDetailHistoryFilterDto();
            if (!await _dbContext.Payments.AsNoTracking().AnyAsync(x => x.Id == paymentId, cancellationToken))
                throw new KeyNotFoundException("پرداخت پیدا نشد.");
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.PaymentRefunds.AsNoTracking().Where(x => x.PaymentId == paymentId);
            var totalCount = await query.CountAsync(cancellationToken);
            query = string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id)
                : query.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new Vitorize.Application.DTOs.Payments.PaymentRefundDto
                {
                    Id = x.Id, PaymentId = x.PaymentId, OrderId = x.OrderId, Amount = x.Amount,
                    Method = x.Method, Status = x.Status, Reason = x.Reason,
                    RequestedAt = x.RequestedAt, CompletedAt = x.CompletedAt
                }).ToListAsync(cancellationToken);
            return new PagedResult<Vitorize.Application.DTOs.Payments.PaymentRefundDto>
                { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<PagedResult<FinancialAuditEntryDto>> GetAuditHistoryPagedAsync(
            Guid paymentId, PaymentDetailHistoryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new PaymentDetailHistoryFilterDto();
            var orderId = await _dbContext.Payments.AsNoTracking().Where(x => x.Id == paymentId)
                .Select(x => (Guid?)x.OrderId).FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("پرداخت پیدا نشد.");
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.FinancialAuditLogs.AsNoTracking().Where(x =>
                x.CorrelationId == orderId || x.EntityId == paymentId ||
                _dbContext.PaymentRefunds.Any(refund => refund.PaymentId == paymentId && refund.Id == x.EntityId));
            var totalCount = await query.CountAsync(cancellationToken);
            query = string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                : query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new FinancialAuditEntryDto
            {
                EventType = x.EventType, EntityId = x.EntityId, Amount = x.Amount,
                Detail = x.Detail, CreatedAt = x.CreatedAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<FinancialAuditEntryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }
    }
}
