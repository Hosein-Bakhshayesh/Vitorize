using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

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
