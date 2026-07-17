SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @CategoryId uniqueidentifier = '31000000-0000-0000-0000-000000000001';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000002';
DECLARE @TagId uniqueidentifier = '31000000-0000-0000-0000-000000000003';
DECLARE @ReviewUserId uniqueidentifier = '31000000-0000-0000-0000-000000000004';
DECLARE @ReviewId uniqueidentifier = '31000000-0000-0000-0000-000000000005';
DECLARE @BrandId uniqueidentifier = '31000000-0000-0000-0000-000000000006';
DECLARE @VariantId uniqueidentifier = '31000000-0000-0000-0000-000000000007';
DECLARE @FeatureId uniqueidentifier = '31000000-0000-0000-0000-000000000008';
DECLARE @InputFieldId uniqueidentifier = '31000000-0000-0000-0000-000000000009';
DECLARE @ImageId uniqueidentifier = '31000000-0000-0000-0000-000000000010';
DECLARE @RelatedProductId uniqueidentifier = '31000000-0000-0000-0000-000000000011';
DECLARE @CouponId uniqueidentifier = '31000000-0000-0000-0000-000000000012';
DECLARE @E2eAdminId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Users WHERE Mobile = N'09120000011');

-- Keep the dedicated Testing-only administrator stable between runs. Browser
-- scenarios create immutable financial history that must retain its actor FK,
-- so deleting and re-bootstrapping this identity would corrupt the fixture.
-- The known password hash below belongs only to the isolated E2E database.
IF @E2eAdminId IS NOT NULL
BEGIN
    DELETE FROM dbo.UserRefreshTokens WHERE UserId = @E2eAdminId;
    UPDATE dbo.Users
    SET PasswordHash = N'$2a$11$oRlRYEDBoNTt6xcAxAEcmeoOi/Ketcai3BWjZBLeCjnLhrwxIWc2y',
        Status = 1,
        IsMobileConfirmed = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @E2eAdminId;

    INSERT dbo.UserRoles (UserId, RoleId)
    SELECT @E2eAdminId, r.Id
    FROM dbo.Roles r
    WHERE r.Name = N'SuperAdmin'
      AND NOT EXISTS
          (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = @E2eAdminId AND ur.RoleId = r.Id);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Id = @CategoryId)
    INSERT dbo.Categories (Id, Title, Slug, [Description], SeoTitle, SeoDescription, FocusKeyword, ImageAltText, IsActive, IsDeleted, CreatedAt)
    VALUES (@CategoryId, N'دسته آزمون مرورگر', N'e2e-category', N'دسته‌بندی قطعی آزمون مرورگر.', N'دسته آزمون مرورگر', N'توضیح دسته آزمون مرورگر.', N'محصول تست', N'تصویر دسته آزمون', 1, 0, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId)
    INSERT dbo.Products
        (Id, CategoryId, Title, Slug, ShortDescription, FullDescription, SeoTitle, SeoDescription, FocusKeyword,
         ThumbnailAltText, ProductType, DeliveryType, BasePrice, CurrencyType, MinOrderQuantity, IsActive, IsFeatured, IsDeleted, CreatedAt)
    VALUES
        (@ProductId, @CategoryId, N'محصول قطعی آزمون مرورگر', N'e2e-seo-product', N'توضیح کوتاه محصول آزمون.',
         N'<p>توضیح کامل و امن محصول آزمون مرورگر.</p>', N'محصول آزمون سئو', N'توضیح متای قطعی برای آزمون سئو.',
         N'خرید محصول آزمون', N'تصویر محصول آزمون', 1, 2, 125000, 2, 1, 1, 1, 0, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Id = @BrandId)
    INSERT dbo.Brands (Id, Title, Slug, [Description], SeoTitle, SeoDescription, FocusKeyword, ImageAltText, IsActive, CreatedAt)
    VALUES (@BrandId, N'E2E Brand', N'e2e-brand', N'Deterministic browser brand.', N'E2E Brand SEO',
            N'E2E brand meta description.', N'e2e brand', N'E2E brand logo', 1, SYSUTCDATETIME());

