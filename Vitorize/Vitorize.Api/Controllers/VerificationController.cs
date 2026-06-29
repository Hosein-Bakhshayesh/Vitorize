using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/verification")]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ICurrentUserService _currentUserService;

        public VerificationController(
            IVerificationService verificationService,
            ICurrentUserService currentUserService)
        {
            _verificationService = verificationService;
            _currentUserService = currentUserService;
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

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}