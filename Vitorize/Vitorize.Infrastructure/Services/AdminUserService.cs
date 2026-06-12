using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Users;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminUserService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<AdminUserDto>> GetAllAsync(
            AdminUserFilterDto filter)
        {
            filter.Page = filter.Page <= 0 ? 1 : filter.Page;
            filter.PageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            filter.PageSize = filter.PageSize > 100 ? 100 : filter.PageSize;

            var query = _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Roles)
                .Include(x => x.Wallet)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();

                query = query.Where(x =>
                    x.FullName.Contains(search) ||
                    x.Mobile.Contains(search) ||
                    (x.Email != null && x.Email.Contains(search)));
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == filter.Status.Value);
            }

            if (filter.VerificationStatus.HasValue)
            {
                query = query.Where(x =>
                    x.VerificationStatus == filter.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var role = filter.Role.Trim();

                query = query.Where(x =>
                    x.Roles.Any(r => r.Name == role));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new AdminUserDto
                {
                    Id = x.Id,
                    FullName = x.FullName,
                    Mobile = x.Mobile,
                    Email = x.Email,
                    Status = x.Status,
                    VerificationStatus = x.VerificationStatus,
                    IsMobileConfirmed = x.IsMobileConfirmed,
                    CreatedAt = x.CreatedAt,
                    LastLoginAt = x.LastLoginAt,
                    WalletBalance = x.Wallet != null ? x.Wallet.Balance : 0,
                    OrdersCount = x.Orders.Count,
                    TicketsCount = x.Tickets.Count,
                    Roles = x.Roles
                        .Select(r => r.Name)
                        .ToList()
                })
                .ToListAsync();

            return new PagedResult<AdminUserDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<AdminUserDetailDto> GetByIdAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Roles)
                .Include(x => x.Wallet)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            return new AdminUserDetailDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                Email = user.Email,
                NationalCode = user.NationalCode,
                AvatarPath = user.AvatarPath,
                Status = user.Status,
                VerificationStatus = user.VerificationStatus,
                IsMobileConfirmed = user.IsMobileConfirmed,
                IsEmailConfirmed = user.IsEmailConfirmed,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                DeletedAt = user.DeletedAt,
                LastLoginAt = user.LastLoginAt,
                IsDeleted = user.IsDeleted,
                WalletBalance = user.Wallet?.Balance ?? 0,
                OrdersCount = await _dbContext.Orders
                    .CountAsync(x => x.UserId == user.Id),
                TicketsCount = await _dbContext.Tickets
                    .CountAsync(x => x.UserId == user.Id),
                Roles = user.Roles
                    .Select(x => x.Name)
                    .ToList()
            };
        }

        public async Task ActivateAsync(Guid userId)
        {
            var user = await GetUserAsync(userId);

            user.Status = (byte)UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task SuspendAsync(Guid userId)
        {
            var user = await GetUserAsync(userId);

            user.Status = (byte)UserStatus.Suspended;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task BlockAsync(Guid userId)
        {
            var user = await GetUserAsync(userId);

            user.Status = (byte)UserStatus.Blocked;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task AddRoleAsync(Guid userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new BusinessException("نام نقش الزامی است.");

            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var normalizedRoleName = roleName.Trim();

            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Name == normalizedRoleName);

            if (role == null)
                throw new NotFoundException("نقش یافت نشد.");

            if (user.Roles.Any(x => x.Id == role.Id))
                return;

            user.Roles.Add(role);
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveRoleAsync(Guid userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new BusinessException("نام نقش الزامی است.");

            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            var normalizedRoleName = roleName.Trim();

            var role = user.Roles
                .FirstOrDefault(x => x.Name == normalizedRoleName);

            if (role == null)
                return;

            if (role.Name == "Customer" && user.Roles.Count == 1)
                throw new BusinessException("کاربر باید حداقل یک نقش داشته باشد.");

            user.Roles.Remove(role);
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Domain.Entities.User> GetUserAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                throw new NotFoundException("کاربر یافت نشد.");

            return user;
        }
    }
}