/*
    F1 remediation - implicit default SKUs.

    Inventory, cart validation and paid-time consumption are SKU-scoped (ProductVariant), so a
    non-Instant product with zero variants was unsellable: its availability aggregated to 0 and
    no stock could be assigned. This script gives every such product exactly one implicit
    default variant. Instant products are excluded on purpose - their availability comes from
    gift codes, which may be product-scoped, and adding variants to them could disturb legacy
    allocation.

    StockQuantity is deliberately 0: unknown legacy stock must never become sellable
    automatically. Administrators set the real quantity after the upgrade.

    Idempotent: guarded per product; never touches products that already own any variant.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

INSERT dbo.ProductVariants
    (Id, ProductId, Title, Price, DiscountPrice, StockMode, StockQuantity,
     IsDefault, IsActive, SortOrder, CreatedAt)
SELECT
    NEWID(),
    p.Id,
    N'پیش' + NCHAR(8204) + N'فرض',   -- same marker title the application uses for implicit SKUs
    p.BasePrice,
    p.DiscountPrice,
    2,                                -- ProductVariantStockMode.Manual
    0,
    1,
    1,
    0,
    SYSUTCDATETIME()
FROM dbo.Products p
WHERE p.IsDeleted = 0
  AND p.DeliveryType <> 1            -- Instant stays gift-code driven and variant-optional
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductVariants v WHERE v.ProductId = p.Id);
