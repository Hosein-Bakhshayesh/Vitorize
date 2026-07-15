using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vitorize.Shared.Logging;

public static partial class CorrelationIdPolicy
{
    public const string HeaderName = "X-Correlation-ID";
    public const int MaximumLength = 64;

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumLength &&
        value == value.Trim() &&
        AllowedCharacters().IsMatch(value);

    public static string Resolve(string? incoming) =>
        IsValid(incoming) ? incoming! : Generate();

    public static string Generate() => ActivityTraceId.CreateRandom().ToHexString();

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharacters();
}

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current
    {
        get => CurrentValue.Value;
        set => CurrentValue.Value = value;
    }
}
