-- ============================================================================
-- Vitorize — SMS (SMS.ir) settings seed  (2026-07-13)
-- NO SCHEMA CHANGES. Data-only. Idempotent — safe to run repeatedly.
--
-- Provisions every Sms.* Setting key used by the SMS subsystem. EXISTING ROWS
-- ARE NEVER TOUCHED (production values, including the API key, are preserved).
--
-- This mirrors VitorizeSeedService.SeedSettingsAsync, which also runs at API
-- startup. Applying this script is OPTIONAL — it only lets you provision the
-- keys without an app restart.
--
-- SECURITY: The "SMS" group is NOT part of the public settings endpoint, and
-- Sms.ApiKey / Sms.DefaultLineNumber are additionally masked by the admin API.
-- Seeded secret values are EMPTY; set them from the Admin › Settings › SMS panel.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Optional deployment inputs. Leave blank and enter the two IDs in Admin,
-- or set them before running this script to seed/synchronize all compatibility keys.
DECLARE @UniversalOtpTemplateId nvarchar(50) = N'';
DECLARE @UniversalNotificationTemplateId nvarchar(50) = N'';

;WITH seed([Key], [Value], GroupName, ValueType, [Description]) AS (
    SELECT * FROM (VALUES
        (N'Sms.IsEnabled',                     N'false',   N'SMS', N'bool',   N'فعال‌سازی سرویس پیامک SMS.ir'),
        (N'Sms.Provider',                      N'SMS.ir',  N'SMS', N'string', N'ارائه‌دهنده پیامک'),
        (N'Sms.ApiKey',                        N'',        N'SMS', N'secret', N'کلید API پنل SMS.ir (محرمانه)'),
        (N'Sms.DefaultLineNumber',             N'',        N'SMS', N'string', N'شماره خط اختصاصی برای پیامک متنی (محرمانه)'),
        (N'Sms.SenderName',                    N'ویتورایز', N'SMS', N'string', N'نام فرستنده'),
        (N'Sms.OtpTemplateId',                 @UniversalOtpTemplateId,          N'SMS', N'int', N'شناسه قالب کد یکبار مصرف'),
        (N'Sms.NotificationTemplateId',        @UniversalNotificationTemplateId, N'SMS', N'int', N'شناسه قالب اطلاع‌رسانی عمومی'),
        (N'Sms.LoginOtpTemplateId',            @UniversalOtpTemplateId,          N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.RegisterOtpTemplateId',         @UniversalOtpTemplateId,          N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.ForgotPasswordTemplateId',      @UniversalOtpTemplateId,          N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.OrderPaidTemplateId',           @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OrderCompletedTemplateId',      @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OrderStatusChangedTemplateId',  @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.GiftCodeDeliveredTemplateId',   @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.TicketReplyTemplateId',         @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.VerificationApprovedTemplateId',@UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.VerificationRejectedTemplateId',@UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.WalletTopUpSuccessTemplateId',  @UniversalNotificationTemplateId, N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OtpExpiryMinutes',              N'3',       N'SMS', N'int',    N'مدت اعتبار کد یکبار‌مصرف (دقیقه)'),
        (N'Sms.OtpResendCooldownSeconds',      N'90',      N'SMS', N'int',    N'فاصله ارسال مجدد کد (ثانیه)'),
        (N'Sms.OtpMaxAttempts',                N'5',       N'SMS', N'int',    N'حداکثر تلاش مجاز برای هر کد'),
        (N'Sms.DailyOtpLimitPerMobile',        N'10',      N'SMS', N'int',    N'سقف کد روزانه برای هر شماره'),
        (N'Sms.DailySmsLimitPerMobile',        N'30',      N'SMS', N'int',    N'سقف پیامک روزانه برای هر شماره'),
        (N'Sms.MaxRetryCount',                 N'5',       N'SMS', N'int',    N'حداکثر تعداد بازتلاش ارسال'),
        (N'Sms.RetryDelaySeconds',             N'30',      N'SMS', N'int',    N'پایه تأخیر بازتلاش (ثانیه)'),
        (N'Sms.UseOutbox',                     N'true',    N'SMS', N'bool',   N'ارسال پیامک رویدادهای تجاری از طریق Outbox'),
        (N'Sms.CustomSendEnabled',              N'false',   N'SMS', N'bool',   N'فعال‌سازی ارسال پیامک سفارشی توسط مدیر'),
        (N'Sms.CustomTextEnabled',              N'false',   N'SMS', N'bool',   N'فعال‌سازی پیامک متنی سفارشی'),
        (N'Sms.MaxCustomRecipients',            N'1',       N'SMS', N'int',    N'حداکثر گیرنده در هر ارسال سفارشی'),
        (N'Sms.MaxCustomTextLength',            N'500',     N'SMS', N'int',    N'حداکثر طول پیامک متنی سفارشی'),
        (N'Sms.RequireConfirmation',            N'true',    N'SMS', N'bool',   N'نیاز به تایید نهایی پیش از ارسال سفارشی'),
        (N'Sms.AllowImmediateSend',             N'false',   N'SMS', N'bool',   N'اجازه ارسال فوری به جای صف'),
        (N'Sms.HistoryRetentionDays',           N'180',     N'SMS', N'int',    N'مدت نگهداری تاریخچه پیامک بر حسب روز'),
        (N'Sms.MaskMobileInAdmin',              N'true',    N'SMS', N'bool',   N'پنهان‌سازی شماره موبایل در تاریخچه مدیر'),
        (N'Sms.AllowAdminViewFullMobile',       N'false',   N'SMS', N'bool',   N'اجازه مشاهده شماره کامل برای مدیر کل'),
        (N'Sms.AllowRetryFailed',               N'true',    N'SMS', N'bool',   N'اجازه بازتلاش امن پیامک ناموفق'),
        (N'Sms.LogSensitiveData',              N'false',   N'SMS', N'bool',   N'لاگ‌کردن داده حساس (فقط توسعه)')
    ) AS v([Key], [Value], GroupName, ValueType, [Description])
)
INSERT INTO Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), s.[Key], s.[Value], s.GroupName, s.ValueType, s.[Description], SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM Settings e WHERE e.[Key] = s.[Key]);

