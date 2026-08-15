namespace Vitorize.Application.Common;

/// <summary>Authoritative, timezone-free bounds for identity birth dates.</summary>
public static class VerificationBirthDateRules
{
    public static readonly DateOnly Minimum = new(1900, 1, 1);

    public static bool IsWithinRange(DateOnly value, DateOnly today) =>
        value >= Minimum && value <= today;
}
