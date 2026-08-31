/*
  Vitorize standalone production upgrade: V0031 -> V0032

  Run this file in SSMS after selecting the Vitorize production database.
  No USE statement, SQLCMD mode, external file, or parameter is required.
  It only removes obsolete SMS activation settings; SMS history, API key,
  sender line, templates, orders, and customer data are preserved.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
        THROW 51350, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

    IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
        THROW 51351, N'جدول تاریخچه مهاجرت وجود ندارد؛ این فایل فقط برای ارتقای دیتابیس canonical نسخه V0031 است.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0031'
          AND ScriptName = N'V0031__paid_order_number_sequence.sql'
          AND ScriptHash = 'ca6958841bd412bd5aad10f1336aaed60dd81286727999036bc0a5f8db6d99b4'
          AND Success = 1
    )
        THROW 51352, N'این دیتابیس در وضعیت canonical نسخه V0031 نیست. اسکریپت اجرا نشد و تغییری اعمال نشد.', 1;

    IF EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptVersion = N'V0032')
        THROW 51353, N'نسخه V0032 قبلاً ثبت شده است. برای جلوگیری از اجرای تکراری، تغییری اعمال نشد.', 1;

    BEGIN TRANSACTION;

    DELETE FROM dbo.Settings
    WHERE [Key] IN
    (
        N'SmsEnabled',
        N'Sms.IsEnabled',
        N'Sms.CustomSendEnabled',
        N'Sms.CustomTextEnabled',
        N'Sms.UseOutbox',
        N'Sms.RequireConfirmation',
        N'Sms.AllowImmediateSend',
        N'Sms.AllowRetryFailed'
    );

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Settings
        WHERE [Key] IN
        (
            N'SmsEnabled', N'Sms.IsEnabled', N'Sms.CustomSendEnabled', N'Sms.CustomTextEnabled',
            N'Sms.UseOutbox', N'Sms.RequireConfirmation', N'Sms.AllowImmediateSend', N'Sms.AllowRetryFailed'
        )
    )
        THROW 51354, N'اعتبارسنجی نهایی ناموفق بود: یکی از کلیدهای منسوخ فعال‌سازی پیامک باقی مانده است.', 1;

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0032__always_on_sms_remove_activation_switches.sql', N'V0032',
        '9981f260851fb928f8fea707e482ec147c39e2d2d9fe66eeddbe864c4264ec05',
        N'Production', 1, N'Canonical deployment chain');

    COMMIT TRANSACTION;

    SELECT N'ارتقای Vitorize از V0031 به V0032 با موفقیت انجام شد.' AS Result,
           DB_NAME() AS DatabaseName,
           SYSUTCDATETIME() AS CompletedAtUtc;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
