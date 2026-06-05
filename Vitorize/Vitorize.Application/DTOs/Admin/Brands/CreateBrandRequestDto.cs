namespace Vitorize.Application.DTOs.Admin.Brands
{
    public class CreateBrandRequestDto
    {
        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}