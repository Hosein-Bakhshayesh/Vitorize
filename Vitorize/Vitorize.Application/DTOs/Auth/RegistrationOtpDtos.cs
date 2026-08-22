namespace Vitorize.Application.DTOs.Auth
{
    /// <summary>
    /// Result of the first registration step. Deliberately carries no tokens: an account whose mobile
    /// has not been verified must never receive an authenticated session.
    /// </summary>
    public class RegistrationChallengeDto
    {
        public string MaskedMobile { get; set; } = string.Empty;
        public int ExpirySeconds { get; set; }
        public int ResendCooldownSeconds { get; set; }

        /// <summary>One of <c>AuthOutcomeCodes</c>; <c>RegistrationOtpSent</c> on success.</summary>
        public string Outcome { get; set; } = Vitorize.Application.Common.AuthOutcomeCodes.RegistrationOtpSent;
    }

    /// <summary>Asks for the pending registration's code to be sent again.</summary>
    public class ResendRegistrationRequestDto
    {
        public string Mobile { get; set; } = string.Empty;
    }

    /// <summary>Second registration step: the code the customer received, bound to their mobile.</summary>
    public class VerifyRegistrationRequestDto
    {
        public string Mobile { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
