using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Verification
{
    public class VerificationProfileModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string NationalCode { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string? BankCardNumber { get; set; }
        public string? ShabaNumber { get; set; }
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public List<VerificationDocumentModel> Documents { get; set; } = new();
        public List<VerificationDocumentModel> VerificationDocuments { get; set; } = new();
    }

    public class VerificationDocumentModel
    {
        public Guid Id { get; set; }
        public Guid UserVerificationProfileId { get; set; }
        public byte DocumentType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class ReviewVerificationRequestModel
    {
        public bool Approve { get; set; }
        [MaxLength(1000)] public string? AdminNote { get; set; }
    }
}
