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
        private readonly IAuditService _auditService;
        private readonly ISecurityLogService _securityLogService;
        private readonly ICurrentUserService _currentUser;

        public AdminUserService(
            VitorizeDbContext dbContext,
            IAuditService auditService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUser)
        {
            _dbContext = dbContext;
            _auditService = auditService;
            _securityLogService = securityLogService;
            _currentUser = currentUser;
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

        /// <summary>
        /// Sets another account's password.
        ///
        /// Two things make this different from the self-service change. There is no current-password
        /// check, because an administrator does not have it - authorisation is the control here, and it
        /// is enforced on the endpoint. And every one of that user's sessions is revoked: a password
        /// reset that left existing refresh tokens alive would not actually take the account back,
        /// which is usually the whole reason for doing it.
        ///
        /// The password itself is hashed immediately and never logged, returned or audited.
        /// </summary>
        public async Task<int> ResetPasswordAsync(Guid userId, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new BusinessException("رمز عبور جدید الزامی است.");

            if (newPassword != confirmPassword)
                throw new BusinessException("رمز عبور جدید و تکرار آن یکسان نیستند.");

            var user = await GetUserAsync(userId);

            user.PasswordHash = PasswordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var active = await _dbContext.UserRefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in active)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevocationReason = "PasswordResetByAdmin";
            }

            await _dbContext.SaveChangesAsync();

            // An administrator acting on another account is an audit-trail event, so it is recorded
            // against the user entity with who did it - never with any password material.
            await _auditService.LogAsync(
                _currentUser.UserId,
                "UserPasswordReset",
                nameof(Vitorize.Domain.Entities.User),
                userId.ToString(),
                $"sessionsRevoked:{active.Count}",
                _currentUser.IpAddress,
                _currentUser.UserAgent);

            // And against the affected user in the security log, alongside their own password events.
            await _securityLogService.LogAsync(
                userId,
                "RESET_PASSWORD",
                true,
                "Password reset by an administrator; all sessions revoked");

            return active.Count;
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