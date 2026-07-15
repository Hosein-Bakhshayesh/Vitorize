using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components;
using Vitorize.Shared.Common;

namespace Vitorize.Web.Services;

public sealed class PrerenderApiState : IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly Dictionary<string, object> _pending = new(StringComparer.Ordinal);
    private readonly PersistingComponentStateSubscription? _subscription;

    public PrerenderApiState(PersistentComponentState state)
    {
        _state = state;
        try { _subscription = state.RegisterOnPersisting(PersistAsync); }
        catch (InvalidOperationException) { _subscription = null; }
    }

    public bool TryTake<T>(string url, out ApiResult<T>? value)
    {
        try { return _state.TryTakeFromJson(Key<T>(url), out value); }
        catch (InvalidOperationException) { value = null; return false; }
    }

    public void Remember<T>(string url, ApiResult<T> value) => _pending[Key<T>(url)] = value;

    private Task PersistAsync()
    {
        foreach (var entry in _pending)
            _state.PersistAsJson(entry.Key, entry.Value);
        return Task.CompletedTask;
    }

    private static string Key<T>(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{typeof(T).FullName}|{url}"));
        return "api_" + Convert.ToHexString(bytes);
    }

    public void Dispose() => _subscription?.Dispose();
}
