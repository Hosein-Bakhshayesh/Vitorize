namespace Vitorize.Web.Models.Admin.Products
{
    public class ProductLookupModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }
}