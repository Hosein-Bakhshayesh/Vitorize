/*
  Vitorize standalone production upgrade: V0025 -> V0031

  Run this single file in SSMS after selecting the Vitorize production database.
  No USE statement, SQLCMD mode, external file, or parameter is required.

  Safety contract:
  - refuses system databases;
  - refuses a database unless canonical V0025 exists in the immutable ledger;
  - refuses a partially/already upgraded database;
  - applies all changes and ledger rows in ONE transaction;
  - rolls back every change if any statement fails.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
        THROW 51320, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

    IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
        THROW 51321, N'جدول تاریخچه مهاجرت وجود ندارد؛ این فایل فقط برای ارتقای دیتابیس canonical در نسخه V0025 است.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0025'
          AND ScriptName = N'V0025__peyda_default_and_sortmode_setting_type.sql'
          AND ScriptHash = 'ad21b18d3918da4ab56221e1b00b1fd4c455de9f58cc50d1adc28513a713d5c6'
          AND Success = 1
    )
        THROW 51322, N'این دیتابیس در وضعیت canonical نسخه V0025 نیست. اسکریپت اجرا نشد و تغییری اعمال نشد.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion IN (N'V0026', N'V0027', N'V0028', N'V0029', N'V0030', N'V0031')
    )
        THROW 51323, N'حداقل یکی از نسخه‌های V0026 تا V0031 قبلاً ثبت شده است. برای جلوگیری از اجرای تکراری، تغییری اعمال نشد.', 1;

    BEGIN TRANSACTION;

    /* V0026 — remove retired product-level KYC configuration. */
    IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
        THROW 51324, N'جدول dbo.Products وجود ندارد.', 1;

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Products') AND name = N'CK_Products_KycConfiguration')
        ALTER TABLE dbo.Products DROP CONSTRAINT CK_Products_KycConfiguration;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Products') AND name = N'FK_Products_KycPolicyVersions')
        ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_KycPolicyVersions;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_KycPolicyVersionId')
        DROP INDEX IX_Products_KycPolicyVersionId ON dbo.Products;

    DECLARE @dropProductKycDefaults nvarchar(max) =
    (
        SELECT STRING_AGG(
            N'ALTER TABLE dbo.Products DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';',
            NCHAR(10))
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products')
          AND c.name IN (N'RequiresVerification', N'KycRequirementMode', N'KycThresholdAmount', N'KycPolicyVersionId')
    );

    IF @dropProductKycDefaults IS NOT NULL
        EXEC sys.sp_executesql @dropProductKycDefaults;

    IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN RequiresVerification;
    IF COL_LENGTH(N'dbo.Products', N'KycRequirementMode') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycRequirementMode;
    IF COL_LENGTH(N'dbo.Products', N'KycThresholdAmount') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycThresholdAmount;
    IF COL_LENGTH(N'dbo.Products', N'KycPolicyVersionId') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycPolicyVersionId;

    IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL OR
       COL_LENGTH(N'dbo.Products', N'KycRequirementMode') IS NOT NULL OR
       COL_LENGTH(N'dbo.Products', N'KycThresholdAmount') IS NOT NULL OR
       COL_LENGTH(N'dbo.Products', N'KycPolicyVersionId') IS NOT NULL
        THROW 51325, N'حذف تنظیمات منسوخ احراز هویت کالا ناموفق بود.', 1;

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0026__remove_product_level_kyc.sql', N'V0026', 'ae1fe1f5313da1f65193864f3763eb642967d869c752d4e2ceb770fc80f73e53', N'Production', 1, N'Canonical deployment chain');

    /* V0027 — configure final-order-total KYC and its two required documents. */
    IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Verification.OrderAmountThresholdToman')
        INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, Description, UpdatedAt)
        VALUES (NEWID(), N'Verification.OrderAmountThresholdToman', N'1000000', N'Verification', N'decimal', N'آستانه مبلغ نهایی سفارش برای احراز هویت (تومان؛ صفر = غیرفعال)', SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Verification.CustomerNotice')
        INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, Description, UpdatedAt)
        VALUES (NEWID(), N'Verification.CustomerNotice', N'به دلیل حساسیت‌های اخیر پلیس محترم فتا و جهت جلوگیری از جرایم و کلاهبرداری الکترونیکی، فروشگاه اینترنتی ویتورایز ناچار است تا هویت فردی مشتریان خود را تأیید کند. اطلاعات شما نزد فروشگاه ویتورایز محفوظ خواهند ماند و این مراحل صرفاً جهت جلوگیری از کلاهبرداری‌های اینترنتی و موارد فیشینگ و سایبری است؛ بنابراین می‌توانید با خیال راحت احراز هویت خود را انجام دهید و در کمترین زمان ممکن نتیجه از طریق پیامک به شما اعلام خواهد شد.', N'Verification', N'string', N'متن راهنمای قابل جمع شدن در فرم احراز هویت سفارش', SYSUTCDATETIME());

    DECLARE @identityDocumentId uniqueidentifier = (SELECT Id FROM dbo.KycDocumentTypes WHERE Code = N'bank-card-holder-identity');
    IF @identityDocumentId IS NULL
    BEGIN
        SET @identityDocumentId = NEWID();
        INSERT dbo.KycDocumentTypes (Id, Code, Title, Description, IsActive, AllowedExtensions, MaxFileSizeBytes, SortOrder, CreatedAt)
        VALUES (@identityDocumentId, N'bank-card-holder-identity', N'تصویر مدرک هویتی دارنده کارت', N'لطفاً تصویر کارت ملی خود را بارگذاری کنید. در صورتی که به کارت ملی دسترسی ندارید، تصویر شناسنامه، پاسپورت، گواهینامه رانندگی، کارت سوخت یا دیگر مدارک هویتی نیز پذیرفته می‌شود.', 1, N'jpg,jpeg,png,webp', 5242880, 10, SYSUTCDATETIME());
    END;

    DECLARE @bankCardDocumentId uniqueidentifier = (SELECT Id FROM dbo.KycDocumentTypes WHERE Code = N'bank-card-holder-card');
    IF @bankCardDocumentId IS NULL
    BEGIN
        SET @bankCardDocumentId = NEWID();
        INSERT dbo.KycDocumentTypes (Id, Code, Title, Description, IsActive, AllowedExtensions, MaxFileSizeBytes, SortOrder, CreatedAt)
        VALUES (@bankCardDocumentId, N'bank-card-holder-card', N'تصویر کارت بانکی دارنده کارت', N'در صورت نیاز، می‌توانید اطلاعات حساس کارت خود، از جمله CVV و تاریخ انقضا، را با یک تکه کاغذ یا انگشتان خود بپوشانید.', 1, N'jpg,jpeg,png,webp', 5242880, 20, SYSUTCDATETIME());
    END;

    DECLARE @policyId uniqueidentifier = (SELECT Id FROM dbo.KycPolicies WHERE Code = N'order-total-verification');
    IF @policyId IS NULL
    BEGIN
        SET @policyId = NEWID();
        INSERT dbo.KycPolicies (Id, Code, Name, IsActive, CreatedAt)
        VALUES (@policyId, N'order-total-verification', N'احراز هویت بر اساس مبلغ نهایی سفارش', 1, SYSUTCDATETIME());
    END;

    DECLARE @versionId uniqueidentifier = (SELECT Id FROM dbo.KycPolicyVersions WHERE KycPolicyId = @policyId AND Version = 1);
    IF @versionId IS NULL
    BEGIN
        SET @versionId = NEWID();
        INSERT dbo.KycPolicyVersions (Id, KycPolicyId, Version, Status, CustomerTitle, CustomerInstructions, CreatedAt, PublishedAt)
        VALUES (@versionId, @policyId, 1, 2, N'احراز هویت سفارش', N'برای ادامه فرایند تحویل این سفارش، اطلاعات و مدارک هویتی دارنده کارت بانکی را ثبت کنید.', SYSUTCDATETIME(), SYSUTCDATETIME());
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.KycPolicyDocumentRequirements WHERE KycPolicyVersionId = @versionId AND KycDocumentTypeId = @identityDocumentId)
        INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder, Instructions, RedactionMode)
        VALUES (NEWID(), @versionId, @identityDocumentId, 1, 10, N'لطفاً تصویر کارت ملی خود را بارگذاری کنید. در صورتی که به کارت ملی خود دسترسی ندارید، می‌توانید تصویر دیگر مدارک هویتی از جمله شناسنامه، پاسپورت، گواهینامه رانندگی، کارت سوخت و ... را نیز بارگذاری کنید.', 0);

    IF NOT EXISTS (SELECT 1 FROM dbo.KycPolicyDocumentRequirements WHERE KycPolicyVersionId = @versionId AND KycDocumentTypeId = @bankCardDocumentId)
        INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder, Instructions, RedactionMode, RedactionInstructions)
        VALUES (NEWID(), @versionId, @bankCardDocumentId, 1, 20, N'در صورت نیاز، می‌توانید اطلاعات حساس کارت خود را از جمله CVV و تاریخ انقضا با یک تکه کاغذ یا انگشتان خود بپوشانید.', 0, NULL);

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0027__order_total_kyc_settings_and_policy.sql', N'V0027', '9720ffb6a52cc72101db3ebcfe272982f95f8347350879dd9b9b1037f11f867e', N'Production', 1, N'Canonical deployment chain');

    /* V0028 — retain only the payment provider masked card PAN. */
    IF COL_LENGTH(N'dbo.Payments', N'MaskedCardPan') IS NULL
        ALTER TABLE dbo.Payments ADD MaskedCardPan nvarchar(32) NULL;

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0028__payment_masked_card_pan.sql', N'V0028', 'f2cde7ef5bfab578f390bf76e8b436bee1057e0709039f8c028da04327b51881', N'Production', 1, N'Canonical deployment chain');

    /* V0029 — replace fixed trust-seal URLs with trusted HTML/script settings. */
    DELETE FROM dbo.Settings
    WHERE [Key] IN
    (
        N'TrustSeal.Enamad.Enabled', N'TrustSeal.Enamad.Title', N'TrustSeal.Enamad.Url',
        N'TrustSeal.Enamad.ImagePath', N'TrustSeal.Enamad.Alt', N'TrustSeal.Enamad.SortOrder', N'TrustSeal.Enamad.NewTab',
        N'TrustSeal.Ecunion.Enabled', N'TrustSeal.Ecunion.Title', N'TrustSeal.Ecunion.Url',
        N'TrustSeal.Ecunion.ImagePath', N'TrustSeal.Ecunion.Alt', N'TrustSeal.Ecunion.SortOrder', N'TrustSeal.Ecunion.NewTab',
        N'TrustSeal.Samandehi.Enabled', N'TrustSeal.Samandehi.Title', N'TrustSeal.Samandehi.Url',
        N'TrustSeal.Samandehi.ImagePath', N'TrustSeal.Samandehi.Alt', N'TrustSeal.Samandehi.SortOrder', N'TrustSeal.Samandehi.NewTab'
    );

    IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'TrustSeal.FooterHtml')
        INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
        VALUES (NEWID(), N'TrustSeal.FooterHtml', N'', N'TrustSeals', N'trustedhtml',
            N'کدهای رسمی نمادهای اعتماد در فوتر (اینماد، زرین‌پال، ایمالز، ترب و ...)', SYSUTCDATETIME());

    UPDATE dbo.Settings
    SET ValueType = N'trustedhtml',
        [Description] = CASE [Key]
            WHEN N'CustomHeadHtml' THEN N'کد سفارشی داخل <head> (فقط کد مورداعتماد مانند تحلیل و تگ‌ها)'
            ELSE N'کد سفارشی انتهای سایت (فقط کد مورداعتماد)'
        END,
        UpdatedAt = SYSUTCDATETIME()
    WHERE [Key] IN (N'CustomHeadHtml', N'CustomFooterHtml');

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0029__trusted_footer_seals_and_custom_markup.sql', N'V0029', '7eccbfbf0ccb451a67b151b9bc2d79fca68d84ff00d67c92d825044e3a8324f0', N'Production', 1, N'Canonical deployment chain');

    /* V0030 — custom SMS is permanently enabled; remove obsolete switches. */
    DELETE FROM dbo.Settings
    WHERE [Key] IN (N'Sms.CustomSendEnabled', N'Sms.CustomTextEnabled');

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0030__remove_deprecated_custom_sms_settings.sql', N'V0030', '828eb33a554293092ed47ffddb946cf5c4cf0b511fe8100183dc73824477a53c', N'Production', 1, N'Canonical deployment chain');

    /* V0031 — reserve a durable sequence for short public order numbers. */
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

    INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
    VALUES (N'V0031__paid_order_number_sequence.sql', N'V0031', 'ca6958841bd412bd5aad10f1336aaed60dd81286727999036bc0a5f8db6d99b4', N'Production', 1, N'Canonical deployment chain');

    /* Final in-transaction verification. */
    IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL
        THROW 51326, N'اعتبارسنجی نهایی ناموفق بود: ستون منسوخ Products.RequiresVerification باقی مانده است.', 1;
    IF COL_LENGTH(N'dbo.Payments', N'MaskedCardPan') IS NULL
        THROW 51327, N'اعتبارسنجی نهایی ناموفق بود: ستون Payments.MaskedCardPan ایجاد نشده است.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Verification.OrderAmountThresholdToman')
        THROW 51328, N'اعتبارسنجی نهایی ناموفق بود: تنظیم آستانه احراز هویت ایجاد نشده است.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'TrustSeal.FooterHtml' AND ValueType = N'trustedhtml')
        THROW 51329, N'اعتبارسنجی نهایی ناموفق بود: تنظیم کد نمادهای اعتماد ایجاد نشده است.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] IN (N'Sms.CustomSendEnabled', N'Sms.CustomTextEnabled'))
        THROW 51330, N'اعتبارسنجی نهایی ناموفق بود: تنظیم‌های منسوخ پیامک هنوز وجود دارند.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.OrderNumberCounters WHERE Id = 1 AND NextNumber >= 8000)
        THROW 51332, N'اعتبارسنجی نهایی ناموفق بود: شمارنده شماره سفارش ایجاد نشده است.', 1;
    IF (SELECT COUNT(*) FROM dbo.DatabaseScriptHistory WHERE ScriptVersion IN (N'V0026', N'V0027', N'V0028', N'V0029', N'V0030', N'V0031') AND Success = 1) <> 6
        THROW 51331, N'اعتبارسنجی نهایی ناموفق بود: تاریخچه کامل مهاجرت ثبت نشده است.', 1;

    COMMIT TRANSACTION;

    SELECT
        N'ارتقای Vitorize از V0025 به V0031 با موفقیت انجام شد.' AS Result,
        DB_NAME() AS DatabaseName,
        SYSUTCDATETIME() AS CompletedAtUtc;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
