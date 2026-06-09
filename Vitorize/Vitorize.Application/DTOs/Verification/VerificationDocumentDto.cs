namespace Vitorize.Application.DTOs.Verification
{
    public class VerificationDocumentDto
    {
        public Guid Id { get; set; }

        public byte DocumentType { get; set; }

        public string FilePath { get; set; } = null!;

        public byte Status { get; set; }

        public string? AdminNote { get; set; }
    }
}