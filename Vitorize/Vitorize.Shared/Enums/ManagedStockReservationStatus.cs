namespace Vitorize.Shared.Enums;

/// <summary>Lifecycle for a counted-inventory hold created before payment.</summary>
public enum ManagedStockReservationStatus : byte
{
    Active = 1,
    Consumed = 2,
    Released = 3,
    Expired = 4
}
