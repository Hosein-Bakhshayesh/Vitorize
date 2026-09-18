namespace Vitorize.Shared.Storefront
{
    /// <summary>
    /// Where on the homepage a slide is drawn. Both blocks are the same carousel with the same
    /// fields; only the frame around them differs, which is why they share one entity instead of
    /// the middle one living on <c>Banners</c> as an image with no room for words.
    /// </summary>
    public static class HomeSlidePlacements
    {
        /// <summary>The wide 16:9 band between the product grid and the benefits row.</summary>
        public const string Middle = "home-middle";

        /// <summary>The very wide 3.5:1 band near the foot of the page.</summary>
        public const string Bottom = "home-bottom";

        public static readonly string[] All = { Middle, Bottom };

        public static bool IsKnown(string? value) =>
            value is not null && Array.IndexOf(All, value) >= 0;

        /// <summary>
        /// The foot slideshow existed first and every slide written before placements belongs to
        /// it, so an unrecognised or missing value resolves there rather than vanishing.
        /// </summary>
        public static string Normalize(string? value) =>
            IsKnown(value) ? value! : Bottom;

        public static string Label(string? value) =>
            Normalize(value) == Middle ? "بنر میانی صفحه" : "اسلایدشوی پایین صفحه";
    }
}
