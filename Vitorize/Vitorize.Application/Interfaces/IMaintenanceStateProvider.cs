namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// Whether the shop is in maintenance mode, cached briefly so asking does not cost a query on
    /// every request.
    ///
    /// The flag lives in the generic Settings table, which every other consumer reads straight from
    /// the database. That is fine for a page that reads it once; it is not fine for a check that has
    /// to run in front of every API call. The cache is invalidated when the key is saved, so an
    /// administrator switching maintenance on or off sees it take effect immediately rather than
    /// waiting for an expiry — the same arrangement the SMS settings already use.
    /// </summary>
    public interface IMaintenanceStateProvider
    {
        Task<bool> IsMaintenanceModeAsync(CancellationToken cancellationToken = default);

        /// <summary>Called when the setting changes so the next request re-reads it.</summary>
        void Invalidate();
    }
}
