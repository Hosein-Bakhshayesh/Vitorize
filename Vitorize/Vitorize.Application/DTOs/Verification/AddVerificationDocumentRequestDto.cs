namespace Vitorize.Application.DTOs.Verification
{
    public class AddVerificationDocumentRequestDto
    {
        public byte DocumentType { get; set; }

        public string FilePath { get; set; } = null!;
    }
}