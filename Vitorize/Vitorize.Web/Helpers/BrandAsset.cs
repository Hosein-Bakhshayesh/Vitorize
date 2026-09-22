namespace Vitorize.Web.Helpers
{
    /// <summary>
    /// A brand image can come from two different places. One an administrator uploaded lives on the
    /// media host and has to be resolved against it; the ones this application ships with are files
    /// in its own wwwroot, and resolving those against the media host would point at a host that has
    /// never heard of them. Both are stored in the same settings row, so every place that renders one
    /// asks here which kind it is holding.
    /// </summary>
    public static class BrandAsset
    {
        private const string PackagedFolder = "images/";

        public static bool IsPackaged(string? path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.Trim().TrimStart('~', '/').StartsWith(PackagedFolder, StringComparison.OrdinalIgnoreCase);
    }
}
