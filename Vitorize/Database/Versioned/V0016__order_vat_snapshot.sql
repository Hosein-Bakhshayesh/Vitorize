/*
  FIX-13 (Client Issue #11): purchase-time VAT snapshot on dbo.Orders plus the global VAT
  configuration keys.

  Historical rows keep the column defaults (disabled, zero rate, zero tax), so no existing order
  receives retroactive VAT and no existing total changes. There is deliberately no backfill.
  The seeded settings default to VAT disabled, so deploying FIX-13 does not change any price until
  an administrator explicitly enables it.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Orders', N'VatEnabled') IS NULL
    ALTER TABLE dbo.Orders ADD VatEnabled bit NOT NULL CONSTRAINT DF_Orders_VatEnabled DEFAULT (0);
IF COL_LENGTH(N'dbo.Orders', N'VatRatePercent') IS NULL
    ALTER TABLE dbo.Orders ADD VatRatePercent decimal(5,2) NOT NULL CONSTRAINT DF_Orders_VatRatePercent DEFAULT (0);
IF COL_LENGTH(N'dbo.Orders', N'VatCalculationMode') IS NULL
    ALTER TABLE dbo.Orders ADD VatCalculationMode tinyint NOT NULL CONSTRAINT DF_Orders_VatCalculationMode DEFAULT (1);
IF COL_LENGTH(N'dbo.Orders', N'VatAmount') IS NULL
    ALTER TABLE dbo.Orders ADD VatAmount decimal(18,2) NOT NULL CONSTRAINT DF_Orders_VatAmount DEFAULT (0);
IF COL_LENGTH(N'dbo.Orders', N'VatTaxableAmount') IS NULL
    ALTER TABLE dbo.Orders ADD VatTaxableAmount decimal(18,2) NOT NULL CONSTRAINT DF_Orders_VatTaxableAmount DEFAULT (0);

-- SQL Server binds names for the whole batch before executing ALTER TABLE. Start a new batch so the
-- constraint below can reference the columns added above.
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Orders_VatSnapshot')
    ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Orders_VatSnapshot CHECK
    (VatRatePercent >= 0 AND VatRatePercent <= 100 AND
     VatAmount >= 0 AND VatTaxableAmount >= 0 AND
     VatCalculationMode IN (1, 2) AND
     (VatEnabled = 1 OR (VatAmount = 0 AND VatRatePercent = 0)));

-- Idempotent VAT configuration seed. Administrator-customised values are never overwritten.
DECLARE @VatSettings TABLE
(
    [Key] nvarchar(200) NOT NULL PRIMARY KEY,
    [Value] nvarchar(max) NOT NULL,
    GroupName nvarchar(100) NOT NULL,
    ValueType nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL
);

INSERT @VatSettings ([Key], [Value], GroupName, ValueType, [Description]) VALUES
    (N'VatEnabled', N'false', N'Tax', N'bool', N'فعال بودن مالیات بر ارزش افزوده'),
    (N'VatRatePercent', N'0', N'Tax', N'decimal', N'نرخ مالیات بر ارزش افزوده (درصد)'),
    (N'VatCalculationMode', N'BeforeDiscount', N'Tax', N'vatmode', N'نحوه محاسبه مالیات بر ارزش افزوده');

INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), seed.[Key], seed.[Value], seed.GroupName, seed.ValueType, seed.[Description], SYSUTCDATETIME()
FROM @VatSettings seed
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Settings existing
    WHERE existing.[Key] = seed.[Key]
);

COMMIT TRANSACTION;
