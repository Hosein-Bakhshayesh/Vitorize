namespace Vitorize.Api.Services;

/// <summary>Ensures the Torob request includes its required headers.</summary>
public interface ITorobRequestAuthenticator
{
    bool TryValidate(HttpRequest request, out string error);
}

public sealed class TorobRequestAuthenticator : ITorobRequestAuthenticator
{
    public bool TryValidate(HttpRequest request, out string error)
    {
        error = "";
        if (!request.Headers.TryGetValue("C-Torob-Token-Version", out var versionHeader) ||
            string.IsNullOrWhiteSpace(versionHeader.ToString()))
        {
            error = "نسخه توکن ترب ارسال نشده است.";
            return false;
        }

        if (!request.Headers.TryGetValue("X-Torob-Token", out var tokenHeader) ||
            string.IsNullOrWhiteSpace(tokenHeader.ToString()))
        {
            error = "توکن ترب ارسال نشده است.";
            return false;
        }

        return true;
    }
}
