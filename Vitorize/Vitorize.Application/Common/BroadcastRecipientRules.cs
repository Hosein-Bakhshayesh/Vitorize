using System.Linq.Expressions;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// FIX-15 recipient eligibility. Preview and Send share this single predicate so the count an
/// administrator confirms can never drift from the set that is actually delivered to.
/// </summary>
public static class BroadcastRecipientRules
{
    /// <summary>Approved v1 hard cap. A larger audience is blocked, never silently truncated.</summary>
    public const int MaximumRecipients = 5000;

    /// <summary>Insert batch size inside the single send transaction.</summary>
    public const int BatchSize = 500;

    public const string CustomerRole = "Customer";

    /// <summary>
    /// Staff roles. A broadcast aimed at customers must never reach these accounts, even when the
    /// same account also carries the Customer role.
    /// </summary>
    public static readonly string[] StaffRoles = ["Admin", "SuperAdmin", "Support", "KycViewer"];

    /// <summary>
    /// An eligible recipient is a live, usable customer account: it holds the Customer role, holds
    /// no staff role, is not soft-deleted and is Active. Inactive/Suspended/Blocked accounts cannot
    /// use the storefront, so they are not notified.
    /// </summary>
    public static Expression<Func<User, bool>> IsEligibleCustomer => user =>
        !user.IsDeleted &&
        user.Status == (byte)UserStatus.Active &&
        user.Roles.Any(role => role.Name == CustomerRole) &&
        !user.Roles.Any(role => StaffRoles.Contains(role.Name));
}
