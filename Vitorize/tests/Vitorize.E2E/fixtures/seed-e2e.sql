SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @CategoryId uniqueidentifier = '31000000-0000-0000-0000-000000000001';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000002';
DECLARE @TagId uniqueidentifier = '31000000-0000-0000-0000-000000000003';
DECLARE @ReviewUserId uniqueidentifier = '31000000-0000-0000-0000-000000000004';
DECLARE @ReviewId uniqueidentifier = '31000000-0000-0000-0000-000000000005';

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

PRINT N'E2E deterministic public fixture is ready.';
