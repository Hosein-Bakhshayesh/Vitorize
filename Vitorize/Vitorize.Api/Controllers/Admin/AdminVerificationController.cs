using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/verifications")]
    public class AdminVerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;
        private readonly ICurrentUserService _currentUserService;

        public AdminVerificationController(
            IVerificationService verificationService,
            ICurrentUserService currentUserService)
        {
            _verificationService = verificationService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<VerificationProfileDto>>>> GetAll()
        {
            var result = await _verificationService.GetAllAsync();

            return Ok(ApiResult<List<VerificationProfileDto>>.Success(
                result,
                "لیست درخواست‌های احراز هویت دریافت شد."));
        }

        [HttpGet("{profileId:guid}")]
        public async Task<ActionResult<ApiResult<VerificationProfileDto>>> GetById(
            Guid profileId)
        {
            var result = await _verificationService.GetByIdAsync(profileId);

            return Ok(ApiResult<VerificationProfileDto>.Success(
                result,
                "جزئیات احراز هویت دریافت شد."));
        }

        [HttpPost("{profileId:guid}/review")]
        public async Task<ActionResult<ApiResult<VerificationProfileDto>>> Review(
            Guid profileId,
            ReviewVerificationRequestDto request)
        {
            var adminUserId = GetUserId();

            var result = await _verificationService.ReviewAsync(
                profileId,
                adminUserId,
                request);

            return Ok(ApiResult<VerificationProfileDto>.Success(
                result,
                request.Approve
                    ? "احراز هویت با موفقیت تایید شد."
                    : "احراز هویت رد شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}