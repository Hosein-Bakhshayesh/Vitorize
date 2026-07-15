using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/verification")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly VitorizeDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(
            IVerificationService verificationService,
            ICurrentUserService currentUserService,
            VitorizeDbContext dbContext,
            IWebHostEnvironment environment,
            ILogger<VerificationController> logger)
        {
            _verificationService = verificationService;
            _currentUserService = currentUserService;
            _dbContext = dbContext;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("me")]
        public async Task<ActionResult<ApiResult<VerificationProfileDto?>>> GetMyProfile()
        {
            var result = await _verificationService.GetMyProfileAsync(GetUserId());

            return Ok(ApiResult<VerificationProfileDto?>.Success(
                result,
                "وضعیت احراز هویت دریافت شد."));
        }

        [HttpPost("submit")]
        public async Task<ActionResult<ApiResult<VerificationProfileDto>>> Submit(
            SubmitVerificationRequestDto request)
        {
            var result = await _verificationService.SubmitAsync(
                GetUserId(),
                request);

            return Ok(ApiResult<VerificationProfileDto>.Success(
                result,
                "درخواست احراز هویت ثبت شد."));
        }

        [HttpPost("documents")]
        public async Task<ActionResult<ApiResult<VerificationDocumentDto>>> AddDocument(
            AddVerificationDocumentRequestDto request)
        {
            var result = await _verificationService.AddDocumentAsync(
                GetUserId(),
                request.DocumentType,
                request.FilePath);

            return Ok(ApiResult<VerificationDocumentDto>.Success(
                result,
                "مدرک احراز هویت ثبت شد."));
        }

        [HttpDelete("documents/{documentId:guid}")]
        public async Task<ActionResult<ApiResult>> DeleteDocument(Guid documentId)
        {
            await _verificationService.DeleteDocumentAsync(GetUserId(), documentId);

            return Ok(ApiResult.Success("مدرک احراز هویت حذف شد."));
        }

        [HttpGet("documents/{documentId:guid}/content")]
        public async Task<IActionResult> GetDocumentContent(Guid documentId)
        {
            var userId = GetUserId();
            var document = await _dbContext.VerificationDocuments.AsNoTracking()
                .Where(x => x.Id == documentId)
                .Select(x => new { x.FilePath, OwnerId = x.UserVerificationProfile.UserId })
                .FirstOrDefaultAsync() ?? throw new NotFoundException("مدرک یافت نشد.");
            var canReview = User.HasClaim(Vitorize.Application.Common.AdminPermissions.ClaimType,
                Vitorize.Application.Common.AdminPermissions.KycReview);
            if (document.OwnerId != userId && !canReview)
                throw new NotFoundException("مدرک یافت نشد."); // IDOR-safe response.

            var fullPath = ResolvePrivateDocumentPath(document.FilePath, document.OwnerId);
            if (!System.IO.File.Exists(fullPath))
                throw new NotFoundException("فایل مدرک یافت نشد.");

            _logger.LogInformation(
                "Protected KYC document viewed. UserId={UserId} FileId={FileId} OwnerAccess={OwnerAccess} ReviewAccess={ReviewAccess} EventType={EventType}",
                userId, documentId, document.OwnerId == userId, canReview, OperationalEventNames.KycViewed);
            Response.Headers.CacheControl = "no-store, private";
            Response.Headers["Content-Disposition"] = "inline";
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fullPath, out var contentType))
                contentType = "application/octet-stream";
            return PhysicalFile(fullPath, contentType, enableRangeProcessing: false);
        }

        private string ResolvePrivateDocumentPath(string token, Guid ownerId)
        {
            string root;
            string relative;
            var prefix = $"kyc-private:{ownerId:N}/";
            if (token.StartsWith(prefix, StringComparison.Ordinal))
            {
                root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "private", "verification-documents", ownerId.ToString("N")));
                relative = token[prefix.Length..];
            }
            else if (token.StartsWith("/uploads/verifications/", StringComparison.OrdinalIgnoreCase))
            {
                root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "verifications"));
                relative = Path.GetFileName(token);
            }
            else throw new NotFoundException("فایل مدرک یافت نشد.");
            var resolved = Path.GetFullPath(Path.Combine(root, relative));
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new NotFoundException("فایل مدرک یافت نشد.");
            return resolved;
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}
