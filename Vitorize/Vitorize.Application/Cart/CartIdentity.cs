namespace Vitorize.Application.Cart;

/// <summary>Server-resolved cart ownership. Callers never choose a CartId or UserId.</summary>
public readonly record struct CartIdentity(Guid? UserId, string? GuestTokenHash)
{
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsGuest => !IsAuthenticated && !string.IsNullOrWhiteSpace(GuestTokenHash);

    public static CartIdentity ForUser(Guid userId) =>
        userId == Guid.Empty ? throw new ArgumentOutOfRangeException(nameof(userId)) : new(userId, null);

    public static CartIdentity ForGuest(string guestTokenHash) =>
        string.IsNullOrWhiteSpace(guestTokenHash) ? throw new ArgumentOutOfRangeException(nameof(guestTokenHash)) : new(null, guestTokenHash);
}
