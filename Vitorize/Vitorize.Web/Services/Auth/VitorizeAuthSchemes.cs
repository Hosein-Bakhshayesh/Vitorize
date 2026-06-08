namespace Vitorize.Web.Services.Auth
{
    public static class VitorizeAuthSchemes
    {
        public const string AdminScheme = "Vitorize.Admin";
        public const string CustomerScheme = "Vitorize.Customer";

        public const string AdminAccessTokenCookie = "Vitorize.Admin.AccessToken";
        public const string AdminRefreshTokenCookie = "Vitorize.Admin.RefreshToken";

        public const string CustomerAccessTokenCookie = "Vitorize.Customer.AccessToken";
        public const string CustomerRefreshTokenCookie = "Vitorize.Customer.RefreshToken";
    }
}
