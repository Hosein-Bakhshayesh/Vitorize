using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Reports;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/reports")]
    public class AdminReportsController : ControllerBase
    {
        private readonly IAdminReportService _reportService;

        public AdminReportsController(IAdminReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("sales")]
        public async Task<ActionResult<ApiResult<SalesReportDto>>> Sales(
            [FromQuery] ReportDateRangeDto filter)
        {
            var result = await _reportService.GetSalesReportAsync(filter);

            return Ok(ApiResult<SalesReportDto>.Success(
                result,
                "گزارش فروش با موفقیت دریافت شد."));
        }

        [HttpGet("payments")]
        public async Task<ActionResult<ApiResult<PaymentsReportDto>>> Payments(
            [FromQuery] ReportDateRangeDto filter)
        {
            var result = await _reportService.GetPaymentsReportAsync(filter);

            return Ok(ApiResult<PaymentsReportDto>.Success(
                result,
                "گزارش پرداخت‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("wallet")]
        public async Task<ActionResult<ApiResult<WalletReportDto>>> Wallet(
            [FromQuery] ReportDateRangeDto filter)
        {
            var result = await _reportService.GetWalletReportAsync(filter);

            return Ok(ApiResult<WalletReportDto>.Success(
                result,
                "گزارش کیف پول با موفقیت دریافت شد."));
        }

        [HttpGet("coupons")]
        public async Task<ActionResult<ApiResult<CouponsReportDto>>> Coupons(
            [FromQuery] ReportDateRangeDto filter)
        {
            var result = await _reportService.GetCouponsReportAsync(filter);

            return Ok(ApiResult<CouponsReportDto>.Success(
                result,
                "گزارش کدهای تخفیف با موفقیت دریافت شد."));
        }

        [HttpGet("giftcodes")]
        public async Task<ActionResult<ApiResult<GiftCodesReportDto>>> GiftCodes()
        {
            var result = await _reportService.GetGiftCodesReportAsync();

            return Ok(ApiResult<GiftCodesReportDto>.Success(
                result,
                "گزارش کدهای گیفت کارت با موفقیت دریافت شد."));
        }

        [HttpGet("users")]
        public async Task<ActionResult<ApiResult<UsersReportDto>>> Users(
            [FromQuery] ReportDateRangeDto filter)
        {
            var result = await _reportService.GetUsersReportAsync(filter);

            return Ok(ApiResult<UsersReportDto>.Success(
                result,
                "گزارش کاربران با موفقیت دریافت شد."));
        }
    }
}