using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "KycReview")]
    [Route("api/admin/kyc")]
    public class AdminKycPoliciesController : ControllerBase
    {
        private readonly VitorizeDbContext _db;
        public AdminKycPoliciesController(VitorizeDbContext db) => _db = db;

        [HttpGet("policy-versions")]
        public async Task<ActionResult<ApiResult<List<AdminKycPolicyVersionOptionDto>>>> GetPublishedVersions()
        {
            var versions = (await _db.KycPolicyVersions.AsNoTracking().Include(x => x.KycPolicy)
                .Where(x => x.Status == (byte)KycPolicyVersionStatus.Published && x.KycPolicy.IsActive)
                .OrderBy(x => x.KycPolicy.Name).ThenByDescending(x => x.Version)
                .ToListAsync()).Select(MapVersion).ToList();
            return Ok(ApiResult<List<AdminKycPolicyVersionOptionDto>>.Success(versions));
        }

        [HttpGet("policies")]
        public async Task<ActionResult<ApiResult<List<AdminKycPolicyDto>>>> GetPolicies()
        {
            var policies = (await _db.KycPolicies.AsNoTracking().Include(x => x.Versions)
                .ThenInclude(x => x.DocumentRequirements).ThenInclude(x => x.KycDocumentType)
                .OrderBy(x => x.Name).ToListAsync()).Select(x => new AdminKycPolicyDto
                {
                    Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive,
                    Versions = x.Versions.OrderByDescending(v => v.Version).Select(v => new AdminKycPolicyVersionOptionDto
                    {
                        Id = v.Id, KycPolicyId = x.Id, PolicyCode = x.Code, PolicyName = x.Name,
                        Version = v.Version, Status = v.Status, CustomerTitle = v.CustomerTitle,
                        CustomerInstructions = v.CustomerInstructions,
                        CustomerActionDeadlineHours = v.CustomerActionDeadlineHours,
                        DocumentRequirements = v.DocumentRequirements.OrderBy(r => r.SortOrder).Select(MapRequirement).ToList()
                    }).ToList()
                }).ToList();
            return Ok(ApiResult<List<AdminKycPolicyDto>>.Success(policies));
        }

        [HttpPost("policies")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyDto>>> CreatePolicy(UpsertKycPolicyRequestDto request)
        {
            ValidatePolicy(request);
            ValidateDeadline(request.CustomerActionDeadlineHours);
            var code = request.Code.Trim().ToLowerInvariant();
            if (await _db.KycPolicies.AnyAsync(x => x.Code == code)) throw new BusinessException("کد سیاست تکراری است.");
            var now = DateTime.UtcNow;
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = code, Name = request.Name.Trim(), IsActive = request.IsActive, CreatedAt = now };
            policy.Versions.Add(new KycPolicyVersion { Id = Guid.NewGuid(), Version = 1, Status = (byte)KycPolicyVersionStatus.Draft, CustomerTitle = request.CustomerTitle.Trim(), CustomerInstructions = TrimToNull(request.CustomerInstructions), CustomerActionDeadlineHours = request.CustomerActionDeadlineHours, CreatedAt = now });
            _db.KycPolicies.Add(policy);
            await _db.SaveChangesAsync();
            return Ok(ApiResult<AdminKycPolicyDto>.Success(await ReadPolicy(policy.Id)));
        }

        [HttpPut("policies/{id:guid}")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyDto>>> UpdatePolicy(Guid id, UpsertKycPolicyRequestDto request)
        {
            ValidatePolicy(request);
            var policy = await _db.KycPolicies.FindAsync(id) ?? throw new NotFoundException("سیاست احراز هویت یافت نشد.");
            var code = request.Code.Trim().ToLowerInvariant();
            if (await _db.KycPolicies.AnyAsync(x => x.Code == code && x.Id != id)) throw new BusinessException("کد سیاست تکراری است.");
            policy.Code = code; policy.Name = request.Name.Trim(); policy.IsActive = request.IsActive; policy.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ApiResult<AdminKycPolicyDto>.Success(await ReadPolicy(id)));
        }

        [HttpPost("policies/{id:guid}/versions")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyVersionOptionDto>>> CreateVersion(Guid id, CreateKycPolicyVersionRequestDto request)
        {
            ValidateDeadline(request.CustomerActionDeadlineHours);
            if (string.IsNullOrWhiteSpace(request.CustomerTitle) || request.CustomerTitle.Trim().Length > 250) throw new BusinessException("عنوان مشتری سیاست معتبر نیست.");
            var policy = await _db.KycPolicies.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("سیاست احراز هویت یافت نشد.");
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = id, Version = policy.Versions.DefaultIfEmpty().Max(x => x is null ? 0 : x.Version) + 1, Status = (byte)KycPolicyVersionStatus.Draft, CustomerTitle = request.CustomerTitle.Trim(), CustomerInstructions = TrimToNull(request.CustomerInstructions), CustomerActionDeadlineHours = request.CustomerActionDeadlineHours, CreatedAt = DateTime.UtcNow };
            _db.KycPolicyVersions.Add(version);
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { throw new BusinessException("نسخه جدید هم‌زمان ایجاد شد؛ فهرست را تازه‌سازی کنید."); }
            return Ok(ApiResult<AdminKycPolicyVersionOptionDto>.Success(await ReadVersion(version.Id)));
        }

        [HttpGet("policy-versions/{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyVersionOptionDto>>> GetVersion(Guid id)
            => Ok(ApiResult<AdminKycPolicyVersionOptionDto>.Success(await ReadVersion(id)));

        [HttpPut("policy-versions/{id:guid}")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyVersionOptionDto>>> UpdateVersion(Guid id, UpdateKycPolicyVersionRequestDto request)
        {
            ValidateDeadline(request.CustomerActionDeadlineHours);
            if (string.IsNullOrWhiteSpace(request.CustomerTitle) || request.CustomerTitle.Trim().Length > 250)
                throw new BusinessException("عنوان مشتری سیاست معتبر نیست.");
            var version = await _db.KycPolicyVersions.SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("نسخه سیاست یافت نشد.");
            if (version.Status != (byte)KycPolicyVersionStatus.Draft)
                throw new BusinessException("نسخه منتشرشده تغییرناپذیر است؛ یک نسخه جدید بسازید.");
            version.CustomerTitle = request.CustomerTitle.Trim();
            version.CustomerInstructions = TrimToNull(request.CustomerInstructions);
            version.CustomerActionDeadlineHours = request.CustomerActionDeadlineHours;
            await _db.SaveChangesAsync();
            return Ok(ApiResult<AdminKycPolicyVersionOptionDto>.Success(await ReadVersion(id)));
        }

        [HttpPost("policy-versions/{id:guid}/publish")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycPolicyVersionOptionDto>>> PublishVersion(Guid id)
        {
            var version = await _db.KycPolicyVersions.Include(x => x.KycPolicy).SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("نسخه سیاست یافت نشد.");
            if (!version.KycPolicy.IsActive) throw new BusinessException("ابتدا سیاست را فعال کنید.");
            if (version.Status == (byte)KycPolicyVersionStatus.Published) return Ok(ApiResult<AdminKycPolicyVersionOptionDto>.Success(await ReadVersion(id)));
            version.Status = (byte)KycPolicyVersionStatus.Published; version.PublishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ApiResult<AdminKycPolicyVersionOptionDto>.Success(await ReadVersion(id)));
        }

        [HttpPut("policy-versions/{id:guid}/document-requirements")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult>> SetRequirements(Guid id, SetKycPolicyDocumentRequirementsRequestDto request)
        {
            var version = await _db.KycPolicyVersions.Include(x => x.DocumentRequirements).SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("نسخه سیاست یافت نشد.");
            if (version.Status != (byte)KycPolicyVersionStatus.Draft) throw new BusinessException("نسخه منتشرشده تغییرناپذیر است؛ یک نسخه جدید بسازید.");
            var items = request.Requirements ?? new();
            if (items.Count > 20 || items.Any(x => x.KycDocumentTypeId == Guid.Empty || x.RedactionMode > (byte)KycDocumentRedactionMode.Required || (x.RedactionInstructions?.Trim().Length ?? 0) > 1000) || items.Select(x => x.KycDocumentTypeId).Distinct().Count() != items.Count) throw new BusinessException("فهرست مدارک معتبر نیست.");
            var ids = items.Select(x => x.KycDocumentTypeId).ToList();
            if (await _db.KycDocumentTypes.CountAsync(x => x.IsActive && ids.Contains(x.Id)) != ids.Count) throw new BusinessException("یکی از نوع‌های مدرک فعال نیست.");
            _db.KycPolicyDocumentRequirements.RemoveRange(version.DocumentRequirements);
            _db.KycPolicyDocumentRequirements.AddRange(items.Select(x => new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = id, KycDocumentTypeId = x.KycDocumentTypeId, IsRequired = x.IsRequired, SortOrder = x.SortOrder, Instructions = TrimToNull(x.CustomerInstructions), RedactionMode = x.RedactionMode, RedactionInstructions = TrimToNull(x.RedactionInstructions) }));
            await _db.SaveChangesAsync(); return Ok(ApiResult.Success());
        }

        [HttpGet("document-types")]
        public async Task<ActionResult<ApiResult<List<AdminKycDocumentTypeDto>>>> GetDocumentTypes() => Ok(ApiResult<List<AdminKycDocumentTypeDto>>.Success((await _db.KycDocumentTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync()).Select(MapDocument).ToList()));

        [HttpPost("document-types")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycDocumentTypeDto>>> CreateDocumentType(UpsertKycDocumentTypeRequestDto request)
        {
            ValidateDocument(request); var code = request.Code.Trim().ToLowerInvariant();
            if (await _db.KycDocumentTypes.AnyAsync(x => x.Code == code)) throw new BusinessException("کد نوع مدرک تکراری است.");
            var entity = new KycDocumentType { Id = Guid.NewGuid(), Code = code, Title = request.Title.Trim(), Description = TrimToNull(request.Description), IsActive = request.IsActive, AllowedExtensions = request.AllowedExtensions.Trim().ToLowerInvariant(), MaxFileSizeBytes = request.MaxFileSizeBytes, SortOrder = request.SortOrder, CreatedAt = DateTime.UtcNow };
            _db.KycDocumentTypes.Add(entity); await _db.SaveChangesAsync(); return Ok(ApiResult<AdminKycDocumentTypeDto>.Success(MapDocument(entity)));
        }

        [HttpPut("document-types/{id:guid}")]
        [Authorize(Policy = "KycManage")]
        public async Task<ActionResult<ApiResult<AdminKycDocumentTypeDto>>> UpdateDocumentType(Guid id, UpsertKycDocumentTypeRequestDto request)
        {
            ValidateDocument(request); var entity = await _db.KycDocumentTypes.FindAsync(id) ?? throw new NotFoundException("نوع مدرک یافت نشد."); var code = request.Code.Trim().ToLowerInvariant();
            if (await _db.KycDocumentTypes.AnyAsync(x => x.Code == code && x.Id != id)) throw new BusinessException("کد نوع مدرک تکراری است.");
            entity.Code = code; entity.Title = request.Title.Trim(); entity.Description = TrimToNull(request.Description); entity.IsActive = request.IsActive; entity.AllowedExtensions = request.AllowedExtensions.Trim().ToLowerInvariant(); entity.MaxFileSizeBytes = request.MaxFileSizeBytes; entity.SortOrder = request.SortOrder; entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(); return Ok(ApiResult<AdminKycDocumentTypeDto>.Success(MapDocument(entity)));
        }

        private async Task<AdminKycPolicyDto> ReadPolicy(Guid id)
        {
            var policy = await _db.KycPolicies.AsNoTracking().Include(x => x.Versions)
                .ThenInclude(x => x.DocumentRequirements).ThenInclude(x => x.KycDocumentType).SingleAsync(x => x.Id == id);
            return new AdminKycPolicyDto
            {
                Id = policy.Id, Code = policy.Code, Name = policy.Name, IsActive = policy.IsActive,
                Versions = policy.Versions.OrderByDescending(x => x.Version).Select(x => new AdminKycPolicyVersionOptionDto
                {
                    Id = x.Id, KycPolicyId = policy.Id, PolicyCode = policy.Code, PolicyName = policy.Name,
                    Version = x.Version, Status = x.Status, CustomerTitle = x.CustomerTitle,
                    CustomerInstructions = x.CustomerInstructions,
                    CustomerActionDeadlineHours = x.CustomerActionDeadlineHours,
                    DocumentRequirements = x.DocumentRequirements.OrderBy(r => r.SortOrder).Select(MapRequirement).ToList()
                }).ToList()
            };
        }
        private async Task<AdminKycPolicyVersionOptionDto> ReadVersion(Guid id) => MapVersion(await _db.KycPolicyVersions.AsNoTracking().Include(x => x.KycPolicy).Include(x => x.DocumentRequirements).ThenInclude(x => x.KycDocumentType).SingleAsync(x => x.Id == id));
        private static AdminKycPolicyVersionOptionDto MapVersion(KycPolicyVersion x) => new() { Id = x.Id, KycPolicyId = x.KycPolicyId, PolicyCode = x.KycPolicy.Code, PolicyName = x.KycPolicy.Name, Version = x.Version, Status = x.Status, CustomerTitle = x.CustomerTitle, CustomerInstructions = x.CustomerInstructions, CustomerActionDeadlineHours = x.CustomerActionDeadlineHours, DocumentRequirements = x.DocumentRequirements.OrderBy(r => r.SortOrder).Select(MapRequirement).ToList() };
        private static AdminKycPolicyDocumentRequirementDto MapRequirement(KycPolicyDocumentRequirement x) => new() { KycDocumentTypeId = x.KycDocumentTypeId, DocumentTypeCode = x.KycDocumentType.Code, DocumentTypeTitle = x.KycDocumentType.Title, IsRequired = x.IsRequired, SortOrder = x.SortOrder, CustomerInstructions = x.Instructions, RedactionMode = x.RedactionMode, RedactionInstructions = x.RedactionInstructions };
        private static AdminKycDocumentTypeDto MapDocument(KycDocumentType x) => new() { Id = x.Id, Code = x.Code, Title = x.Title, Description = x.Description, IsActive = x.IsActive, AllowedExtensions = x.AllowedExtensions, MaxFileSizeBytes = x.MaxFileSizeBytes, SortOrder = x.SortOrder };
        private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static void ValidateDeadline(int? hours)
        {
            try
            {
                KycCustomerActionDeadlineRules.EnsureValidDuration(hours);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new BusinessException("Customer-action deadline hours must be greater than zero.");
            }
        }
        private static void ValidatePolicy(UpsertKycPolicyRequestDto r) { if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Trim().Length > 100 || string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 250 || string.IsNullOrWhiteSpace(r.CustomerTitle) || r.CustomerTitle.Trim().Length > 250) throw new BusinessException("اطلاعات سیاست معتبر نیست."); }
        private static void ValidateDocument(UpsertKycDocumentTypeRequestDto r) { if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Trim().Length > 100 || string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 250 || string.IsNullOrWhiteSpace(r.AllowedExtensions) || r.AllowedExtensions.Length > 250 || r.MaxFileSizeBytes <= 0) throw new BusinessException("اطلاعات نوع مدرک معتبر نیست."); }
    }
}
