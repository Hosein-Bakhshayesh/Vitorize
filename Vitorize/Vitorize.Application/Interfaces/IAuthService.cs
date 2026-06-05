using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

        Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

        Task<CurrentUserDto> GetCurrentUserAsync(Guid userId);

        Task LogoutAsync(LogoutRequestDto request);

        Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request);

        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);

        Task ResetPasswordAsync(ResetPasswordRequestDto request);

        Task SendOtpAsync(SendOtpRequestDto request);

        Task VerifyOtpAsync(VerifyOtpRequestDto request);
    }
}