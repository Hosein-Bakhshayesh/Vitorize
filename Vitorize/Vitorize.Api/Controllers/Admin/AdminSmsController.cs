using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    /// <summary>
    /// APIهای مدیریت پیامک (ادمین‌محور): بررسی سلامت/اعتبار حساب و ارسال پیامک آزمایشی.
    /// این کنترلر هرگز مقدار محرمانه (کلید API) را برنمی‌گرداند.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/sms")]
    public class AdminSmsController : ControllerBase
    {
        private readonly ISmsService _smsService;
        private readonly ISecurityLogService _securityLogService;
        private readonly ICurrentUserService _currentUserService;

        public AdminSmsController(
            ISmsService smsService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUserService)
        {
            _smsService = smsService;
            _securityLogService = securityLogService;
            _currentUserService = currentUserService;
        }

        [HttpGet("status")]
        public async Task<ActionResult<ApiResult<SmsAccountStatusDto>>> Status(
            CancellationToken cancellationToken)
        {
            var (isValid, message) = await _smsService.ValidateConfigurationAsync(cancellationToken);
            var isEnabled = await _smsService.IsEnabledAsync(cancellationToken);
            var account = await _smsService.GetAccountStatusAsync(cancellationToken);

            var dto = new SmsAccountStatusDto
            {
                IsConfigured = isValid,
                IsEnabled = isEnabled,
                ConnectionOk = account.IsSuccess,
                Credit = account.Credit,
                Lines = account.Lines?.ToList() ?? new List<long>(),
                Message = account.IsSuccess
                    ? "اتصال به SMS.ir برقرار است."
                    : (account.UserMessage ?? message)
            };

            return Ok(ApiResult<SmsAccountStatusDto>.Success(dto, "وضعیت پیامک دریافت شد."));
        }

        [EnableRateLimiting("otp")]
        [HttpPost("test")]
        public async Task<ActionResult<ApiResult<SmsTestResultDto>>> SendTest(
            SendTestSmsRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!IranMobile.TryNormalize(request.Mobile, out var mobile))
                throw new BusinessException("شماره موبایل معتبر نیست.");

            SmsSendResult result;

            if (!string.IsNullOrWhiteSpace(request.TemplateKey))
            {
                var parameters = request.Parameters
                    .Select(p => new SmsTemplateParameter(p.Name, p.Value))
                    .ToList();

                result = await _smsService.SendTemplateAsync(
                    mobile, request.TemplateKey!, parameters, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(request.Text))
            {
                result = await _smsService.SendTextAsync(mobile, request.Text!, cancellationToken);
            }
            else
            {
                throw new BusinessException("قالب یا متن پیامک را مشخص کنید.");
            }

            await _securityLogService.LogAsync(
                _currentUserService.UserId,
                "TEST_SMS_SENT",
                result.IsSuccess,
                $"Test SMS to {IranMobile.Mask(mobile)} ({(result.IsSuccess ? "sent" : result.FailureReason.ToString())})",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());

            var dto = new SmsTestResultDto
            {
                Success = result.IsSuccess,
                MessageId = result.ProviderMessageId,
                Cost = result.Cost,
                FailureReason = result.IsSuccess ? null : result.FailureReason.ToString(),
                Message = result.IsSuccess
                    ? "پیامک آزمایشی با موفقیت ارسال شد."
                    : FriendlyMessage(result.FailureReason)
            };

            return Ok(ApiResult<SmsTestResultDto>.Success(dto, dto.Message));
        }

        private static string FriendlyMessage(SmsFailureReason reason) => reason switch
        {
            SmsFailureReason.Disabled => "سرویس پیامک غیرفعال است.",
            SmsFailureReason.NotConfigured => "کلید API تنظیم نشده است.",
            SmsFailureReason.InvalidMobile => "شماره موبایل معتبر نیست.",
            SmsFailureReason.InvalidTemplate => "شناسه قالب تنظیم نشده یا نامعتبر است.",
            SmsFailureReason.InvalidParameter => "پارامترهای قالب نامعتبرند.",
            SmsFailureReason.InvalidLineNumber => "شماره خط اختصاصی تنظیم نشده یا نامعتبر است.",
            SmsFailureReason.InsufficientCredit => "اعتبار حساب پیامک کافی نیست.",
            SmsFailureReason.Unauthorized => "کلید API نامعتبر است.",
            SmsFailureReason.AccessDenied => "دسترسی به سرویس پیامک مجاز نیست.",
            SmsFailureReason.TooManyRequests => "تعداد درخواست‌ها بیش از حد مجاز است.",
            SmsFailureReason.Timeout or SmsFailureReason.Network => "ارتباط با سرویس پیامک برقرار نشد.",
            SmsFailureReason.ProviderUnavailable => "سرویس پیامک در دسترس نیست.",
            _ => "ارسال پیامک آزمایشی ناموفق بود."
        };
    }
}
