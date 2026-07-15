namespace Vitorize.Application.Common;

public static class AdminPermissions
{
    public const string ClaimType = "permission";
    public const string FinanceManage = "finance.manage";
    public const string OrderFulfillment = "orders.fulfill";
    public const string KycReview = "kyc.review";
    public const string SecurityDiagnostics = "security.diagnostics";
    public const string SettingsManage = "settings.manage";
    public const string UserManage = "users.manage";

    public static readonly string[] All =
    [
        FinanceManage, OrderFulfillment, KycReview,
        SecurityDiagnostics, SettingsManage, UserManage
    ];

    public static IEnumerable<string> ForRoles(IEnumerable<string> roles)
    {
        var set = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Contains("SuperAdmin")) return All;
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (set.Contains("Admin"))
        {
            result.Add(OrderFulfillment);
            result.Add(KycReview);
            result.Add(SettingsManage);
        }
        if (set.Contains("Support"))
            result.Add(OrderFulfillment);
        return result;
    }
}
