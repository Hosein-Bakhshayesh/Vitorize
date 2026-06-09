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

        public AdminUserService(
            VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<AdminUserDto>> GetAllAsync(
    AdminUserFilterDto filter)
        {
            var query = _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Roles)
                .Include(x => x.Wallet)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(x =>
                    x.FullName.Contains(filter.Search) ||
                    x.Mobile.Contains(filter.Search) ||
                    (x.Email != null &&
                     x.Email.Contains(filter.Search)));
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == filter.Status.Value);
            }

            if (filter.VerificationStatus.HasValue)
            {
                query = query.Where(x =>
                    x.VerificationStatus ==
                    filter.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                query = query.Where(x =>
                    x.Roles.Any(r =>
                        r.Name == filter.Role));
            }

            var totalCount =
                await query.CountAsync();

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
                    VerificationStatus =
                        x.VerificationStatus,
                    IsMobileConfirmed =
                        x.IsMobileConfirmed,
                    CreatedAt = x.CreatedAt,
                    LastLoginAt = x.LastLoginAt,

                    WalletBalance =
                        x.Wallet != null
                            ? x.Wallet.Balance
                            : 0,

                    OrdersCount =
                        x.Orders.Count,

                    TicketsCount =
                        x.Tickets.Count,

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
                .Include(x => x.Roles)
                .Include(x => x.Wallet)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

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
            var user = await GetUser(userId);

            user.Status = (byte)UserStatus.Active;

            await _dbContext.SaveChangesAsync();
        }

        public async Task SuspendAsync(Guid userId)
        {
            var user = await GetUser(userId);

            user.Status = (byte)UserStatus.Suspended;

            await _dbContext.SaveChangesAsync();
        }

        public async Task BlockAsync(Guid userId)
        {
            var user = await GetUser(userId);

            user.Status = (byte)UserStatus.Blocked;

            await _dbContext.SaveChangesAsync();
        }

        public async Task AddRoleAsync(
            Guid userId,
            string roleName)
        {
            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Name == roleName);

            if (role == null)
            {
                throw new NotFoundException("نقش یافت نشد.");
            }

            if (user.Roles.Any(x => x.Id == role.Id))
            {
                return;
            }

            user.Roles.Add(role);

            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveRoleAsync(
            Guid userId,
            string roleName)
        {
            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            var role = user.Roles
                .FirstOrDefault(x => x.Name == roleName);

            if (role == null)
            {
                return;
            }

            user.Roles.Remove(role);

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Domain.Entities.User> GetUser(Guid userId)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            return user;
        }
    }
}