UPDATE dbo.Categories
SET Title = N'E2E Category', [Description] = N'Deterministic browser category.',
    SeoTitle = N'E2E Category SEO', SeoDescription = N'E2E category meta description.'
WHERE Id = @CategoryId;

UPDATE dbo.Products
SET BrandId = @BrandId, Title = N'E2E Dynamic Product',
    ShortDescription = N'Deterministic product with variants and required customer fields.',
    FullDescription = N'<h2>Rich E2E Description</h2><p>This <strong>sanitized HTML</strong> is rendered for browser verification.</p><ul><li>Responsive</li><li>Secure</li></ul>',
    SeoTitle = N'E2E Product SEO', SeoDescription = N'E2E product meta description.',
    FocusKeyword = N'e2e product',
    ThumbnailImagePath = N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',
    ThumbnailAltText = N'E2E product artwork', BasePrice = 125000,
    DeliveryType = 2, IsActive = 1, IsDeleted = 0
WHERE Id = @ProductId;

IF NOT EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE Id = @VariantId)
    INSERT dbo.ProductVariants
        (Id, ProductId, Title, Sku, Price, DiscountPrice, Value, StockMode, IsDefault, IsActive, SortOrder, CreatedAt)
    VALUES (@VariantId, @ProductId, N'E2E Premium Variant', N'E2E-PREMIUM', 150000, 140000,
            N'premium', 3, 1, 1, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.ProductFeatures WHERE Id = @FeatureId)
    INSERT dbo.ProductFeatures (Id, ProductId, Title, Value, IconKey, SortOrder, IsActive, CreatedAt)
    VALUES (@FeatureId, @ProductId, N'Platform', N'Browser', 'monitor', 1, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.ProductInputFields WHERE Id = @InputFieldId)
    INSERT dbo.ProductInputFields
        (Id, ProductId, [Key], Label, [Description], Placeholder, FieldType, IsRequired,
         IsSensitive, RequiresConfirmation, DisplayStage, SortOrder, IsActive, CreatedAt)
    VALUES (@InputFieldId, @ProductId, 'account_email', N'Account Email', N'Used only to fulfill this test order.',
            N'customer@example.test', 2, 1, 0, 0, 1, 1, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.ProductImages WHERE Id = @ImageId)
    INSERT dbo.ProductImages (Id, ProductId, ImagePath, AltText, SortOrder, CreatedAt)
    VALUES (@ImageId, @ProductId, N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',
            N'E2E gallery artwork', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @RelatedProductId)
    INSERT dbo.Products
        (Id, CategoryId, BrandId, Title, Slug, ShortDescription, FullDescription, SeoTitle, SeoDescription,
         ThumbnailImagePath, ThumbnailAltText, ProductType, DeliveryType, BasePrice, CurrencyType,
         MinOrderQuantity, IsActive, IsFeatured, IsDeleted, CreatedAt)
    VALUES (@RelatedProductId, @CategoryId, @BrandId, N'E2E Related Product', N'e2e-related-product',
            N'Related product for browser coverage.', N'<p>Related description.</p>', N'E2E Related SEO',
            N'E2E related product meta.', N'/uploads/products/947c2fd1b9a84f2ea86a683008e7fdc0.jpg',
            N'E2E related artwork', 1, 2, 50000, 2, 1, 1, 0, 0, SYSUTCDATETIME());

UPDATE dbo.Products
SET DeliveryType = 1, IsActive = 1, IsDeleted = 0
WHERE Id = @RelatedProductId;

-- Encryption keys are deliberately ephemeral for each managed browser stack.
-- Retire only unconsumed codes belonging to the dedicated E2E instant product
-- so a later run cannot reserve ciphertext created with an earlier test key.
UPDATE dbo.GiftCodeReservations
SET Status = 3
WHERE ProductId = @RelatedProductId AND Status = 1;

