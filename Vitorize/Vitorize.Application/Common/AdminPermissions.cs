namespace Vitorize.Application.Common;

public static class AdminPermissions
{
    public const string ClaimType = "permission";
    public const string FinanceManage = "finance.manage";
    public const string OrderFulfillment = "orders.fulfill";
    public const string KycReview = "kyc.review";
    public const string KycManage = "kyc.manage";
    public const string SecurityDiagnostics = "security.diagnostics";
    public const string SettingsManage = "settings.manage";
    public const string UserManage = "users.manage";

    /// <summary>
    /// Setting another account's password. Separate from &lt;see cref="UserManage"/&gt; on purpose:
    /// listing and suspending users is routine administration, while replacing someone's credentials
    /// takes over their account and ends every session they hold, so it is worth being able to grant
    /// the two independently. Changing one's <i>own</i> password needs no permission at all - that
    /// endpoint is scoped to the caller.
    /// </summary>
    public const string UserPasswordReset = "users.password.reset";

    public static readonly string[] All =
    [
        FinanceManage, OrderFulfillment, KycReview, KycManage,
        SecurityDiagnostics, SettingsManage, UserManage, UserPasswordReset
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
            result.Add(KycManage);
            result.Add(SettingsManage);
            // Administrators reach /admin/users through the page's role check, but the API behind it
            // required users.manage, which only SuperAdmin held - so the page opened and every call on
            // it returned 403. Granting it here closes that mismatch and makes the password reset
            // below reachable by the role that actually administers users day to day.
            result.Add(UserManage);
            result.Add(UserPasswordReset);
        }
        // A deliberately read-only administration role for KYC policy review.
        // Mutations remain gated by kyc.manage.
        if (set.Contains("KycViewer"))
            result.Add(KycReview);
        if (set.Contains("Support"))
            result.Add(OrderFulfillment);
        return result;
    }
}
