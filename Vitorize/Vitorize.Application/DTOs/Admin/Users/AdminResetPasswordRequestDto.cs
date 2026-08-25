namespace Vitorize.Application.DTOs.Admin.Users
{
    /// <summary>
    /// An administrator setting another account's password.
    ///
    /// There is deliberately no current-password field: an administrator performing a reset does not
    /// know it and must not need it. The confirmation is required so a mistyped password cannot lock
    /// the account holder out of an account the administrator has just changed on their behalf.
    /// </summary>
    public class AdminResetPasswordRequestDto
    {
        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
