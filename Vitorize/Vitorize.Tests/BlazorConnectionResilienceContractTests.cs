using Xunit;

namespace Vitorize.Tests;

public sealed class BlazorConnectionResilienceContractTests
{
    [Fact]
    public void Vpn_handoff_retries_silently_and_recovers_the_page_when_the_circuit_is_gone()
    {
        var root = FindSolutionRoot();
        var app = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "App.razor"));
        var reconnect = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "js", "blazor-reconnect.js"));

        Assert.Contains("maxRetries: 900", app, StringComparison.Ordinal);
        Assert.DoesNotContain("یک خطای پیش‌بینی‌نشده رخ داد", app, StringComparison.Ordinal);
        Assert.Contains("DelayedNoticeMs = 8000", reconnect, StringComparison.Ordinal);
        Assert.DoesNotContain("در حال برقراری دوباره ارتباط", reconnect, StringComparison.Ordinal);
        Assert.Contains("scheduleRetry(OfflineRetryDelayMs)", reconnect, StringComparison.Ordinal);
        Assert.Contains("if (await Blazor.reconnect())", reconnect, StringComparison.Ordinal);
        Assert.Contains("window.location.reload();", reconnect, StringComparison.Ordinal);
        Assert.Contains("#blazor-error-ui .dismiss", reconnect, StringComparison.Ordinal);
        Assert.DoesNotContain("sans-serif\",", reconnect, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vitorize.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Vitorize solution root.");
    }
}
