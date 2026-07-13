using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

        Task<CurrentUserDto> GetCurrentUserAsync(Guid userId);

        Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request);

        Task LogoutAsync(LogoutRequestDto request);

        Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request);

        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);

        Task ResetPasswordAsync(ResetPasswordRequestDto request);

        Task SendOtpAsync(SendOtpRequestDto request);

        Task VerifyOtpAsync(VerifyOtpRequestDto request);

        // ---- ورود با کد یکبار‌مصرف (OTP Login) ----

        Task<RequestOtpLoginResponseDto> RequestLoginOtpAsync(
            RequestOtpLoginRequestDto request,
            string? ipAddress = null,
            string? userAgent = null);

        Task<AuthResponseDto> VerifyLoginOtpAsync(
            VerifyOtpLoginRequestDto request,
            string? ipAddress = null,
            string? userAgent = null);
    }
}