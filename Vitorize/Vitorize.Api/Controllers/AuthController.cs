using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> Register(RegisterRequestDto request)
        {
            var result = await _authService.RegisterAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "ثبت‌نام با موفقیت انجام شد."));
        }

        [EnableRateLimiting("login")]
        [HttpPost("login")]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> Login(LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "ورود با موفقیت انجام شد."));
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<ApiResult<AuthResponseDto>>> RefreshToken(RefreshTokenRequestDto request)
        {
            var result = await _authService.RefreshTokenAsync(request);

            return Ok(ApiResult<AuthResponseDto>.Success(
                result,
                "توکن با موفقیت تمدید شد."));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ApiResult<CurrentUserDto>>> Me()
        {
            if (!_currentUserService.UserId.HasValue)
            {
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            }

            var result = await _authService.GetCurrentUserAsync(
                _currentUserService.UserId.Value);

            return Ok(ApiResult<CurrentUserDto>.Success(
                result,
                "اطلاعات کاربر با موفقیت دریافت شد."));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResult>> Logout(LogoutRequestDto request)
        {
            await _authService.LogoutAsync(request);

            return Ok(ApiResult.Success("خروج از حساب با موفقیت انجام شد."));
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResult>> ChangePassword(ChangePasswordRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
            {
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");
            }

            await _authService.ChangePasswordAsync(
                _currentUserService.UserId.Value,
                request);

            return Ok(ApiResult.Success("رمز عبور با موفقیت تغییر کرد."));
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResult>> ForgotPassword(ForgotPasswordRequestDto request)
        {
            await _authService.ForgotPasswordAsync(request);

            return Ok(ApiResult.Success(
                "در صورت وجود حساب کاربری، کد بازیابی ارسال خواهد شد."));
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResult>> ResetPassword(ResetPasswordRequestDto request)
        {
            await _authService.ResetPasswordAsync(request);

            return Ok(ApiResult.Success("رمز عبور با موفقیت بازیابی شد."));
        }

        [EnableRateLimiting("otp")]
        [HttpPost("send-otp")]
        public async Task<ActionResult<ApiResult>> SendOtp(SendOtpRequestDto request)
        {
            await _authService.SendOtpAsync(request);

            return Ok(ApiResult.Success("کد تایید با موفقیت ارسال شد."));
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult<ApiResult>> VerifyOtp(VerifyOtpRequestDto request)
        {
            await _authService.VerifyOtpAsync(request);

            return Ok(ApiResult.Success("کد تایید با موفقیت تایید شد."));
        }
    }
}