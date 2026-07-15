using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.GiftCodes;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "OrderFulfillment")]
    [Route("api/admin/giftcodes")]
    public class AdminGiftCodesController : ControllerBase
    {
        private readonly IAdminGiftCodeService _giftCodeService;

        public AdminGiftCodesController(IAdminGiftCodeService giftCodeService)
        {
            _giftCodeService = giftCodeService;
        }

        [HttpPost("import")]
        public async Task<ActionResult<ApiResult<GiftCodeBatchDto>>> Import(
            GiftCodeImportDto request)
        {
            var result = await _giftCodeService.ImportAsync(request);

            return Ok(ApiResult<GiftCodeBatchDto>.Success(
                result,
                "کدها با موفقیت وارد شدند."));
        }

        [HttpGet("batches")]
        public async Task<ActionResult<ApiResult<List<GiftCodeBatchDto>>>> GetBatches()
        {
            var result = await _giftCodeService.GetBatchesAsync();

            return Ok(ApiResult<List<GiftCodeBatchDto>>.Success(
                result,
                "لیست بچ‌های کد با موفقیت دریافت شد."));
        }

        [HttpGet("batches/{batchId:guid}")]
        public async Task<ActionResult<ApiResult<GiftCodeBatchDto>>> GetBatch(Guid batchId)
        {
            var result = await _giftCodeService.GetBatchByIdAsync(batchId);

            return Ok(ApiResult<GiftCodeBatchDto>.Success(
                result,
                "جزئیات بچ کد با موفقیت دریافت شد."));
        }

        [HttpGet("codes")]
        public async Task<ActionResult<ApiResult<PagedResult<AdminGiftCodeDto>>>> GetCodes(
            [FromQuery] AdminGiftCodeFilterDto filter)
        {
            var result = await _giftCodeService.GetGiftCodesAsync(filter);

            return Ok(ApiResult<PagedResult<AdminGiftCodeDto>>.Success(
                result,
                "لیست کدها با موفقیت دریافت شد."));
        }

        [HttpDelete("batches/{batchId:guid}")]
        public async Task<ActionResult<ApiResult>> DeleteBatch(Guid batchId)
        {
            await _giftCodeService.DeleteBatchAsync(batchId);

            return Ok(ApiResult.Success("بچ کدها با موفقیت حذف شد."));
        }
    }
}
