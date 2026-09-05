namespace Vitorize.Application.DTOs.Verification
{
    public class VerificationDocumentDto
    {
        public Guid Id { get; set; }

        public byte DocumentType { get; set; }

        /// <summary>Versioned policy document type, when this upload belongs to a purchased KYC policy.</summary>
        public Guid? KycDocumentTypeId { get; set; }

        /// <summary>Current title of the configured KYC document type.</summary>
        public string? DocumentTypeTitle { get; set; }

        public string FilePath { get; set; } = null!;

        public byte Status { get; set; }

        public string? AdminNote { get; set; }
    }
}