UPDATE dbo.GiftCodes
SET Status = 5,
    ReservedByUserId = NULL,
    ReservedAt = NULL,
    ReservationExpiresAt = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE ProductId = @RelatedProductId AND Status IN (0, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Coupons WHERE Id = @CouponId)
    INSERT dbo.Coupons
        (Id, Code, Title, DiscountType, DiscountValue, MaxUsageCount, UsedCount, MaxUsagePerUser,
         MinOrderAmount, StartsAt, EndsAt, IsActive, CreatedAt)
    VALUES (@CouponId, N'E2E10', N'E2E ten percent', 1, 10, 1000, 0, 10, 1000,
            DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(day, 30, SYSUTCDATETIME()), 1, SYSUTCDATETIME());

UPDATE dbo.Settings SET Value = N'true' WHERE [Key] IN (N'Sms.IsEnabled', N'SmsEnabled');
UPDATE dbo.Settings SET Value = N'Testing' WHERE [Key] IN (N'Sms.Provider', N'SmsProvider');
UPDATE dbo.Settings SET Value = N'e2e-testing-key' WHERE [Key] = N'Sms.ApiKey';
UPDATE dbo.Settings SET Value = N'1001' WHERE [Key] IN
    (N'Sms.OtpTemplateId', N'Sms.LoginOtpTemplateId', N'Sms.RegisterOtpTemplateId', N'Sms.ForgotPasswordTemplateId');
UPDATE dbo.Settings SET Value = N'1002' WHERE [Key] IN
    (N'Sms.NotificationTemplateId', N'Sms.OrderPaidTemplateId', N'Sms.OrderCompletedTemplateId',
     N'Sms.OrderStatusChangedTemplateId', N'Sms.GiftCodeDeliveredTemplateId', N'Sms.TicketReplyTemplateId',
     N'Sms.VerificationApprovedTemplateId', N'Sms.VerificationRejectedTemplateId', N'Sms.WalletTopUpSuccessTemplateId');
UPDATE dbo.Settings SET Value = N'0' WHERE [Key] = N'Sms.OtpResendCooldownSeconds';

IF NOT EXISTS (SELECT 1 FROM dbo.ProductTags WHERE Id = @TagId)
    INSERT dbo.ProductTags (Id, Title, Slug, Aliases, IsActive, CreatedAt)
    VALUES (@TagId, N'آزمون مرورگر', N'e2e-tag', N'e2e,تست مرورگر', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.ProductTagMappings WHERE ProductId = @ProductId AND TagId = @TagId)
    INSERT dbo.ProductTagMappings (ProductId, TagId) VALUES (@ProductId, @TagId);

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @ReviewUserId)
    INSERT dbo.Users (Id, FullName, Mobile, PasswordHash, Status, IsMobileConfirmed, CreatedAt)
    VALUES (@ReviewUserId, N'کاربر تأییدشده آزمون', N'09000000001', N'E2E-NOT-A-LOGIN-HASH', 1, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.ProductReviews WHERE Id = @ReviewId)
    INSERT dbo.ProductReviews
        (Id, ProductId, UserId, Title, Comment, Rating, IsApproved, IsRejected, IsBuyer, CreatedAt)
    VALUES
        (@ReviewId, @ProductId, @ReviewUserId, N'نظر واقعی تأییدشده', N'این نظر عمومی فقط برای آزمون داده ساختاریافته است.', 5, 1, 0, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.LegacyRedirects WHERE SourcePath = N'/e2e-old-product')
    INSERT dbo.LegacyRedirects (Id, SourcePath, DestinationPath, StatusCode, IsActive, CreatedAt)
    VALUES (NEWID(), N'/e2e-old-product', N'/product/e2e-seo-product', 301, 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.LegacyRedirects WHERE SourcePath = N'/e2e-gone-product')
    INSERT dbo.LegacyRedirects (Id, SourcePath, DestinationPath, StatusCode, IsActive, CreatedAt)
    VALUES (NEWID(), N'/e2e-gone-product', NULL, 410, 1, SYSUTCDATETIME());

PRINT N'E2E deterministic public fixture is ready.';
