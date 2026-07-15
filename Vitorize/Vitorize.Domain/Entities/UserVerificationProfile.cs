using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class UserVerificationProfile
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

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? EncryptedPayload { get; set; }

    public short? EncryptionVersion { get; set; }

    public virtual User? ReviewedByAdmin { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<VerificationDocument> VerificationDocuments { get; set; } = new List<VerificationDocument>();
}
