using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class VerificationDocument
{
    public Guid Id { get; set; }

    public Guid UserVerificationProfileId { get; set; }

    public byte DocumentType { get; set; }

    /// <summary>
    /// Explicit Phase-1 policy document type. Legacy byte-only documents remain
    /// intentionally unmapped rather than being guessed into a policy type.
    /// </summary>
    public Guid? KycDocumentTypeId { get; set; }

    public string FilePath { get; set; } = null!;

    public byte Status { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public virtual User? ReviewedByAdmin { get; set; }

    public virtual UserVerificationProfile UserVerificationProfile { get; set; } = null!;

    public virtual KycDocumentType? KycDocumentType { get; set; }
}
