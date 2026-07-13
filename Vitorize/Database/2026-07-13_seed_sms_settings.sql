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

;WITH seed([Key], [Value], GroupName, ValueType, [Description]) AS (
    SELECT * FROM (VALUES
        (N'Sms.IsEnabled',                     N'false',   N'SMS', N'bool',   N'فعال‌سازی سرویس پیامک SMS.ir'),
        (N'Sms.Provider',                      N'SMS.ir',  N'SMS', N'string', N'ارائه‌دهنده پیامک'),
        (N'Sms.ApiKey',                        N'',        N'SMS', N'secret', N'کلید API پنل SMS.ir (محرمانه)'),
        (N'Sms.DefaultLineNumber',             N'',        N'SMS', N'string', N'شماره خط اختصاصی برای پیامک متنی (محرمانه)'),
        (N'Sms.SenderName',                    N'ویتورایز', N'SMS', N'string', N'نام فرستنده'),
        (N'Sms.OtpTemplateId',                 N'',        N'SMS', N'int',    N'شناسه قالب پیش‌فرض کد یکبار‌مصرف (CODE, EXPIRE)'),
        (N'Sms.LoginOtpTemplateId',            N'',        N'SMS', N'int',    N'شناسه قالب کد ورود (CODE, EXPIRE)'),
        (N'Sms.RegisterOtpTemplateId',         N'',        N'SMS', N'int',    N'شناسه قالب کد ثبت‌نام/تایید موبایل (CODE, EXPIRE)'),
        (N'Sms.ForgotPasswordTemplateId',      N'',        N'SMS', N'int',    N'شناسه قالب کد بازیابی رمز (CODE, EXPIRE)'),
        (N'Sms.OrderPaidTemplateId',           N'',        N'SMS', N'int',    N'شناسه قالب پرداخت موفق سفارش (ORDER, AMOUNT)'),
        (N'Sms.OrderCompletedTemplateId',      N'',        N'SMS', N'int',    N'شناسه قالب تکمیل سفارش (ORDER)'),
        (N'Sms.OrderStatusChangedTemplateId',  N'',        N'SMS', N'int',    N'شناسه قالب تغییر وضعیت سفارش (ORDER, STATUS)'),
        (N'Sms.GiftCodeDeliveredTemplateId',   N'',        N'SMS', N'int',    N'شناسه قالب تحویل کد (ORDER)'),
        (N'Sms.TicketReplyTemplateId',         N'',        N'SMS', N'int',    N'شناسه قالب پاسخ تیکت (TICKET)'),
        (N'Sms.VerificationApprovedTemplateId',N'',        N'SMS', N'int',    N'شناسه قالب تایید احراز هویت (NAME)'),
        (N'Sms.VerificationRejectedTemplateId',N'',        N'SMS', N'int',    N'شناسه قالب رد احراز هویت (NAME, REASON)'),
        (N'Sms.WalletTopUpSuccessTemplateId',  N'',        N'SMS', N'int',    N'شناسه قالب شارژ موفق کیف پول (AMOUNT, BALANCE)'),
        (N'Sms.OtpExpiryMinutes',              N'3',       N'SMS', N'int',    N'مدت اعتبار کد یکبار‌مصرف (دقیقه)'),
        (N'Sms.OtpResendCooldownSeconds',      N'90',      N'SMS', N'int',    N'فاصله ارسال مجدد کد (ثانیه)'),
        (N'Sms.OtpMaxAttempts',                N'5',       N'SMS', N'int',    N'حداکثر تلاش مجاز برای هر کد'),
        (N'Sms.DailyOtpLimitPerMobile',        N'10',      N'SMS', N'int',    N'سقف کد روزانه برای هر شماره'),
        (N'Sms.DailySmsLimitPerMobile',        N'30',      N'SMS', N'int',    N'سقف پیامک روزانه برای هر شماره'),
        (N'Sms.MaxRetryCount',                 N'5',       N'SMS', N'int',    N'حداکثر تعداد بازتلاش ارسال'),
        (N'Sms.RetryDelaySeconds',             N'30',      N'SMS', N'int',    N'پایه تأخیر بازتلاش (ثانیه)'),
        (N'Sms.UseOutbox',                     N'true',    N'SMS', N'bool',   N'ارسال پیامک رویدادهای تجاری از طریق Outbox'),
        (N'Sms.LogSensitiveData',              N'false',   N'SMS', N'bool',   N'لاگ‌کردن داده حساس (فقط توسعه)')
    ) AS v([Key], [Value], GroupName, ValueType, [Description])
)
INSERT INTO Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), s.[Key], s.[Value], s.GroupName, s.ValueType, s.[Description], SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM Settings e WHERE e.[Key] = s.[Key]);

PRINT 'Vitorize SMS settings seed complete. Existing values (including any API key) were preserved.';
