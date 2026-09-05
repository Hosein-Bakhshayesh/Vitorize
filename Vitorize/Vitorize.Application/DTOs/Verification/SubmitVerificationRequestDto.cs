namespace Vitorize.Application.DTOs.Verification
{
    public class SubmitVerificationRequestDto
    {
        /// <summary>
        /// Private-upload tokens chosen by the customer.  They are promoted to
        /// verification documents only as part of the final submission, so a
        /// partially completed form never becomes an admin-reviewable profile.
        /// </summary>
        public List<SubmitVerificationDocumentDto> Documents { get; set; } = new();

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string NationalCode { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

        /// <summary>Whether the mobile used to register the account belongs to the bank-card holder.</summary>
        public bool? RegisteredMobileBelongsToCardHolder { get; set; }

        /// <summary>Required only when the registered account mobile belongs to somebody else.</summary>
        public string? CardHolderMobile { get; set; }

        public string? BankCardNumber { get; set; }

        public string? ShabaNumber { get; set; }

        public string? Address { get; set; }

        public string? PostalCode { get; set; }
    }

    public class SubmitVerificationDocumentDto
    {
        public byte DocumentType { get; set; }
        public Guid? KycDocumentTypeId { get; set; }
        public Guid? OrderItemId { get; set; }
        public bool IsRedacted { get; set; }
        public string FilePath { get; set; } = null!;
    }
}
