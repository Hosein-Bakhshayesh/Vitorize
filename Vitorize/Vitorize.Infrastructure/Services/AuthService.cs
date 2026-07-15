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

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            var mobileExists = await _dbContext.Users
                .AnyAsync(x => x.Mobile == request.Mobile && !x.IsDeleted);

            if (mobileExists)
            {
                throw new BusinessException("این شماره موبایل قبلا ثبت شده است.");
            }

            var customerRole = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Name == "Customer");

            if (customerRole == null)
            {
                throw new BusinessException("نقش پیش‌فرض مشتری یافت نشد.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Mobile = request.Mobile,
                Email = request.Email,
                PasswordHash = PasswordHasher.Hash(request.Password),
                Status = (byte)UserStatus.Active,
                VerificationStatus = (byte)VerificationStatus.Pending,
                IsMobileConfirmed = false,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            user.Roles.Add(customerRole);

            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenHash = HashToken(refreshToken);

            var userRefreshToken = new UserRefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(user);
            await _dbContext.UserRefreshTokens.AddAsync(userRefreshToken);
            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "REGISTER",
                true,
                "User registration successful");

            var accessToken = _jwtTokenService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
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
                    $"Failed login for mobile {request.Mobile}");

                throw new BusinessException("شماره موبایل یا رمز عبور اشتباه است.");
            }

            var isPasswordValid = PasswordHasher.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                await _securityLogService.LogAsync(
                    user.Id,
                    "LOGIN",
                    false,
                    $"Invalid password for user {user.Mobile}");

                throw new BusinessException("شماره موبایل یا رمز عبور اشتباه است.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new BusinessException("حساب کاربری شما فعال نیست.");
            }

            // الزام تایید موبایل برای ورود با رمز، یک تصمیم محصولی است و عمداً اعمال نمی‌شود تا
            // کاربران قدیمی قفل نشوند. ورود با کد یکبار‌مصرف به‌صورت ضمنی موبایل را تایید می‌کند
            // و کاربران می‌توانند از طریق OTP موبایل خود را تایید کنند.

            user.LastLoginAt = DateTime.UtcNow;

            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            var refreshTokenHash = HashToken(refreshToken);

            var userRefreshToken = new UserRefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.UserRefreshTokens.AddAsync(userRefreshToken);
            await _dbContext.SaveChangesAsync();

            await _securityLogService.LogAsync(
                user.Id,
                "LOGIN",
                true,
                "User login successful");

            var accessToken = _jwtTokenService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = userRefreshToken.ExpiresAt
            };
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

            // سیاست: ورود با کد فقط برای مشتریان فعال موجود است. برای جلوگیری از افشای
            // وجود حساب، پاسخ در همه‌ی حالت‌ها یکسان است و در نبود کاربر پیامکی ارسال نمی‌شود.
            if (user is null || user.Status != (byte)UserStatus.Active)
            {
                await _securityLogService.LogAsync(
                    user?.Id, "OTP_LOGIN_REQUEST", false,
                    $"Login OTP requested for {IranMobile.Mask(mobile)} (no active account)",
                    ipAddress, userAgent);

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
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _securityLogService.LogAsync(
                user.Id, "OTP_LOGIN_SUCCESS", true,
                "Customer OTP login successful", ipAddress, userAgent);

            var accessToken = _jwtTokenService.GenerateAccessToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Mobile = user.Mobile,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = userRefreshToken.ExpiresAt
            };
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
