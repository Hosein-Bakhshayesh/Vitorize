namespace Vitorize.Application.DTOs.Storefront
{
    public class FaqDto
    {
        public Guid Id { get; set; }

        public string Question { get; set; } = null!;

        public string Answer { get; set; } = null!;

        public int SortOrder { get; set; }
    }
}