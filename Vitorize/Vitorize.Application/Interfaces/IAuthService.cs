using Vitorize.Application.DTOs.Auth;

namespace Vitorize.Application.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Begins registration: creates or refreshes a NOT login-eligible pending account and sends a
        /// mobile verification code. Deliberately returns no tokens - see
        /// <see cref="VerifyRegistrationAsync"/>.
        /// </summary>
        Task<RegistrationChallengeDto> StartRegistrationAsync(RegisterRequestDto request, string? ipAddress = null, string? userAgent = null);

        /// <summary>
        /// Completes registration with the code sent to the customer's mobile, then establishes the
        /// session through the same issuance path login uses.
        /// </summary>
        Task<AuthResponseDto> VerifyRegistrationAsync(VerifyRegistrationRequestDto request, string? ipAddress = null, string? userAgent = null);

        /// <summary>
        /// Re-sends the pending registration's code. Requires only the mobile, and refuses anything
        /// that is not an unclaimed pending registration.
        /// </summary>
        Task<RegistrationChallengeDto> ResendRegistrationOtpAsync(string mobile, string? ipAddress = null, string? userAgent = null);

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