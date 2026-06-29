namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// مسئول تامین توکن دسترسی ادمین برای فراخوانی APIها.
    /// در حالت تعاملی از claimها و در حالت SSR از کوکی استفاده می‌کند.
    /// </summary>
    public interface IAccessTokenProvider
    {
        Task<string?> GetAccessTokenAsync();
    }
}
