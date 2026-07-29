using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;

namespace Vitorize.Infrastructure.Services
{
    public class AdminNotificationReadService : IAdminNotificationReadService
    {
        private readonly VitorizeDbContext _dbContext;
        public AdminNotificationReadService(VitorizeDbContext dbContext) => _dbContext = dbContext;
        public async Task<List<AdminNotificationDto>> GetAllAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.Notifications.AsNoTracking().AsQueryable();
            if (filter.IsRead.HasValue) query = query.Where(x => x.IsRead == filter.IsRead.Value);
            if (filter.DateFrom.HasValue) query = query.Where(x => x.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue) query = query.Where(x => x.CreatedAt < filter.DateTo.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x => x.Title.Contains(s) || x.Message.Contains(s) || x.User.FullName.Contains(s) || x.User.Mobile.Contains(s));
            }
            return await query.OrderByDescending(x => x.CreatedAt).Take(filter.PageSize <= 0 ? 100 : Math.Min(filter.PageSize, 300)).Select(x => new AdminNotificationDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User.FullName, UserMobile = x.User.Mobile, Title = x.Title, Message = x.Message,
                Type = x.Type, IsRead = x.IsRead, CreatedAt = x.CreatedAt, ReadAt = x.ReadAt
            }).ToListAsync();
        }
        public async Task<AdminNotificationDto> GetByIdAsync(Guid id)
        {
            var item = await _dbContext.Notifications.AsNoTracking().Where(x => x.Id == id).Select(x => new AdminNotificationDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User.FullName, UserMobile = x.User.Mobile, Title = x.Title, Message = x.Message,
                Type = x.Type, IsRead = x.IsRead, CreatedAt = x.CreatedAt, ReadAt = x.ReadAt
            }).FirstOrDefaultAsync();
            return item ?? throw new KeyNotFoundException("اطلاعیه پیدا نشد.");
        }
        public async Task<PagedResult<AdminNotificationDto>> GetPagedAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.Notifications.AsNoTracking().AsQueryable();
            if (filter.IsRead.HasValue) query = query.Where(x => x.IsRead == filter.IsRead.Value);
            if (filter.DateFrom.HasValue) query = query.Where(x => x.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue) query = query.Where(x => x.CreatedAt < filter.DateTo.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim(); if (search.Length > 250) search = search[..250];
                query = query.Where(x => x.Title.Contains(search) || x.Message.Contains(search) || x.User.FullName.Contains(search) || x.User.Mobile.Contains(search));
            }
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("title", "asc") => query.OrderBy(x => x.Title).ThenBy(x => x.Id),
                ("title", "desc") => query.OrderByDescending(x => x.Title).ThenBy(x => x.Id),
                ("read", "asc") => query.OrderBy(x => x.IsRead).ThenBy(x => x.Id),
                ("read", "desc") => query.OrderByDescending(x => x.IsRead).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminNotificationDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User.FullName, UserMobile = x.User.Mobile, Title = x.Title,
                Message = x.Message, Type = x.Type, IsRead = x.IsRead, CreatedAt = x.CreatedAt, ReadAt = x.ReadAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<AdminNotificationDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }
        public async Task MarkAsReadAsync(Guid id)
        {
            var item = await _dbContext.Notifications.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) throw new KeyNotFoundException("اطلاعیه پیدا نشد.");
            if (!item.IsRead)
            {
                item.IsRead = true;
                item.ReadAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
