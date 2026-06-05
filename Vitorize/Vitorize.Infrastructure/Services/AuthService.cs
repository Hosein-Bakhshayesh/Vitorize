using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Domain.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
            VitorizeDbContext dbContext,
            IJwtTokenService jwtTokenService,
            IOptions<JwtSettings> jwtSettings)
        {
            _dbContext = dbContext;
            _jwtTokenService = jwtTokenService;
            _jwtSettings = jwtSettings.Value;
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
                throw new BusinessException("شماره موبایل یا رمز عبور اشتباه است.");
            }

            var isPasswordValid = PasswordHasher.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                throw new BusinessException("شماره موبایل یا رمز عبور اشتباه است.");
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new BusinessException("حساب کاربری شما فعال نیست.");
            }

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

            var codeHash = HashToken(request.Code);

            if (otp.CodeHash != codeHash)
            {
                otp.AttemptCount += 1;

                if (otp.AttemptCount >= otp.MaxAttempt)
                {
                    otp.ConsumedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

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
        }

        public async Task SendOtpAsync(SendOtpRequestDto request)
        {
            if (!Enum.IsDefined(typeof(OtpPurpose), request.Purpose))
            {
                throw new BusinessException("نوع کد تایید معتبر نیست.");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x =>
                    x.Mobile == request.Mobile &&
                    !x.IsDeleted);

            if (user == null)
            {
                return;
            }

            if (user.Status != (byte)UserStatus.Active)
            {
                throw new UnauthorizedException("حساب کاربری فعال نیست.");
            }

            var activeOtpExists = await _dbContext.OtpCodes
                .AnyAsync(x =>
                    x.Mobile == request.Mobile &&
                    x.Purpose == request.Purpose &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow);

            if (activeOtpExists)
            {
                throw new BusinessException("کد تایید قبلاً ارسال شده است. لطفاً چند دقیقه بعد دوباره تلاش کنید.");
            }

            var code = Random.Shared.Next(100000, 999999).ToString();
            var codeHash = HashToken(code);

            var otp = new OtpCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Mobile = user.Mobile,
                Purpose = request.Purpose,
                CodeHash = codeHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                AttemptCount = 0,
                MaxAttempt = 5
            };

            await _dbContext.OtpCodes.AddAsync(otp);
            await _dbContext.SaveChangesAsync();

            Console.WriteLine($"Vitorize OTP Code: {code}");
        }

        public async Task VerifyOtpAsync(VerifyOtpRequestDto request)
        {
            if (!Enum.IsDefined(typeof(OtpPurpose), request.Purpose))
            {
                throw new BusinessException("نوع کد تایید معتبر نیست.");
            }

            var codeHash = HashToken(request.Code);

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

            if (otp.CodeHash != codeHash)
            {
                otp.AttemptCount += 1;

                if (otp.AttemptCount >= otp.MaxAttempt)
                {
                    otp.ConsumedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

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
        }

        private static string HashToken(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var hashBytes = SHA256.HashData(bytes);

            return Convert.ToBase64String(hashBytes);
        }
    }
}