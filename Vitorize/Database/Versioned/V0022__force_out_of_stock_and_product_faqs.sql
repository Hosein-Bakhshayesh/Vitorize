/*
    Product availability override + product-scoped FAQ entries.

    1) dbo.Products.ForceOutOfStock

       An administrator needs to take a product off sale without destroying the inventory that
       will still be there when they put it back. Setting StockQuantity to 0 would lose the
       quantity and, for Instant products, would mean deleting gift codes - so availability gets
       its own explicit column instead. It is the highest-priority term in the availability rule:
       when set, the product is unavailable no matter how much stock or how many gift codes exist.

       Backfilled to 0 for every existing row, so behaviour after this upgrade is identical to
       behaviour before it until an administrator changes something.

    2) dbo.FAQs.ProductId

       Product FAQs share the site-wide FAQ table, told apart by ProductId: NULL is the site-wide
       FAQ, a value scopes the entry to one product. Sharing the entity keeps one sanitisation and
       ordering path, so a product answer can never be rendered through a laxer route than a
       global one. Existing rows stay NULL and therefore stay global.

       ON DELETE CASCADE stops a hard-deleted product leaving orphaned answers behind. Products are
       normally soft-deleted (IsDeleted), which the application filters on; the cascade covers the
       hard-delete path that would otherwise violate the foreign key.

    Every statement that touches a column this script adds runs through EXEC: SQL Server resolves
    names for a whole batch up front, so a direct reference to a just-added column would fail to
    compile even though the ALTER precedes it.

    Idempotent: every change is guarded, so a second deployment is a no-op.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1 ---------------------------------------------------------------- availability override
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'ForceOutOfStock')
BEGIN
    EXEC sp_executesql N'
        ALTER TABLE dbo.Products
            ADD ForceOutOfStock bit NOT NULL
                CONSTRAINT DF_Products_ForceOutOfStock DEFAULT (0);';
END;

-- Only products taken off sale are ever matched, so a filtered index keeps the override cheap to
-- evaluate without widening the existing catalogue indexes.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_ForceOutOfStock')
BEGIN
    EXEC sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_Products_ForceOutOfStock
            ON dbo.Products (ForceOutOfStock)
            WHERE ForceOutOfStock = 1;';
END;

-- 2 ---------------------------------------------------------------- product-scoped FAQs
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.FAQs') AND name = N'ProductId')
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.FAQs ADD ProductId uniqueidentifier NULL;';
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.FAQs') AND name = N'FK_FAQs_Products_ProductId')
BEGIN
    EXEC sp_executesql N'
        ALTER TABLE dbo.FAQs WITH CHECK
            ADD CONSTRAINT FK_FAQs_Products_ProductId
            FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id) ON DELETE CASCADE;';
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FAQs') AND name = N'IX_FAQs_Product_Active_Sort')
BEGIN
    EXEC sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_FAQs_Product_Active_Sort
            ON dbo.FAQs (ProductId, IsActive, SortOrder);';
END;

-- 3 ---------------------------------------------------------------- verification
DECLARE @errors nvarchar(max) = N'';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'ForceOutOfStock')
    SET @errors = @errors + N'Products.ForceOutOfStock missing. ';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.FAQs') AND name = N'ProductId')
    SET @errors = @errors + N'FAQs.ProductId missing. ';

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.FAQs') AND name = N'FK_FAQs_Products_ProductId')
    SET @errors = @errors + N'FK_FAQs_Products_ProductId missing. ';

-- This upgrade must not have taken a single existing product off sale.
DECLARE @forced int;
EXEC sp_executesql
    N'SELECT @count = COUNT(*) FROM dbo.Products WHERE ForceOutOfStock <> 0;',
    N'@count int OUTPUT', @count = @forced OUTPUT;
IF @forced <> 0
    SET @errors = @errors + N'ForceOutOfStock backfill left products off sale. ';

IF @errors <> N''
    THROW 51022, @errors, 1;
