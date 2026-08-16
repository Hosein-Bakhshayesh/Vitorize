namespace Vitorize.Shared.Enums
{
    /// <summary>Audiences supported by FIX-15 broadcast announcements.</summary>
    public enum BroadcastAudience : byte
    {
        AllCustomers = 1,
        SelectedCustomers = 2
    }

    /// <summary>
    /// Lifecycle of a broadcast. There is no draft or scheduling: a send is atomic, so a broadcast
    /// is only ever persisted as Sent, or rolled back entirely.
    /// </summary>
    public enum BroadcastStatus : byte
    {
        Sending = 1,
        Sent = 2,
        Failed = 3
    }
}
