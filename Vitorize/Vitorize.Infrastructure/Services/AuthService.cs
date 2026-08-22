using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly JwtSettings _jwtSettings;
        private readonly ISecurityLogService _securityLogService;
        private readonly ISmsService _smsService;
        private readonly ISmsSettingsProvider _smsSettingsProvider;
        private readonly ISmsHistoryService _smsHistory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            VitorizeDbContext dbContext,
            IJwtTokenService jwtTokenService,
            IOptions<JwtSettings> jwtSettings,
            ISecurityLogService securityLogService,
            ISmsService smsService,
            ISmsSettingsProvider smsSettingsProvider,
            ISmsHistoryService smsHistory,
            ILogger<AuthService> logger)
        {
            _dbContext = dbContext;
            _jwtTokenService = jwtTokenService;
            _jwtSettings = jwtSettings.Value;
            _securityLogService = securityLogService;
            _smsService = smsService;
            _smsSettingsProvider = smsSettingsProvider;
            _smsHistory = smsHistory;
            _logger = logger;
        }

        /// <summary>
        /// First registration step: hold the account as pending and send a verification code.
        ///
        /// The account row is created immediately - that is what keeps the mobile number unique, keeps
        /// the password hashed at rest instead of parked in a pending blob, and lets a resend reuse one
        /// record instead of creating duplicates. It is created NOT login-eligible
        /// (<see cref="UserStatus.Inactive"/>, mobile unconfirmed), so no existing sign-in path will
        /// accept it: password login and OTP login both refuse anything that is not Active. Only
        /// <see cref="VerifyRegistrationAsync"/> can promote it, and only with the code.
        /// </summary>
        public async Task<RegistrationChallengeDto> StartRegistrationAsync(
            RegisterRequestDto request,
            string? ipAddress = null,
            string? userAgent = null)
        {
            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
                throw new BusinessException("شماره موبایل معتبر نیست.");

            var customerRole = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == "Customer")
                ?? throw new BusinessException("نقش پیش‌فرض مشتری یافت نشد.");

            var existing = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Mobile == mobile && !x.IsDeleted);

            if (existing is not null && !IsUnclaimedRegistration(existing))
            {
                // A real account owns this number. Say so and send nothing: issuing a code here could
                // only lead to a duplicate account or serve an attempt to take over someone else's.
                await _securityLogService.LogAsync(
                    existing.Id, "REGISTER", false,
                    $"Registration attempted for an existing account {SensitiveLogData.MaskMobile(mobile)}",
                    ipAddress, userAgent);

                throw new BusinessException(
                    "این شماره موبایل قبلاً ثبت شده است. لطفاً وارد شوید.",
                    AuthOutcomeCodes.AlreadyRegistered);
            }

            var now = DateTime.UtcNow;
            var user = existing;
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = request.FullName,
                    Mobile = mobile,
                    Email = request.Email,
                    PasswordHash = PasswordHasher.Hash(request.Password),
                    // Pending: no sign-in path accepts a non-Active account.
                    Status = (byte)UserStatus.Inactive,
                    VerificationStatus = (byte)VerificationStatus.Pending,
                    IsMobileConfirmed = false,
                    IsEmailConfirmed = false,
                    CreatedAt = now,
                    IsDeleted = false
                };
                user.Roles.Add(customerRole);
                await _dbContext.Users.AddAsync(user);
            }
            else
            {
                // An unclaimed pending registration is re-issued rather than duplicated. Whoever
                // receives the code owns the number, so refreshing the pending details is safe: an
                // abandoned attempt can never block the real owner from registering.
                user.FullName = request.FullName;
                user.Email = request.Email;
                user.PasswordHash = PasswordHasher.Hash(request.Password);
                user.UpdatedAt = now;
                if (!user.Roles.Any(x => x.Id == customerRole.Id))
                    user.Roles.Add(customerRole);
            }

            await _dbContext.SaveChangesAsync();

            // Same issuance path as every other code: purpose-bound, mobile-bound, hashed, rate
            // limited, and it invalidates any previous code for this purpose.
            await IssueAndSendOtpAsync(user, mobile, OtpPurpose.MobileVerification, ipAddress, userAgent);

            await _securityLogService.LogAsync(
                user.Id, "REGISTER_OTP_REQUEST", true,
                $"Registration code issued for {SensitiveLogData.MaskMobile(mobile)}",
                ipAddress, userAgent);

            var opts = await _smsSettingsProvider.GetAsync();
            return new RegistrationChallengeDto
            {
                MaskedMobile = IranMobile.Mask(mobile),
                ExpirySeconds = Math.Clamp(opts.OtpExpiryMinutes, 1, 15) * 60,
                ResendCooldownSeconds = Math.Max(0, opts.OtpResendCooldownSeconds),
                Outcome = AuthOutcomeCodes.RegistrationOtpSent
            };
        }

        /// <summary>
        /// Re-sends the registration code for a pending registration.
        ///
        /// Takes only the mobile, so a resend never needs the password again and can never create a
        /// second account: it refuses anything that is not an unclaimed pending registration. The
        /// existing OTP rate limits and resend cooldown apply unchanged, and issuing a new code
        /// invalidates the previous one.
        /// </summary>
        public async Task<RegistrationChallengeDto> ResendRegistrationOtpAsync(
            string mobileInput,
            string? ipAddress = null,
            string? userAgent = null)
        {
            if (!IranMobile.TryNormalize(mobileInput, out var mobile))
                throw new BusinessException("شماره موبایل معتبر نیست.");

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Mobile == mobile && !x.IsDeleted);
            if (user is null || !IsUnclaimedRegistration(user))
                throw new BusinessException("ثبت‌نام در جریانی برای این شماره یافت نشد. لطفاً دوباره ثبت‌نام کنید.");

            await IssueAndSendOtpAsync(user, mobile, OtpPurpose.MobileVerification, ipAddress, userAgent);

            await _securityLogService.LogAsync(
                user.Id, "REGISTER_OTP_RESEND", true,
                $"Registration code re-issued for {SensitiveLogData.MaskMobile(mobile)}",
                ipAddress, userAgent);

            var opts = await _smsSettingsProvider.GetAsync();
            return new RegistrationChallengeDto
            {
                MaskedMobile = IranMobile.Mask(mobile),
                ExpirySeconds = Math.Clamp(opts.OtpExpiryMinutes, 1, 15) * 60,
                ResendCooldownSeconds = Math.Max(0, opts.OtpResendCooldownSeconds),
                Outcome = AuthOutcomeCodes.RegistrationOtpSent
            };
        }

        /// <summary>
        /// A user row that exists but has never been claimed: created by an unfinished registration and
        /// never signed in. Only such a row may be taken over by a new registration for the same
        /// mobile. An account merely deactivated by an administrator has signed in before, or carries a
        /// confirmed mobile, and is never treated as claimable.
        /// </summary>
        private static bool IsUnclaimedRegistration(User user) =>
            user.Status == (byte)UserStatus.Inactive &&
            !user.IsMobileConfirmed &&
            user.LastLoginAt is null;

        /// <summary>
        /// Second registration step. On the correct code the account becomes real and the customer is
        /// signed in through the same session issuance login uses, so no second sign-in is needed.
        /// </summary>
        public async Task<AuthResponseDto> VerifyRegistrationAsync(
            VerifyRegistrationRequestDto request,
            string? ipAddress = null,
            string? userAgent = null)
        {
            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");

            var now = DateTime.UtcNow;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(
                _dbContext, $"otp:{mobile}:{(byte)OtpPurpose.MobileVerification}");

            // Purpose is part of the lookup, so a login code can never complete a registration.
            var otp = await _dbContext.OtpCodes
                .Where(x =>
                    x.Mobile == mobile &&
                    x.Purpose == (byte)OtpPurpose.MobileVerification &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > now)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp is null)
            {
                await _securityLogService.LogAsync(
                    null, "REGISTER_OTP_VERIFY", false,
                    $"No usable registration code for {SensitiveLogData.MaskMobile(mobile)}",
                    ipAddress, userAgent);
                throw new BusinessException("کد تایید معتبر نیست یا منقضی شده است.");
            }

            if (otp.AttemptCount >= otp.MaxAttempt)
                throw new BusinessException("تعداد تلاش‌های مجاز برای این کد تمام شده است.");

            if (!OtpSecurity.Verify(request.Code, otp.CodeHash))
            {
                otp.AttemptCount += 1;
                if (otp.AttemptCount >= otp.MaxAttempt)
                    otp.ConsumedAt = now;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _securityLogService.LogAsync(
                    otp.UserId, "REGISTER_OTP_VERIFY", false,
                    $"Incorrect registration code for {SensitiveLogData.MaskMobile(mobile)}",
                    ipAddress, userAgent);
                throw new BusinessException("کد تایید اشتباه است.");
            }

            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Mobile == mobile && !x.IsDeleted)
                ?? throw new BusinessException("ثبت‌نام یافت نشد. لطفاً دوباره تلاش کنید.");

            // One-time consumption, committed with the promotion in a single transaction.
            otp.ConsumedAt = now;

            var wasPending = user.Status != (byte)UserStatus.Active;
            user.Status = (byte)UserStatus.Active;
            user.IsMobileConfirmed = true;
            user.UpdatedAt = now;

            var session = await IssueSessionAsync(user, now);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _securityLogService.LogAsync(
                user.Id, "REGISTER", true,
                wasPending ? "Registration completed after mobile verification" : "Registration verification repeated",
                ipAddress, userAgent);

            _logger.LogInformation(
                "Registration verified for user {UserId}. EventType={EventType}",
                user.Id, "RegistrationVerified");

            return session;
        }

        /// <summary>
        /// The one place a customer session is minted. Login, OTP login and registration verification
        /// all go through here, so a session established by any of them is identical.
        /// </summary>
        private async Task<AuthResponseDto> IssueSessionAsync(User user, DateTime now)
        {
            user.LastLoginAt = now;

            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var userRefreshToken = new UserRefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = HashToken(refreshToken),
                ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedAt = now
            };
            await _dbContext.UserRefreshTokens.AddAsync(userRefreshToken);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                AccessToken = _jwtTokenService.GenerateAccessToken(user),
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = userRefreshToken.ExpiresAt
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Mobile == request.Mobile && !x.IsDeleted);

            if (user == null)
            {
                await _securityLogService.LogAsync(
                    null,
                    "LOGIN",
                    false,
                    $"Failed login for mobile {SensitiveLogData.MaskMobile(request.Mobile)}");

                _logger.LogWarning(
                    "Login failed for {MaskedMobile}. ReasonCategory={ReasonCategory} EventType={EventType}",
                    SensitiveLogData.MaskMobile(request.Mobile), "UnknownUser", "LoginFailed");

                // Product decision: an unregistered visitor is told to register instead of being
                // left to guess at a password. Only that one fact is disclosed, and the wrong-password
                // case below keeps its own indistinguishable message.
                throw new BusinessException(
                    "حسابی با این شماره یافت نشد. ابتدا ثبت‌نام کنید.",
                    AuthOutcomeCodes.RequiresRegistration);
            }

            var isPasswordValid = PasswordHasher.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                await _securityLogService.LogAsync(
                    user.Id,
                    "LOGIN",
                    false,
                    $"Invalid password for user {SensitiveLogData.MaskMobile(user.Mobile)}");

                _logger.LogWarning(
                    "Login failed for user {UserId} and {MaskedMobile}. ReasonCategory={ReasonCategory} EventType={EventType}",
                    user.Id, SensitiveLogData.MaskMobile(user.Mobile), "InvalidCredential", "LoginFailed");

                // Unchanged wording: an existing account must not reveal whether the mobile or the
                // password was the wrong half.
                throw new BusinessException(
                    "شماره موبایل یا رمز عبور اشتباه است.",
                    AuthOutcomeCodes.InvalidCredentials);
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new BusinessException(
                    "حساب کاربری شما فعال نیست.",
                    AuthOutcomeCodes.AccountNotEligible);
            }

            // الزام تایید موبایل برای ورود با رمز، یک تصمیم محصولی است و عمداً اعمال نمی‌شود تا
            // کاربران قدیمی قفل نشوند. ورود با کد یکبار‌مصرف به‌صورت ضمنی موبایل را تایید می‌کند
            // و کاربران می‌توانند از طریق OTP موبایل خود را تایید کنند.

            // One shared issuance path for login, OTP login and registration verification.
            var session = await IssueSessionAsync(user, DateTime.UtcNow);
            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "LOGIN",
                true,
                "User login successful");

            _logger.LogInformation(
                "Login succeeded for user {UserId}. EventType={EventType}",
                user.Id, "LoginSucceeded");

            return session;
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var refreshTokenHash = HashToken(request.RefreshToken);

            var userRefreshToken = await _dbContext.UserRefreshTokens
                .Include(x => x.User)
                    .ThenInclude(x => x.Roles)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == refreshTokenHash &&
                    x.RevokedAt == null);

            if (userRefreshToken == null)
            {
                await _securityLogService.LogAsync(
                    null,
                    "REFRESH_TOKEN",
                    false,
                    "Invalid refresh token");

                throw new UnauthorizedException("Refresh Token نامعتبر است.");
            }

            if (userRefreshToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new UnauthorizedException("Refresh Token منقضی شده است.");
            }

            var user = userRefreshToken.User;

            if (user.IsDeleted || user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
            var newRefreshTokenHash = HashToken(newRefreshToken);

            userRefreshToken.RevokedAt = DateTime.UtcNow;
            userRefreshToken.RevocationReason = "Rotated";
            userRefreshToken.ReplacedByTokenHash = newRefreshTokenHash;

            var newUserRefreshToken = new UserRefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newRefreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.UserRefreshTokens.AddAsync(newUserRefreshToken);
            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "REFRESH_TOKEN",
                true,
                "Refresh token rotated");

            var accessToken = _jwtTokenService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = newUserRefreshToken.ExpiresAt
            };
        }

        public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            return new CurrentUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                Email = user.Email,
                Status = user.Status,
                VerificationStatus = user.VerificationStatus,
                IsMobileConfirmed = user.IsMobileConfirmed,
                IsEmailConfirmed = user.IsEmailConfirmed
            };
        }

        public async Task<CurrentUserDto> UpdateProfileAsync(
            Guid userId,
            UpdateProfileRequestDto request)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            var fullName = request.FullName.Trim();

            var email = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim();

            if (email != null &&
                !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailTaken = await _dbContext.Users.AnyAsync(x =>
                    x.Id != userId &&
                    !x.IsDeleted &&
                    x.Email == email);

                if (emailTaken)
                {
                    throw new BusinessException("این ایمیل قبلا ثبت شده است.");
                }

                user.Email = email;
                user.IsEmailConfirmed = false;
            }
            else if (email == null && user.Email != null)
            {
                user.Email = null;
                user.IsEmailConfirmed = false;
            }

            user.FullName = fullName;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "PROFILE_UPDATED",
                true,
                "User profile updated");

            return new CurrentUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                Email = user.Email,
                Status = user.Status,
                VerificationStatus = user.VerificationStatus,
                IsMobileConfirmed = user.IsMobileConfirmed,
                IsEmailConfirmed = user.IsEmailConfirmed
            };
        }

        public async Task LogoutAsync(LogoutRequestDto request)
        {
            var refreshTokenHash = HashToken(request.RefreshToken);

            var userRefreshToken = await _dbContext.UserRefreshTokens
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == refreshTokenHash &&
                    x.RevokedAt == null);

            if (userRefreshToken == null)
            {
                return;
            }

            userRefreshToken.RevokedAt = DateTime.UtcNow;
            userRefreshToken.RevocationReason = "Logout";

            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                userRefreshToken.UserId,
                "LOGOUT",
                true,
                "User logout");
        }

        public async Task ChangePasswordAsync(
            Guid userId,
            ChangePasswordRequestDto request)
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                throw new BusinessException("رمز عبور جدید و تکرار آن یکسان نیستند.");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);

            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new BusinessException("رمز عبور فعلی اشتباه است.");
            }

            user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var activeRefreshTokens = await _dbContext.UserRefreshTokens
                .Where(x =>
                    x.UserId == user.Id &&
                    x.RevokedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in activeRefreshTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevocationReason = "PasswordChanged";
            }

            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "CHANGE_PASSWORD",
                true,
                "Password changed");
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            await SendOtpAsync(new SendOtpRequestDto
            {
                Mobile = request.Mobile,
                Purpose = (byte)OtpPurpose.ForgotPassword
            });
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                throw new BusinessException("رمز عبور جدید و تکرار آن یکسان نیستند.");
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(
                _dbContext, $"otp:{request.Mobile.Trim()}:{(byte)OtpPurpose.ForgotPassword}");

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x =>
                    x.Mobile == request.Mobile &&
                    !x.IsDeleted);

            if (user == null)
            {
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            var otp = await _dbContext.OtpCodes
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Mobile == request.Mobile &&
                    x.Purpose == (byte)OtpPurpose.ForgotPassword &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            if (otp.AttemptCount >= otp.MaxAttempt)
            {
                throw new BusinessException("تعداد تلاش‌های مجاز برای این کد تمام شده است.");
            }

            if (!OtpSecurity.Verify(request.Code, otp.CodeHash))
            {
                otp.AttemptCount += 1;

                if (otp.AttemptCount >= otp.MaxAttempt)
                {
                    otp.ConsumedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _securityLogService.LogAsync(
                    otp.UserId,
                    "OTP_VERIFY",
                    false,
                    "Invalid OTP code");

                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            otp.ConsumedAt = DateTime.UtcNow;

            user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var activeRefreshTokens = await _dbContext.UserRefreshTokens
                .Where(x =>
                    x.UserId == user.Id &&
                    x.RevokedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in activeRefreshTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevocationReason = "PasswordReset";
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "RESET_PASSWORD",
                true,
                "Password reset successful");
        }

        public async Task SendOtpAsync(SendOtpRequestDto request)
        {
            if (!Enum.IsDefined(typeof(OtpPurpose), request.Purpose))
            {
                throw new BusinessException("نوع کد تایید معتبر نیست.");
            }

            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
            {
                throw new BusinessException("شماره موبایل معتبر نیست.");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x =>
                    x.Mobile == mobile &&
                    !x.IsDeleted);

            // برای جلوگیری از افشای وجود حساب، در نبود کاربر بی‌صدا برمی‌گردیم.
            if (user == null)
            {
                return;
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            var purpose = (OtpPurpose)request.Purpose;
            if (purpose == OtpPurpose.TwoFactorAuthentication)
                throw new BusinessException("ارسال کد احراز هویت دومرحله‌ای تا فعال شدن جریان کامل آن غیرفعال است.");

            await IssueAndSendOtpAsync(user, mobile, purpose, ip: null, userAgent: null);
        }

        /// <summary>
        /// تولید امن کد، ابطال کدهای فعال قبلی، ذخیره‌ی هش و ارسال از طریق سرویس متمرکز پیامک.
        /// اعمال محدودیت روزانه و فاصله‌ی ارسال مجدد (cooldown) نیز اینجا انجام می‌شود.
        /// </summary>
        private async Task IssueAndSendOtpAsync(
            User user,
            string mobile,
            OtpPurpose purpose,
            string? ip,
            string? userAgent)
        {
            var opts = await _smsSettingsProvider.GetAsync();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(
                _dbContext, $"otp:{mobile}:{(byte)purpose}");

            await EnforceOtpRateLimitsAsync(mobile, (byte)purpose, opts);

            // ابطال کدهای فعال قبلی برای همان شماره و هدف (تک‌کد فعال).
            var now = DateTime.UtcNow;
            var previous = await _dbContext.OtpCodes
                .Where(x =>
                    x.Mobile == mobile &&
                    x.Purpose == (byte)purpose &&
                    x.ConsumedAt == null)
                .ToListAsync();

            foreach (var prev in previous)
                prev.ConsumedAt = now;

            var code = OtpSecurity.Generate();
            var otp = new OtpCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Mobile = mobile,
                Purpose = (byte)purpose,
                CodeHash = OtpSecurity.Hash(code),
                ExpiresAt = now.AddMinutes(Math.Clamp(opts.OtpExpiryMinutes, 1, 15)),
                CreatedAt = now,
                AttemptCount = 0,
                MaxAttempt = Math.Clamp(opts.OtpMaxAttempts, 1, 10),
                IpAddress = ip,
                UserAgent = userAgent
            };

            await _dbContext.OtpCodes.AddAsync(otp);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var templateKey = TemplateKeyForPurpose(purpose);
            var sendResult = await _smsService.SendOtpAsync(
                mobile, templateKey, code, Math.Clamp(opts.OtpExpiryMinutes, 1, 15));

            await _smsHistory.RecordDirectResultAsync(
                new SmsHistoryRecordRequest
                {
                    UserId = user.Id,
                    Mobile = mobile,
                    Purpose = purpose.ToString(),
                    SendType = (byte)SmsSendType.OtpTemplate,
                    TemplateKey = templateKey,
                    TemplateId = await _smsService.GetTemplateIdAsync(templateKey),
                    SafeMessagePreview = "قالب امن کد یکبار مصرف؛ کد ذخیره نشده است",
                    RelatedEntityType = "OtpCode",
                    RelatedEntityId = otp.Id,
                    IdempotencyKey = $"sms:otp:{otp.Id:N}",
                    MaxRetryCount = 1
                },
                sendResult);

            await _securityLogService.LogAsync(
                user.Id,
                sendResult.IsSuccess ? "OTP_SEND" : "SMS_PROVIDER_FAILURE",
                sendResult.IsSuccess,
                $"OTP {purpose} to {IranMobile.Mask(mobile)} ({(sendResult.IsSuccess ? "sent" : sendResult.FailureReason.ToString())})",
                ip,
                userAgent);

            if (sendResult.IsSuccess)
            {
                _logger.LogInformation(
                    "OTP requested and sent. UserId={UserId} MaskedMobile={MaskedMobile} Purpose={Purpose} TemplateKey={TemplateKey} EventType={EventType}",
                    user.Id, SensitiveLogData.MaskMobile(mobile), purpose, templateKey, OperationalEventNames.OtpRequested);
            }
            else
            {
                _logger.LogWarning(
                    "OTP delivery failed. UserId={UserId} MaskedMobile={MaskedMobile} Purpose={Purpose} TemplateKey={TemplateKey} FailureReason={FailureReason} EventType={EventType}",
                    user.Id, SensitiveLogData.MaskMobile(mobile), purpose, templateKey, sendResult.FailureReason, OperationalEventNames.OtpFailed);
            }

            // اگر ارسال پیامک شکست بخورد، کد یکبار‌مصرف عمل اصلی است؛ خطای امن برمی‌گردانیم.
            if (!sendResult.IsSuccess)
            {
                throw new BusinessException(
                    "ارسال کد تایید با مشکل مواجه شد. لطفاً چند لحظه بعد دوباره تلاش کنید.");
            }
        }

        private async Task EnforceOtpRateLimitsAsync(string mobile, byte purpose, SmsOptions opts)
        {
            var now = DateTime.UtcNow;

            // محدودیت روزانه برای هر شماره (همه‌ی هدف‌ها).
            var since = now.Date;
            var todayCount = await _dbContext.OtpCodes
                .CountAsync(x => x.Mobile == mobile && x.CreatedAt >= since);

            if (todayCount >= Math.Max(1, opts.DailyOtpLimitPerMobile))
            {
                _logger.LogWarning(
                    "OTP daily rate limit reached. MaskedMobile={MaskedMobile} Purpose={Purpose} EventType={EventType}",
                    SensitiveLogData.MaskMobile(mobile), purpose, OperationalEventNames.OtpRateLimited);
                throw new BusinessException(
                    "تعداد درخواست‌های کد تایید برای امروز به حداکثر رسیده است. لطفاً فردا دوباره تلاش کنید.");
            }

            // فاصله‌ی ارسال مجدد (cooldown).
            var lastCreatedAt = await _dbContext.OtpCodes
                .Where(x => x.Mobile == mobile && x.Purpose == purpose)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastCreatedAt.HasValue)
            {
                var elapsed = now - lastCreatedAt.Value;
                var cooldown = TimeSpan.FromSeconds(Math.Max(0, opts.OtpResendCooldownSeconds));

                if (elapsed < cooldown)
                {
                    _logger.LogWarning(
                        "OTP resend cooldown active. MaskedMobile={MaskedMobile} Purpose={Purpose} EventType={EventType}",
                        SensitiveLogData.MaskMobile(mobile), purpose, OperationalEventNames.OtpRateLimited);
                    throw new BusinessException(
                        "کد تایید اخیراً ارسال شده است. لطفاً پس از پایان شمارش معکوس دوباره تلاش کنید.");
                }
            }
        }

        public async Task<RequestOtpLoginResponseDto> RequestLoginOtpAsync(
            RequestOtpLoginRequestDto request,
            string? ipAddress = null,
            string? userAgent = null)
        {
            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
            {
                throw new BusinessException("شماره موبایل معتبر نیست.");
            }

            var opts = await _smsSettingsProvider.GetAsync();

            var response = new RequestOtpLoginResponseDto
            {
                MaskedMobile = IranMobile.Mask(mobile),
                ExpirySeconds = Math.Max(1, opts.OtpExpiryMinutes) * 60,
                ResendCooldownSeconds = Math.Max(0, opts.OtpResendCooldownSeconds)
            };

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Mobile == mobile && !x.IsDeleted);

            // سیاست محصول: به کاربرِ ثبت‌نام‌نشده صریحاً گفته می‌شود که ابتدا ثبت‌نام کند. پیش از این
            // پاسخ در همه‌ی حالت‌ها یکسان بود تا وجود حساب افشا نشود، اما نتیجه‌ی آن این بود که کاربر
            // به مرحله‌ی وارد کردن کدی می‌رفت که هرگز ارسال نشده بود. در هیچ حالتی پیامکی ارسال
            // نمی‌شود و هیچ کدی ساخته نمی‌شود؛ فقط دلیل، ماشین‌خوان می‌شود.
            if (user is null)
            {
                await _securityLogService.LogAsync(
                    null, "OTP_LOGIN_REQUEST", false,
                    $"Login OTP requested for {IranMobile.Mask(mobile)} (not registered)",
                    ipAddress, userAgent);

                response.Outcome = AuthOutcomeCodes.RequiresRegistration;
                return response;
            }

            // حسابی که وجود دارد ولی فعال نیست هرگز به ثبت‌نام مجدد هدایت نمی‌شود؛ این کار هم نادرست
            // است و هم می‌تواند به ساخت حساب تکراری منجر شود.
            if (user.Status != (byte)UserStatus.Active)
            {
                await _securityLogService.LogAsync(
                    user.Id, "OTP_LOGIN_REQUEST", false,
                    $"Login OTP requested for {IranMobile.Mask(mobile)} (account not active)",
                    ipAddress, userAgent);

                response.Outcome = AuthOutcomeCodes.AccountNotEligible;
                return response;
            }

            await IssueAndSendOtpAsync(user, mobile, OtpPurpose.Login, ipAddress, userAgent);

            await _securityLogService.LogAsync(
                user.Id, "OTP_LOGIN_REQUEST", true,
                $"Login OTP requested for {IranMobile.Mask(mobile)}",
                ipAddress, userAgent);

            return response;
        }

        public async Task<AuthResponseDto> VerifyLoginOtpAsync(
            VerifyOtpLoginRequestDto request,
            string? ipAddress = null,
            string? userAgent = null)
        {
            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
            {
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            var now = DateTime.UtcNow;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(
                _dbContext, $"otp:{mobile}:{(byte)OtpPurpose.Login}");

            var otp = await _dbContext.OtpCodes
                .Where(x =>
                    x.Mobile == mobile &&
                    x.Purpose == (byte)OtpPurpose.Login &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > now)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                await _securityLogService.LogAsync(
                    null, "OTP_LOGIN_FAILED", false,
                    $"No active login OTP for {IranMobile.Mask(mobile)}", ipAddress, userAgent);
                _logger.LogWarning(
                    "OTP verification failed because no active code exists. MaskedMobile={MaskedMobile} Purpose={Purpose} EventType={EventType}",
                    SensitiveLogData.MaskMobile(mobile), OtpPurpose.Login, OperationalEventNames.OtpFailed);
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            if (otp.AttemptCount >= otp.MaxAttempt)
            {
                throw new BusinessException("تعداد تلاش‌های مجاز برای این کد تمام شده است.");
            }

            if (!OtpSecurity.Verify(request.Code, otp.CodeHash))
            {
                otp.AttemptCount += 1;
                if (otp.AttemptCount >= otp.MaxAttempt)
                    otp.ConsumedAt = now;

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _securityLogService.LogAsync(
                    otp.UserId, "OTP_LOGIN_FAILED", false,
                    $"Invalid login OTP for {IranMobile.Mask(mobile)}", ipAddress, userAgent);

                _logger.LogWarning(
                    "OTP verification failed. UserId={UserId} MaskedMobile={MaskedMobile} Purpose={Purpose} AttemptCount={AttemptCount} MaxAttempt={MaxAttempt} EventType={EventType}",
                    otp.UserId, SensitiveLogData.MaskMobile(mobile), OtpPurpose.Login, otp.AttemptCount, otp.MaxAttempt, OperationalEventNames.OtpFailed);

                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            var user = await _dbContext.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == otp.UserId && !x.IsDeleted);

            if (user == null)
            {
                otp.ConsumedAt = now;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                throw new BusinessException("کد وارد شده معتبر نیست یا منقضی شده است.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                otp.ConsumedAt = now;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                await _securityLogService.LogAsync(
                    user.Id, "OTP_LOGIN_FAILED", false,
                    "Inactive account attempted OTP login", ipAddress, userAgent);
                throw new BusinessException("حساب کاربری شما فعال نیست.");
            }

            // مصرف کد (تک‌بارمصرف) + تایید ضمنی موبایل.
            otp.ConsumedAt = now;
            if (!user.IsMobileConfirmed)
                user.IsMobileConfirmed = true;

            // Same shared issuance path as password login and registration verification.
            var session = await IssueSessionAsync(user, now);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _securityLogService.LogAsync(
                user.Id, "OTP_LOGIN_SUCCESS", true,
                "Customer OTP login successful", ipAddress, userAgent);

            _logger.LogInformation(
                "OTP verification succeeded. UserId={UserId} MaskedMobile={MaskedMobile} Purpose={Purpose} EventType={EventType}",
                user.Id, SensitiveLogData.MaskMobile(mobile), OtpPurpose.Login, OperationalEventNames.OtpVerified);

            return session;
        }

        private static string TemplateKeyForPurpose(OtpPurpose purpose) => purpose switch
        {
            OtpPurpose.Login => SmsTemplateKeys.LoginOtp,
            OtpPurpose.MobileVerification => SmsTemplateKeys.RegisterOtp,
            OtpPurpose.ForgotPassword => SmsTemplateKeys.ForgotPassword,
            _ => SmsTemplateKeys.GenericOtp
        };


        public async Task VerifyOtpAsync(VerifyOtpRequestDto request)
        {
            if (!Enum.IsDefined(typeof(OtpPurpose), request.Purpose))
            {
                throw new BusinessException("نوع کد تایید معتبر نیست.");
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(
                _dbContext, $"otp:{request.Mobile.Trim()}:{request.Purpose}");

            var otp = await _dbContext.OtpCodes
                .Where(x =>
                    x.Mobile == request.Mobile &&
                    x.Purpose == request.Purpose &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                throw new BusinessException("کد تایید معتبر نیست یا منقضی شده است.");
            }

            if (otp.AttemptCount >= otp.MaxAttempt)
            {
                throw new BusinessException("تعداد تلاش‌های مجاز برای این کد تمام شده است.");
            }

            if (!OtpSecurity.Verify(request.Code, otp.CodeHash))
            {
                otp.AttemptCount += 1;

                if (otp.AttemptCount >= otp.MaxAttempt)
                {
                    otp.ConsumedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                throw new BusinessException("کد تایید اشتباه است.");
            }

            otp.ConsumedAt = DateTime.UtcNow;

            if (request.Purpose == (byte)OtpPurpose.MobileVerification && otp.UserId.HasValue)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(x => x.Id == otp.UserId.Value && !x.IsDeleted);

                if (user != null)
                {
                    user.IsMobileConfirmed = true;
                    user.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _securityLogService.LogAsync(
                otp.UserId,
                "OTP_VERIFY",
                true,
                $"OTP verified for purpose {request.Purpose}");
        }

        private static string HashToken(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToBase64String(hashBytes);
        }
    }
}
