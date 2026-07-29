namespace Vitorize.Web.Services.Auth
{
    public static class VitorizeAuthSchemes
    {
        public const string SmartScheme = "Vitorize.Smart";
        public const string AdminScheme = "Vitorize.Admin";
        public const string CustomerScheme = "Vitorize.Customer";

        // Cookie-authentication session cookies (written by SignInAsync).
        public const string AdminAuthCookie = "Vitorize.Admin.Auth";
        public const string CustomerAuthCookie = "Vitorize.Customer.Auth";

        public const string AdminAccessTokenCookie = "Vitorize.Admin.AccessToken";
        public const string AdminRefreshTokenCookie = "Vitorize.Admin.RefreshToken";

        public const string CustomerAccessTokenCookie = "Vitorize.Customer.AccessToken";
        public const string CustomerRefreshTokenCookie = "Vitorize.Customer.RefreshToken";

        public static IEnumerable<string> TokenCookiesFor(string? scheme) => scheme switch
        {
            AdminScheme => [AdminAuthCookie, AdminAccessTokenCookie, AdminRefreshTokenCookie],
            CustomerScheme => [CustomerAuthCookie, CustomerAccessTokenCookie, CustomerRefreshTokenCookie],
            _ => []
        };
    }
}
