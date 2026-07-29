using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.DTOs.Admin.Wallets;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;

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

        public async Task<PagedResult<AdminWalletListDto>> GetPagedAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new AdminQueryFilterDto();
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.Wallets.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                if (search.Length > 250) search = search[..250];
                query = query.Where(x => x.User.FullName.Contains(search) || x.User.Mobile.Contains(search) ||
                    (x.User.Email != null && x.User.Email.Contains(search)));
            }
            var totalCount = await query.CountAsync(cancellationToken);
            query = (filter.SortBy?.Trim().ToLowerInvariant(), filter.SortDirection?.Trim().ToLowerInvariant()) switch
            {
                ("balance", "asc") => query.OrderBy(x => x.Balance).ThenBy(x => x.Id),
                ("balance", "desc") => query.OrderByDescending(x => x.Balance).ThenBy(x => x.Id),
                ("createdat", "asc") => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id)
            };
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminWalletListDto
            {
                Id = x.Id, UserId = x.UserId, UserFullName = x.User.FullName, UserMobile = x.User.Mobile,
                Balance = x.Balance, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
            }).ToListAsync(cancellationToken);
            return new PagedResult<AdminWalletListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }
    }
}
