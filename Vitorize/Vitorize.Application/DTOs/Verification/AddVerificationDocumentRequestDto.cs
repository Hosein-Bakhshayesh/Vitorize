namespace Vitorize.Application.DTOs.Verification
{
    public class AddVerificationDocumentRequestDto
    {
        public byte DocumentType { get; set; }

        /// <summary>Optional explicit type used by versioned KYC policies.</summary>
        public Guid? KycDocumentTypeId { get; set; }

        /// <summary>
        /// The paid order item whose immutable KYC policy owns this document
        /// slot. Required whenever <see cref="KycDocumentTypeId"/> is supplied.
        /// </summary>
        public Guid? OrderItemId { get; set; }

        /// <summary>True only when the browser uploaded a newly flattened redaction output.</summary>
        public bool IsRedacted { get; set; }

        public string FilePath { get; set; } = null!;
    }
}
