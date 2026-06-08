namespace Vitorize.Web.Models.Admin.Brands
{
    public class AdminBrandModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; }
    }

    public class AdminBrandInputModel
    {
        public Guid? Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}