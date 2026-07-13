namespace Vitorize.Shared.Enums
{
    public enum OtpPurpose : byte
    {
        MobileVerification = 1,
        ForgotPassword = 2,
        TwoFactorAuthentication = 3,

        /// <summary>ورود بدون رمز عبور با کد یکبار‌مصرف (OTP Login).</summary>
        Login = 4
    }
}
