namespace Vitorize.Application.DTOs.Verification
{
    public class VerificationProfileDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string NationalCode { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

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