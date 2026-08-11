namespace Vitorize.Application.DTOs.Admin.Kyc
{
    public class AdminKycPolicyVersionOptionDto
    {
        public Guid Id { get; set; }
        public Guid KycPolicyId { get; set; }
        public string PolicyCode { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public int Version { get; set; }
        public byte Status { get; set; }
        public string CustomerTitle { get; set; } = string.Empty;
        public string? CustomerInstructions { get; set; }
        public List<AdminKycPolicyDocumentRequirementDto> DocumentRequirements { get; set; } = new();
    }

    public class AdminKycPolicyDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<AdminKycPolicyVersionOptionDto> Versions { get; set; } = new();
    }

    public class UpsertKycPolicyRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string CustomerTitle { get; set; } = string.Empty;
        public string? CustomerInstructions { get; set; }
    }

    public class CreateKycPolicyVersionRequestDto
    {
        public string CustomerTitle { get; set; } = string.Empty;
        public string? CustomerInstructions { get; set; }
    }

    public class UpdateKycPolicyVersionRequestDto : CreateKycPolicyVersionRequestDto { }

    public class AdminKycPolicyDocumentRequirementDto
    {
        public Guid KycDocumentTypeId { get; set; }
        public string DocumentTypeCode { get; set; } = string.Empty;
        public string DocumentTypeTitle { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public string? CustomerInstructions { get; set; }
    }

    public class AdminKycDocumentTypeDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string AllowedExtensions { get; set; } = string.Empty;
        public long MaxFileSizeBytes { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpsertKycDocumentTypeRequestDto : AdminKycDocumentTypeDto { }

    public class SetKycPolicyDocumentRequirementsRequestDto
    {
        public List<KycPolicyDocumentRequirementRequestDto> Requirements { get; set; } = new();
    }

    public class KycPolicyDocumentRequirementRequestDto
    {
        public Guid KycDocumentTypeId { get; set; }
        public bool IsRequired { get; set; } = true;
        public int SortOrder { get; set; }
        public string? CustomerInstructions { get; set; }
    }
}
