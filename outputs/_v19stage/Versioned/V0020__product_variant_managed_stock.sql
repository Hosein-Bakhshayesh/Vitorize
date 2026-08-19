/*
    FEATURE — managed inventory for non-Instant delivery.

    ProductVariant is the purchasable SKU (CartItems, OrderItems, GiftCodes, GiftCodeBatches and
    GiftCodeReservations are all variant-scoped), so inventory is tracked there and never on Product.

    Instant delivery (DeliveryType = 1) keeps deriving availability from eligible gift codes and is
    deliberately left untouched by this script. Manual (2) and SupportRequired (3) move to managed
    quantity.

    Backfill is intentionally conservative: unknown legacy inventory becomes 0, never sellable by
    accident. No variant is granted stock it cannot prove it has.

    Idempotent and safe to re-run.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*---------------------------------------------------------------------------
  1. Column. Non-negativity is enforced in the database so the sale-time
     conditional UPDATE can never drive inventory below zero, whatever the
     calling code does.
---------------------------------------------------------------------------*/
IF COL_LENGTH('dbo.ProductVariants', 'StockQuantity') IS NULL
BEGIN
    ALTER TABLE dbo.ProductVariants
        ADD StockQuantity int NOT NULL CONSTRAINT DF_ProductVariants_StockQuantity DEFAULT (0);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ProductVariants_StockQuantity_NonNegative')
BEGIN
    ALTER TABLE dbo.ProductVariants WITH CHECK
        ADD CONSTRAINT CK_ProductVariants_StockQuantity_NonNegative CHECK (StockQuantity >= 0);
END;
GO

/*---------------------------------------------------------------------------
  2. Align StockMode with delivery type.

     StockMode values: 1 = GiftCode, 2 = Manual, 3 = Unlimited.

     Rows changed:
       - variants of Instant products      -> StockMode = 1 (GiftCode)
       - variants of Manual/SupportRequired-> StockMode = 2 (Manual)

     The previous storefront projection returned a hard-coded 999999 for Manual delivery and counted
     gift codes for SupportRequired, so neither had trustworthy per-variant inventory. Forcing
     Manual mode with quantity 0 makes the state explicit: an administrator must set real stock
     before these SKUs sell again. That is the safe direction — the alternative would silently list
     products whose true inventory nobody knows.
---------------------------------------------------------------------------*/
UPDATE v
SET    v.StockMode = 1
FROM   dbo.ProductVariants v
JOIN   dbo.Products p ON p.Id = v.ProductId
WHERE  p.DeliveryType = 1
  AND  v.StockMode <> 1;

UPDATE v
SET    v.StockMode = 2,
       v.StockQuantity = 0
FROM   dbo.ProductVariants v
JOIN   dbo.Products p ON p.Id = v.ProductId
WHERE  p.DeliveryType IN (2, 3)
  AND  v.StockMode <> 2;
GO

/*---------------------------------------------------------------------------
  3. Supporting index for availability lookups on managed-stock variants.
---------------------------------------------------------------------------*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductVariants_StockMode_StockQuantity' AND object_id = OBJECT_ID('dbo.ProductVariants'))
BEGIN
    CREATE INDEX IX_ProductVariants_StockMode_StockQuantity
        ON dbo.ProductVariants (StockMode, StockQuantity)
        INCLUDE (ProductId, IsActive);
END;
GO
