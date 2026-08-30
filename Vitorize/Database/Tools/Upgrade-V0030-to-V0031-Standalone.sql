/*
  Vitorize standalone production upgrade: V0030 -> V0031

  Run this file in SSMS after selecting the Vitorize production database.
  No USE statement, SQLCMD mode, external file, or parameter is required.
  Existing orders and their public order numbers are preserved.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
        THROW 51340, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

    IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
        THROW 51341, N'جدول تاریخچه مهاجرت وجود ندارد؛ این فایل فقط برای ارتقای دیتابیس canonical نسخه V0030 است.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0030'
          AND ScriptName = N'V0030__remove_deprecated_custom_sms_settings.sql'
          AND ScriptHash = '828eb33a554293092ed47ffddb946cf5c4cf0b511fe8100183dc73824477a53c'
          AND Success = 1
    )
        THROW 51342, N'این دیتابیس در وضعیت canonical نسخه V0030 نیست. اسکریپت اجرا نشد و تغییری اعمال نشد.', 1;

    IF EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptVersion = N'V0031')
        THROW 51343, N'نسخه V0031 قبلاً ثبت شده است. برای جلوگیری از اجرای تکراری، تغییری اعمال نشد.', 1;

    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.OrderNumberCounters', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.OrderNumberCounters
        (
            Id tinyint NOT NULL CONSTRAINT PK_OrderNumberCounters PRIMARY KEY,
            NextNumber bigint NOT NULL
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.OrderNumberCounters WHERE Id = 1)
    BEGIN
        INSERT INTO dbo.OrderNumberCounters (Id, NextNumber)
        VALUES (1, 8000);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.OrderNumberCounters WHERE Id = 1 AND NextNumber >= 8000)
        THROW 51344, N'اعتبارسنجی نهایی ناموفق بود: شمارنده شماره سفارش ایجاد نشده است.', 1;

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0031__paid_order_number_sequence.sql', N'V0031',
        'ca6958841bd412bd5aad10f1336aaed60dd81286727999036bc0a5f8db6d99b4',
        N'Production', 1, N'Canonical deployment chain');

    COMMIT TRANSACTION;

    SELECT N'ارتقای Vitorize از V0030 به V0031 با موفقیت انجام شد.' AS Result,
           DB_NAME() AS DatabaseName,
           SYSUTCDATETIME() AS CompletedAtUtc;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
