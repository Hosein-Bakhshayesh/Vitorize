namespace Vitorize.Application.DTOs.Verification
{
    public class SubmitVerificationRequestDto
    {
        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string NationalCode { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

        public string? BankCardNumber { get; set; }

        public string? ShabaNumber { get; set; }

        public string? Address { get; set; }

        public string? PostalCode { get; set; }
    }
}