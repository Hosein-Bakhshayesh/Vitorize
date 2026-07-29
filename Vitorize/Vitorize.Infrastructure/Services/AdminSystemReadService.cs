using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;

namespace Vitorize.Infrastructure.Services
{
    public class AdminSystemReadService : IAdminSystemReadService
    {
        private readonly VitorizeDbContext _dbContext;
        public AdminSystemReadService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task<List<AdminErrorLogDto>> GetErrorLogsAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.ErrorLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x => x.Message.Contains(s) || (x.Source != null && x.Source.Contains(s)));
            }
            query = ApplyDate(query, filter, x => x.CreatedAt);
            return await query.OrderByDescending(x => x.CreatedAt).Take(PageSize(filter)).Select(x => new AdminErrorLogDto
            {
                Id = x.Id, Message = x.Message, StackTrace = x.StackTrace, Source = x.Source, CreatedAt = x.CreatedAt
            }).ToListAsync();
        }

        public async Task<AdminErrorLogDto> GetErrorLogByIdAsync(Guid id)
        {
            var item = await _dbContext.ErrorLogs.AsNoTracking().Where(x => x.Id == id).Select(x => new AdminErrorLogDto
            {
                Id = x.Id, Message = x.Message, StackTrace = x.StackTrace, Source = x.Source, CreatedAt = x.CreatedAt
            }).FirstOrDefaultAsync();
            return item ?? throw new KeyNotFoundException("خطا پیدا نشد.");
        }

        public async Task<PagedResult<AdminErrorLogDto>> GetPagedErrorLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Page(filter); var pageSize = BoundedPageSize(filter);
            var query = _dbContext.ErrorLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = TrimSearch(filter.Search);
                query = query.Where(x => x.Message.Contains(search) || (x.Source != null && x.Source.Contains(search)));
            }
            query = ApplyDate(query, filter, x => x.CreatedAt);
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("source", "asc") => query.OrderBy(x => x.Source).ThenBy(x => x.Id),
                ("source", "desc") => query.OrderByDescending(x => x.Source).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminErrorLogDto
            {
                Id = x.Id, Message = x.Message, StackTrace = x.StackTrace, Source = x.Source, CreatedAt = x.CreatedAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<AdminErrorLogDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<List<AdminAuditLogDto>> GetAuditLogsAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x => x.ActionType.Contains(s) || x.EntityName.Contains(s) || (x.EntityId != null && x.EntityId.Contains(s)) || (x.IpAddress != null && x.IpAddress.Contains(s)));
            }
            query = ApplyDate(query, filter, x => x.CreatedAt);
            return await query.OrderByDescending(x => x.CreatedAt).Take(PageSize(filter)).Select(x => new AdminAuditLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null, ActionType = x.ActionType,
                EntityName = x.EntityName, EntityId = x.EntityId, Data = x.Data, IpAddress = x.IpAddress,
                UserAgent = x.UserAgent, CreatedAt = x.CreatedAt
            }).ToListAsync();
        }

        public async Task<AdminAuditLogDto> GetAuditLogByIdAsync(Guid id)
        {
            var item = await _dbContext.AuditLogs.AsNoTracking().Where(x => x.Id == id).Select(x => new AdminAuditLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null, ActionType = x.ActionType,
                EntityName = x.EntityName, EntityId = x.EntityId, Data = x.Data, IpAddress = x.IpAddress,
                UserAgent = x.UserAgent, CreatedAt = x.CreatedAt
            }).FirstOrDefaultAsync();
            return item ?? throw new KeyNotFoundException("فعالیت پیدا نشد.");
        }

        public async Task<PagedResult<AdminAuditLogDto>> GetPagedAuditLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Page(filter); var pageSize = BoundedPageSize(filter);
            var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = TrimSearch(filter.Search);
                query = query.Where(x => x.ActionType.Contains(search) || x.EntityName.Contains(search) ||
                    (x.EntityId != null && x.EntityId.Contains(search)) || (x.IpAddress != null && x.IpAddress.Contains(search)));
            }
            query = ApplyDate(query, filter, x => x.CreatedAt);
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("action", "asc") => query.OrderBy(x => x.ActionType).ThenBy(x => x.Id),
                ("action", "desc") => query.OrderByDescending(x => x.ActionType).ThenBy(x => x.Id),
                ("entity", "asc") => query.OrderBy(x => x.EntityName).ThenBy(x => x.Id),
                ("entity", "desc") => query.OrderByDescending(x => x.EntityName).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminAuditLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null,
                ActionType = x.ActionType, EntityName = x.EntityName, EntityId = x.EntityId, Data = x.Data,
                IpAddress = x.IpAddress, UserAgent = x.UserAgent, CreatedAt = x.CreatedAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<AdminAuditLogDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<List<AdminSecurityLogDto>> GetSecurityLogsAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.SecurityLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x => x.EventType.Contains(s) || (x.Description != null && x.Description.Contains(s)) || (x.IpAddress != null && x.IpAddress.Contains(s)));
            }
            if (filter.IsSuccessful.HasValue) query = query.Where(x => x.IsSuccessful == filter.IsSuccessful.Value);
            query = ApplyDate(query, filter, x => x.CreatedAt);
            return await query.OrderByDescending(x => x.CreatedAt).Take(PageSize(filter)).Select(x => new AdminSecurityLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null, EventType = x.EventType,
                Description = x.Description, IpAddress = x.IpAddress, UserAgent = x.UserAgent, IsSuccessful = x.IsSuccessful, CreatedAt = x.CreatedAt
            }).ToListAsync();
        }

        public async Task<AdminSecurityLogDto> GetSecurityLogByIdAsync(Guid id)
        {
            var item = await _dbContext.SecurityLogs.AsNoTracking().Where(x => x.Id == id).Select(x => new AdminSecurityLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null, EventType = x.EventType,
                Description = x.Description, IpAddress = x.IpAddress, UserAgent = x.UserAgent, IsSuccessful = x.IsSuccessful, CreatedAt = x.CreatedAt
            }).FirstOrDefaultAsync();
            return item ?? throw new KeyNotFoundException("رویداد امنیتی پیدا نشد.");
        }

        public async Task<PagedResult<AdminSecurityLogDto>> GetPagedSecurityLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Page(filter); var pageSize = BoundedPageSize(filter);
            var query = _dbContext.SecurityLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = TrimSearch(filter.Search);
                query = query.Where(x => x.EventType.Contains(search) || (x.Description != null && x.Description.Contains(search)) ||
                    (x.IpAddress != null && x.IpAddress.Contains(search)));
            }
            if (filter.IsSuccessful.HasValue) query = query.Where(x => x.IsSuccessful == filter.IsSuccessful.Value);
            query = ApplyDate(query, filter, x => x.CreatedAt);
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("event", "asc") => query.OrderBy(x => x.EventType).ThenBy(x => x.Id),
                ("event", "desc") => query.OrderByDescending(x => x.EventType).ThenBy(x => x.Id),
                ("successful", "asc") => query.OrderBy(x => x.IsSuccessful).ThenBy(x => x.Id),
                ("successful", "desc") => query.OrderByDescending(x => x.IsSuccessful).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminSecurityLogDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User != null ? x.User.FullName : null,
                EventType = x.EventType, Description = x.Description, IpAddress = x.IpAddress,
                UserAgent = x.UserAgent, IsSuccessful = x.IsSuccessful, CreatedAt = x.CreatedAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<AdminSecurityLogDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        private static int PageSize(AdminQueryFilterDto filter) => filter.PageSize <= 0 ? 100 : Math.Min(filter.PageSize, 300);
        private static int Page(AdminQueryFilterDto filter) => Math.Max(1, filter.PageNumber ?? filter.Page);
        private static int BoundedPageSize(AdminQueryFilterDto filter) => filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 100);
        private static string TrimSearch(string search) => search.Trim() is var value && value.Length > 250 ? value[..250] : value;
        private static IQueryable<T> ApplyDate<T>(IQueryable<T> query, AdminQueryFilterDto filter, System.Linq.Expressions.Expression<Func<T, DateTime>> selector)
        {
            if (!filter.DateFrom.HasValue && !filter.DateTo.HasValue) return query;
            var p = selector.Parameters[0];
            System.Linq.Expressions.Expression? body = null;
            if (filter.DateFrom.HasValue)
                body = System.Linq.Expressions.Expression.GreaterThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
            {
                var upper = System.Linq.Expressions.Expression.LessThan(selector.Body, System.Linq.Expressions.Expression.Constant(filter.DateTo.Value.AddDays(1)));
                body = body == null ? upper : System.Linq.Expressions.Expression.AndAlso(body, upper);
            }
            return body == null ? query : query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, p));
        }
    }
}
