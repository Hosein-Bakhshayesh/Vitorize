SET NOCOUNT ON;
SET XACT_ABORT ON;

-- FIX-05 visual-only fixture. It is applied only to a disposable E2E database
-- after the standard Testing seed, and is never part of production deployment.
DECLARE @CategoryId uniqueidentifier = '31000000-0000-0000-0000-000000000001';
DECLARE @BrandId uniqueidentifier = '31000000-0000-0000-0000-000000000006';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000071';

DELETE FROM dbo.ProductInputFields WHERE ProductId = @ProductId;
DELETE FROM dbo.Products WHERE Id = @ProductId;

INSERT dbo.Products
    (Id, CategoryId, BrandId, Title, Slug, ShortDescription, FullDescription, SeoTitle, SeoDescription,
     ThumbnailImagePath, ThumbnailAltText, ProductType, DeliveryType, BasePrice, CurrencyType,
     MinOrderQuantity, IsActive, IsFeatured, IsDeleted, CreatedAt)
VALUES
    (@ProductId, @CategoryId, @BrandId, N'FIX-05 Visual Cart Product', N'e2e-fix05-visual-cart-product',
     N'Disposable visual fixture with every supported direction-sensitive input.', N'<p>FIX-05 disposable visual fixture.</p>',
     N'FIX-05 visual cart', N'Disposable E2E visual fixture.', N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',
     N'FIX-05 visual product', 1, 2, 75000, 2, 1, 1, 0, 0, SYSUTCDATETIME());

INSERT dbo.ProductInputFields
    (Id, ProductId, [Key], Label, [Description], Placeholder, FieldType, IsRequired,
     IsSensitive, RequiresConfirmation, DisplayStage, SortOrder, IsActive, OptionsJson, CreatedAt)
VALUES
    ('31000000-0000-0000-0000-000000000072', @ProductId, 'persian_text', N'متن فارسی', N'یک مقدار فارسی لازم است.', N'متن آزمایشی', 1, 1, 0, 0, 2, 10, 1, NULL, SYSUTCDATETIME()),
    ('31000000-0000-0000-0000-000000000073', @ProductId, 'email', N'Email', N'Email test value.', N'customer@example.test', 2, 1, 0, 0, 2, 20, 1, NULL, SYSUTCDATETIME()),
    ('31000000-0000-0000-0000-000000000074', @ProductId, 'url', N'URL', N'URL test value.', N'https://example.test/account', 10, 0, 0, 0, 2, 30, 1, NULL, SYSUTCDATETIME()),
    ('31000000-0000-0000-0000-000000000075', @ProductId, 'phone', N'Phone', N'Phone test value.', N'+491234567890', 3, 0, 0, 0, 2, 40, 1, NULL, SYSUTCDATETIME()),
    ('31000000-0000-0000-0000-000000000076', @ProductId, 'region', N'Region', N'Select a valid option.', N'', 6, 1, 0, 0, 2, 50, 1, N'["north","south"]', SYSUTCDATETIME()),
    ('31000000-0000-0000-0000-000000000077', @ProductId, 'terms', N'شرایط', N'تایید شرایط الزامی است.', N'شرایط آزمایشی را می‌پذیرم', 8, 1, 0, 0, 2, 60, 1, NULL, SYSUTCDATETIME());
