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
        private readonly IAdminSmsManagementService _management;
        private readonly ISmsHistoryService _history;

        public AdminSmsController(
            ISmsService smsService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUserService,
            IAdminSmsManagementService management,
            ISmsHistoryService history)
        {
            _smsService = smsService;
            _securityLogService = securityLogService;
            _currentUserService = currentUserService;
            _management = management;
            _history = history;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<Vitorize.Shared.Common.PagedResult<SmsHistoryItemDto>>>> GetHistory(
            [FromQuery] SmsHistoryFilterDto filter,
            CancellationToken cancellationToken)
        {
            var allowFull = User.IsInRole("SuperAdmin") &&
                            Request.Query["showFullMobile"] == "true" &&
                            await _management.CanViewFullMobileAsync(cancellationToken);
            var result = await _management.GetHistoryAsync(filter, allowFull, cancellationToken);
            return Ok(ApiResult<Vitorize.Shared.Common.PagedResult<SmsHistoryItemDto>>.Success(result, "تاریخچه پیامک دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<SmsHistoryItemDto>>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var allowFull = User.IsInRole("SuperAdmin") &&
                            Request.Query["showFullMobile"] == "true" &&
                            await _management.CanViewFullMobileAsync(cancellationToken);
            var result = await _management.GetByIdAsync(id, allowFull, cancellationToken);
            await _securityLogService.LogAsync(_currentUserService.UserId, "SMS_HISTORY_VIEW", true, $"SmsMessage={id:N}");
            return Ok(ApiResult<SmsHistoryItemDto>.Success(result, "جزئیات پیامک دریافت شد."));
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResult<SmsSummaryDto>>> Summary(CancellationToken cancellationToken) =>
            Ok(ApiResult<SmsSummaryDto>.Success(await _management.GetSummaryAsync(cancellationToken), "خلاصه پیامک دریافت شد."));

        [HttpGet("health")]
        public async Task<ActionResult<ApiResult<SmsHealthDto>>> Health(CancellationToken cancellationToken) =>
            Ok(ApiResult<SmsHealthDto>.Success(await _management.GetHealthAsync(cancellationToken), "وضعیت سرویس پیامک دریافت شد."));

        [EnableRateLimiting("otp")]
        [HttpPost("send-notification")]
        public async Task<ActionResult<ApiResult<SmsActionResultDto>>> SendNotification(
            SendCustomNotificationRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _management.SendNotificationAsync(request, GetAdminId(), cancellationToken);
            return Ok(ApiResult<SmsActionResultDto>.Success(result, result.Message));
        }

        [EnableRateLimiting("otp")]
        [HttpPost("send-text")]
        public async Task<ActionResult<ApiResult<SmsActionResultDto>>> SendText(
            SendCustomTextRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _management.SendTextAsync(request, GetAdminId(), cancellationToken);
            return Ok(ApiResult<SmsActionResultDto>.Success(result, result.Message));
        }

        [HttpPost("{id:guid}/retry")]
        public async Task<ActionResult<ApiResult>> Retry(Guid id, CancellationToken cancellationToken)
        {
            await _management.RetryAsync(id, GetAdminId(), cancellationToken);
            return Ok(ApiResult.Success("پیامک برای بازتلاش در صف قرار گرفت."));
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<ActionResult<ApiResult>> Cancel(Guid id, CancellationToken cancellationToken)
        {
            await _management.CancelAsync(id, GetAdminId(), cancellationToken);
            return Ok(ApiResult.Success("ارسال پیامک لغو شد."));
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] SmsHistoryFilterDto filter, CancellationToken cancellationToken)
        {
            var csv = await _management.ExportCsvAsync(filter, cancellationToken);
            await _securityLogService.LogAsync(
                _currentUserService.UserId, "SMS_HISTORY_EXPORT", true, "Masked CSV export");
            return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
                "text/csv; charset=utf-8", $"sms-history-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
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
                var parameters = (request.Parameters ?? new List<TestSmsParameterDto>())
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

            var templateId = string.IsNullOrWhiteSpace(request.TemplateKey)
                ? null
                : await _smsService.GetTemplateIdAsync(request.TemplateKey!, cancellationToken);
            var isOtp = !string.IsNullOrWhiteSpace(request.TemplateKey) && SmsTemplateKeys.IsOtp(request.TemplateKey!);
            var reference = request.Parameters?.FirstOrDefault(x => x.Name == SmsTemplateParams.OrderNumber)?.Value;
            await _history.RecordDirectResultAsync(new SmsHistoryRecordRequest
            {
                Mobile = mobile,
                Purpose = "AdminDiagnosticTest",
                SendType = string.IsNullOrWhiteSpace(request.TemplateKey)
                    ? (byte)SmsSendType.CustomText
                    : isOtp ? (byte)SmsSendType.OtpTemplate : (byte)SmsSendType.NotificationTemplate,
                TemplateKey = request.TemplateKey,
                TemplateId = templateId,
                PublicReference = reference,
                SafeMessagePreview = isOtp
                    ? "آزمایش قالب OTP؛ کد ذخیره نشده است"
                    : string.IsNullOrWhiteSpace(request.TemplateKey) ? request.Text : $"آزمایش اعلان با کد {reference}",
                CreatedByUserId = _currentUserService.UserId,
                RelatedEntityType = "Diagnostic",
                IdempotencyKey = $"sms:test:{Guid.NewGuid():N}",
                MaxRetryCount = 1
            }, result, cancellationToken);

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

        private Guid GetAdminId() => _currentUserService.UserId
            ?? throw new UnauthorizedException("مدیر احراز هویت نشده است.");

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
