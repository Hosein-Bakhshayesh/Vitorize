SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Products carries filtered indexes, so an INSERT is refused unless this is ON. The other fixtures
-- already set it; this one never did, which went unnoticed while nothing applied it.
SET QUOTED_IDENTIFIER ON;

-- FIX-05 visual-only fixture. It is applied only to a disposable E2E database
-- after the standard Testing seed, and is never part of production deployment.
DECLARE @CategoryId uniqueidentifier = '31000000-0000-0000-0000-000000000001';
DECLARE @BrandId uniqueidentifier = '31000000-0000-0000-0000-000000000006';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000071';

DELETE FROM dbo.ProductInputFields WHERE ProductId = @ProductId;

-- Upserted rather than deleted and recreated: once a run has put this product in a cart or an
-- order, deleting it is refused, and the fixture has to stay repeatable on a database that has
-- already served a run.
INSERT dbo.Products
    (Id, CategoryId, BrandId, Title, Slug, ShortDescription, FullDescription, SeoTitle, SeoDescription,
     ThumbnailImagePath, ThumbnailAltText, ProductType, DeliveryType, BasePrice, CurrencyType,
     MinOrderQuantity, IsActive, IsFeatured, IsDeleted, CreatedAt)
SELECT @ProductId, @CategoryId, @BrandId, N'FIX-05 Visual Cart Product', N'e2e-fix05-visual-cart-product',
     N'Disposable visual fixture with every supported direction-sensitive input.', N'<p>FIX-05 disposable visual fixture.</p>',
     N'FIX-05 visual cart', N'Disposable E2E visual fixture.', N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',
     N'FIX-05 visual product', 1, 2, 75000, 2, 1, 1, 0, 0, SYSUTCDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId);
UPDATE dbo.Products
SET CategoryId = @CategoryId, BrandId = @BrandId, Title = N'FIX-05 Visual Cart Product',
    Slug = N'e2e-fix05-visual-cart-product', ProductType = 1, DeliveryType = 2, BasePrice = 75000,
    CurrencyType = 2, MinOrderQuantity = 1, IsActive = 1, IsDeleted = 0
WHERE Id = @ProductId;

-- Inventory lives on the SKU, so a Manual product that owns none is simply not purchasable and the
-- visual fixtures never get as far as rendering a cart. seed-e2e gives every non-Instant product a
-- stocked default SKU, but it runs before this fixture exists, so this one supplies its own.
INSERT dbo.ProductVariants
    (Id, ProductId, Title, Price, DiscountPrice, StockMode, StockQuantity, IsDefault, IsActive, SortOrder, CreatedAt)
SELECT '31000000-0000-0000-0000-000000000078', @ProductId, N'پیش' + NCHAR(8204) + N'فرض', 75000, NULL, 2, 250, 1, 1, 0, SYSUTCDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductVariants WHERE ProductId = @ProductId);
UPDATE dbo.ProductVariants SET StockMode = 2, StockQuantity = 250, IsActive = 1, IsDefault = 1
WHERE ProductId = @ProductId;

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
