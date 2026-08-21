/*
    Client batch 2.

    1) dbo.ProductCategories - a product may belong to several categories

       Category membership used to live in the single column Products.CategoryId, so a product could
       sit in exactly one category. This adds the real relationship.

       Products.CategoryId is KEPT and becomes the explicit PRIMARY category: it is what the
       breadcrumb, canonical URL and SEO metadata use, and every product must have exactly one. To
       avoid two competing answers to "which products are in this category", the join table always
       contains the primary category as a member, and all category filtering reads the join table
       only. The backfill below establishes that invariant for existing data.

       ON DELETE CASCADE on ProductId: removing a product removes its memberships. Deleting a
       category is deliberately NOT cascaded here - the existing FK from Products.CategoryId already
       prevents deleting a category that is still a product's primary, and silently dropping
       memberships would hide that.

    2) StorefrontPersianFont default

       Vazirmatn is the application's default UI face. The original seed (V0008) shipped 'Peyda',
       which meant a fresh install rendered the storefront in a different family from the admin
       panel. Only rows that still hold that original seeded value are moved; an administrator who
       has deliberately chosen a font keeps it.

    Idempotent: every step is guarded, so a second deployment is a no-op.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1 ---------------------------------------------------------------- product ↔ category
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.ProductCategories'))
BEGIN
    CREATE TABLE dbo.ProductCategories
    (
        ProductId  uniqueidentifier NOT NULL,
        CategoryId uniqueidentifier NOT NULL,
        CreatedAt  datetime2(7)     NOT NULL CONSTRAINT DF_ProductCategories_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ProductCategories PRIMARY KEY CLUSTERED (ProductId, CategoryId),
        CONSTRAINT FK_ProductCategories_Products   FOREIGN KEY (ProductId)  REFERENCES dbo.Products (Id)   ON DELETE CASCADE,
        CONSTRAINT FK_ProductCategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id)
    );
END;

-- Category listing pages read by category, so the reverse direction gets its own index.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ProductCategories') AND name = N'IX_ProductCategories_CategoryId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductCategories_CategoryId
        ON dbo.ProductCategories (CategoryId) INCLUDE (ProductId);
END;

-- Backfill: every existing product keeps the category it already had, now as a membership row.
INSERT dbo.ProductCategories (ProductId, CategoryId, CreatedAt)
SELECT p.Id, p.CategoryId, SYSUTCDATETIME()
FROM dbo.Products p
WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProductCategories pc
        WHERE pc.ProductId = p.Id AND pc.CategoryId = p.CategoryId);

-- 2 ---------------------------------------------------------------- default storefront face
UPDATE dbo.Settings
SET    [Value] = N'Vazirmatn'
WHERE  [Key] = N'StorefrontPersianFont'
  AND  [Value] = N'Peyda';

-- 3 ---------------------------------------------------------------- verification
DECLARE @errors nvarchar(max) = N'';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.ProductCategories'))
    SET @errors = @errors + N'ProductCategories table missing. ';

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_ProductCategories')
    SET @errors = @errors + N'PK_ProductCategories missing. ';

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductCategories_Products')
    SET @errors = @errors + N'FK_ProductCategories_Products missing. ';

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductCategories_Categories')
    SET @errors = @errors + N'FK_ProductCategories_Categories missing. ';

-- Every product must still be reachable through its original category.
IF EXISTS (
    SELECT 1 FROM dbo.Products p
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProductCategories pc
        WHERE pc.ProductId = p.Id AND pc.CategoryId = p.CategoryId))
    SET @errors = @errors + N'Backfill missed at least one product primary category. ';

IF @errors <> N''
    THROW 51023, @errors, 1;