-- Refresh template metadata on existing installations without touching their IDs.
UPDATE target
SET [Description] = metadata.[Description],
    GroupName = N'SMS',
    ValueType = N'int',
    UpdatedAt = SYSUTCDATETIME()
FROM Settings target
INNER JOIN (VALUES
    (N'Sms.OtpTemplateId',                  N'شناسه قالب کد یکبار مصرف'),
    (N'Sms.NotificationTemplateId',         N'شناسه قالب اطلاع‌رسانی عمومی'),
    (N'Sms.LoginOtpTemplateId',             N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.RegisterOtpTemplateId',          N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.ForgotPasswordTemplateId',       N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.OrderPaidTemplateId',            N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.OrderCompletedTemplateId',       N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.OrderStatusChangedTemplateId',   N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.GiftCodeDeliveredTemplateId',    N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.TicketReplyTemplateId',          N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.VerificationApprovedTemplateId', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.VerificationRejectedTemplateId', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.WalletTopUpSuccessTemplateId',   N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)')
) metadata([Key], [Description]) ON metadata.[Key] = target.[Key]
WHERE ISNULL(target.[Description], N'') <> metadata.[Description]
   OR ISNULL(target.GroupName, N'') <> N'SMS'
   OR ISNULL(target.ValueType, N'') <> N'int';

-- If deployment variables were supplied, synchronize existing compatibility rows too.
IF NULLIF(@UniversalOtpTemplateId, N'') IS NOT NULL
    UPDATE Settings
    SET [Value] = @UniversalOtpTemplateId, UpdatedAt = SYSUTCDATETIME()
    WHERE [Key] IN (
        N'Sms.OtpTemplateId', N'Sms.LoginOtpTemplateId',
        N'Sms.RegisterOtpTemplateId', N'Sms.ForgotPasswordTemplateId');

IF NULLIF(@UniversalNotificationTemplateId, N'') IS NOT NULL
    UPDATE Settings
    SET [Value] = @UniversalNotificationTemplateId, UpdatedAt = SYSUTCDATETIME()
    WHERE [Key] IN (
        N'Sms.NotificationTemplateId', N'Sms.OrderPaidTemplateId',
        N'Sms.OrderCompletedTemplateId', N'Sms.OrderStatusChangedTemplateId',
        N'Sms.GiftCodeDeliveredTemplateId', N'Sms.TicketReplyTemplateId',
        N'Sms.VerificationApprovedTemplateId', N'Sms.VerificationRejectedTemplateId',
        N'Sms.WalletTopUpSuccessTemplateId');

PRINT 'Vitorize SMS settings seed complete. Existing values (including any API key) were preserved.';
