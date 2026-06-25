using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.DTOs.Admin.Wallets;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class AdminWalletReadService : IAdminWalletReadService
    {
        private readonly VitorizeDbContext _dbContext;
        public AdminWalletReadService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task<List<AdminWalletListDto>> GetAllAsync(AdminQueryFilterDto filter)
        {
            var query = _dbContext.Wallets.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(x => x.User.FullName.Contains(s) || x.User.Mobile.Contains(s) || (x.User.Email != null && x.User.Email.Contains(s)));
            }

            return await query
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Take(filter.PageSize <= 0 ? 100 : Math.Min(filter.PageSize, 300))
                .Select(x => new AdminWalletListDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserFullName = x.User.FullName,
                    UserMobile = x.User.Mobile,
                    Balance = x.Balance,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }
    }
}
