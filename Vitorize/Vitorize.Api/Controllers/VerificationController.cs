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
            var userId = GetUserId();

            var result = await _verificationService.GetMyProfileAsync(userId);

            return Ok(ApiResult<VerificationProfileDto?>.Success(
                result,
                "وضعیت احراز هویت با موفقیت دریافت شد."));
        }

        [HttpPost("submit")]
        public async Task<ActionResult<ApiResult<VerificationProfileDto>>> Submit(
            SubmitVerificationRequestDto request)
        {
            var userId = GetUserId();

            var result = await _verificationService.SubmitAsync(userId, request);

            return Ok(ApiResult<VerificationProfileDto>.Success(
                result,
                "درخواست احراز هویت با موفقیت ثبت شد."));
        }

        [HttpPost("documents")]
        public async Task<ActionResult<ApiResult<VerificationDocumentDto>>> AddDocument(
            [FromQuery] byte documentType,
            [FromQuery] string filePath)
        {
            var userId = GetUserId();

            var result = await _verificationService.AddDocumentAsync(
                userId,
                documentType,
                filePath);

            return Ok(ApiResult<VerificationDocumentDto>.Success(
                result,
                "مدرک احراز هویت با موفقیت ثبت شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}