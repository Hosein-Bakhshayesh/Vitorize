namespace Vitorize.Shared.Exceptions;

/// <summary>Safe client-facing result for a conflicting concurrent command.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
