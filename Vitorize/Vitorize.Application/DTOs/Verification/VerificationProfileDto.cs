namespace Vitorize.Application.DTOs.Verification
{
    public class VerificationProfileDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        /// <summary>Registration profile name, kept distinct from the submitted KYC identity.</summary>
        public string UserFullName { get; set; } = string.Empty;

        /// <summary>Registration profile mobile, kept distinct from the submitted KYC identity.</summary>
        public string UserMobile { get; set; } = string.Empty;

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string NationalCode { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

        public bool? RegisteredMobileBelongsToCardHolder { get; set; }

        public string? CardHolderMobile { get; set; }

        public string? BankCardNumber { get; set; }

        public string? ShabaNumber { get; set; }

        public string? Address { get; set; }

        public string? PostalCode { get; set; }

        public byte Status { get; set; }

        public string? AdminNote { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public List<VerificationDocumentDto> Documents { get; set; } = new();
    }
}
