/*
  Store-wide KYC configuration:
  - threshold is evaluated against the final payable order amount, in Toman;
  - the published policy contains the two documents required by the customer form.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

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

COMMIT TRANSACTION;
