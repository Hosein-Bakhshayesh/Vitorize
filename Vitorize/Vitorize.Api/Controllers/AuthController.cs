using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [SwaggerTag("Authentication APIs for register, login, token refresh, OTP and password management.")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUserService;

        public AuthController(
            IAuthService authService,
            ICurrentUserService currentUserService)
        {
            _authService = authService;
            _currentUserService = currentUserService;
        }

        [EnableRateLimiting("register")]
        [HttpPost("register")]
        [SwaggerOperation(
            Summary = "ثبت‌نام کاربر",
            Description = "ایجاد حساب کاربری جدید برای مشتری و دریافت AccessToken و RefreshToken.")]
        [ProducesResponseType(typeof(ApiResult<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> Register(RegisterRequestDto request)
        {
            var result = await _authService.RegisterAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "ثبت‌نام با موفقیت انجام شد."));
        }

        [EnableRateLimiting("login")]
        [HttpPost("login")]
        [SwaggerOperation(
            Summary = "ورود کاربر",
            Description = "ورود با شماره موبایل و رمز عبور و دریافت AccessToken و RefreshToken.")]
        [ProducesResponseType(typeof(ApiResult<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> Login(LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "ورود با موفقیت انجام شد."));
        }

        [HttpPost("refresh-token")]
        [SwaggerOperation(
            Summary = "تمدید توکن",
            Description = "دریافت AccessToken جدید با استفاده از RefreshToken معتبر.")]
        [ProducesResponseType(typeof(ApiResult<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> RefreshToken(RefreshTokenRequestDto request)
        {
            var result = await _authService.RefreshTokenAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "توکن با موفقیت تمدید شد."));
        }

        [Authorize]
        [HttpGet("me")]
        [SwaggerOperation(
            Summary = "اطلاعات کاربر فعلی",
            Description = "دریافت پروفایل و وضعیت احراز هویت کاربر لاگین‌شده.")]
        [ProducesResponseType(typeof(ApiResult<CurrentUserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<CurrentUserDto>>> Me()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var result = await _authService.GetCurrentUserAsync(
                _currentUserService.UserId.Value);

            return Ok(ApiResult<CurrentUserDto>.Success(
                result,
                "اطلاعات کاربر با موفقیت دریافت شد."));
        }

        [Authorize]
        [HttpPut("profile")]
        [SwaggerOperation(
            Summary = "ویرایش پروفایل",
            Description = "به‌روزرسانی نام و ایمیل کاربر لاگین‌شده. تغییر ایمیل باعث نیاز به تایید مجدد ایمیل می‌شود.")]
        [ProducesResponseType(typeof(ApiResult<CurrentUserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<CurrentUserDto>>> UpdateProfile(
            UpdateProfileRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var result = await _authService.UpdateProfileAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(ApiResult<CurrentUserDto>.Success(
                result,
                "پروفایل با موفقیت به‌روزرسانی شد."));
        }

        [Authorize]
        [HttpPost("logout")]
        [SwaggerOperation(
            Summary = "خروج از حساب",
            Description = "باطل کردن RefreshToken کاربر و خروج از حساب.")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult>> Logout(LogoutRequestDto request)
        {
            await _authService.LogoutAsync(request);

            return Ok(ApiResult.Success("خروج از حساب با موفقیت انجام شد."));
        }

        [Authorize]
        [HttpPost("change-password")]
        [SwaggerOperation(
            Summary = "تغییر رمز عبور",
            Description = "تغییر رمز عبور کاربر لاگین‌شده و باطل کردن RefreshTokenهای فعال.")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult>> ChangePassword(ChangePasswordRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await _authService.ChangePasswordAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(ApiResult.Success("رمز عبور با موفقیت تغییر کرد."));
        }

        [HttpPost("forgot-password")]
        [SwaggerOperation(
            Summary = "درخواست بازیابی رمز عبور",
            Description = "ارسال کد بازیابی رمز عبور برای شماره موبایل ثبت‌شده.")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResult>> ForgotPassword(ForgotPasswordRequestDto request)
        {
            await _authService.ForgotPasswordAsync(request);

            return Ok(ApiResult.Success(
                "در صورت وجود حساب کاربری، کد بازیابی ارسال خواهد شد."));
        }

        [HttpPost("reset-password")]
        [SwaggerOperation(
            Summary = "بازیابی رمز عبور",
            Description = "تغییر رمز عبور با استفاده از کد تایید بازیابی.")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResult>> ResetPassword(ResetPasswordRequestDto request)
        {
            await _authService.ResetPasswordAsync(request);

            return Ok(ApiResult.Success("رمز عبور با موفقیت بازیابی شد."));
        }

        [EnableRateLimiting("otp")]
        [HttpPost("send-otp")]
        [SwaggerOperation(
            Summary = "ارسال کد تایید",
            Description = "ارسال OTP برای تایید موبایل یا بازیابی رمز عبور از طریق سرویس پیامک (SMS.ir).")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResult>> SendOtp(SendOtpRequestDto request)
        {
            await _authService.SendOtpAsync(request);

            return Ok(ApiResult.Success("کد تایید با موفقیت ارسال شد."));
        }

        [HttpPost("verify-otp")]
        [SwaggerOperation(
            Summary = "تایید کد OTP",
            Description = "بررسی کد تایید و انجام عملیات مربوط به Purpose مانند تایید موبایل.")]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResult>> VerifyOtp(VerifyOtpRequestDto request)
        {
            await _authService.VerifyOtpAsync(request);

            return Ok(ApiResult.Success("کد تایید با موفقیت تایید شد."));
        }

        [EnableRateLimiting("otp")]
        [HttpPost("login/otp/request")]
        [SwaggerOperation(
            Summary = "درخواست کد ورود یکبار‌مصرف",
            Description = "ارسال کد ورود به شماره موبایل مشتری. پاسخ عمومی است و وجود/عدم‌وجود حساب را افشا نمی‌کند.")]
        [ProducesResponseType(typeof(ApiResult<RequestOtpLoginResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResult<RequestOtpLoginResponseDto>>> RequestLoginOtp(
            RequestOtpLoginRequestDto request)
        {
            var result = await _authService.RequestLoginOtpAsync(
                request, GetClientIp(), GetUserAgent());

            return Ok(ApiResult<RequestOtpLoginResponseDto>.Success(
                result,
                "در صورت وجود حساب کاربری فعال، کد ورود ارسال خواهد شد."));
        }

        [EnableRateLimiting("login")]
        [HttpPost("login/otp/verify")]
        [SwaggerOperation(
            Summary = "تایید کد ورود و ورود مشتری",
            Description = "بررسی کد ورود و صدور AccessToken و RefreshToken مانند ورود با رمز عبور.")]
        [ProducesResponseType(typeof(ApiResult<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> VerifyLoginOtp(
            VerifyOtpLoginRequestDto request)
        {
            var result = await _authService.VerifyLoginOtpAsync(
                request, GetClientIp(), GetUserAgent());

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "ورود با موفقیت انجام شد."));
        }

        private string? GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent()
        {
            var ua = Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : ua;
        }
    }
}