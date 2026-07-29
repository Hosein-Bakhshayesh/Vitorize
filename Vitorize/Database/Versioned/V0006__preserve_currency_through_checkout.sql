/*
   Currency must remain an immutable monetary snapshot once an item enters a
   cart.  Products may later change currency, so cart/order/payment amounts may
   never infer it from the current catalog during settlement or reconciliation.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.CartItems', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Orders', N'U') IS NULL OR
   OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Payments', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Products', N'U') IS NULL
    THROW 51600, 'Currency hardening requires CartItems, Orders, OrderItems, Payments, and Products.', 1;

/* Existing mixed-currency orders cannot be reconstructed safely because the
   former schema retained only numeric amounts. Stop for an explicit finance
   decision instead of silently assigning an incorrect historical currency. */
IF EXISTS
(
    SELECT 1
    FROM dbo.OrderItems oi
    INNER JOIN dbo.Products p ON p.Id = oi.ProductId
    GROUP BY oi.OrderId
    HAVING MIN(p.CurrencyType) <> MAX(p.CurrencyType)
)
    THROW 51601, 'Historical mixed-currency orders require a finance migration decision before deployment.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.CartItems', N'CurrencyType') IS NULL
    ALTER TABLE dbo.CartItems ADD CurrencyType tinyint NULL;
IF COL_LENGTH(N'dbo.OrderItems', N'CurrencyType') IS NULL
    ALTER TABLE dbo.OrderItems ADD CurrencyType tinyint NULL;
IF COL_LENGTH(N'dbo.Orders', N'CurrencyType') IS NULL
    ALTER TABLE dbo.Orders ADD CurrencyType tinyint NULL;
IF COL_LENGTH(N'dbo.Payments', N'CurrencyType') IS NULL
    ALTER TABLE dbo.Payments ADD CurrencyType tinyint NULL;

/* Separate the ALTER batch so SQL Server compiles the following backfill
   statements against the newly added columns. */
GO

UPDATE ci
SET CurrencyType = p.CurrencyType
FROM dbo.CartItems ci
INNER JOIN dbo.Products p ON p.Id = ci.ProductId
WHERE ci.CurrencyType IS NULL;

UPDATE oi
SET CurrencyType = p.CurrencyType
FROM dbo.OrderItems oi
INNER JOIN dbo.Products p ON p.Id = oi.ProductId
WHERE oi.CurrencyType IS NULL;

UPDATE o
SET CurrencyType = snapshot.CurrencyType
FROM dbo.Orders o
OUTER APPLY
(
    SELECT TOP (1) oi.CurrencyType
    FROM dbo.OrderItems oi
    WHERE oi.OrderId = o.Id
    ORDER BY oi.CreatedAt, oi.Id
) snapshot
WHERE o.CurrencyType IS NULL;

/* Empty legacy orders and orphaned historical payment rows were created by the
   original Toman-only checkout; retain that documented legacy unit explicitly. */
UPDATE dbo.Orders SET CurrencyType = 2 WHERE CurrencyType IS NULL;
UPDATE p SET CurrencyType = o.CurrencyType
FROM dbo.Payments p
INNER JOIN dbo.Orders o ON o.Id = p.OrderId
WHERE p.CurrencyType IS NULL;
UPDATE dbo.Payments SET CurrencyType = 2 WHERE CurrencyType IS NULL;

IF EXISTS (SELECT 1 FROM dbo.CartItems WHERE CurrencyType NOT IN (1, 2)) OR
   EXISTS (SELECT 1 FROM dbo.OrderItems WHERE CurrencyType NOT IN (1, 2)) OR
   EXISTS (SELECT 1 FROM dbo.Orders WHERE CurrencyType NOT IN (1, 2)) OR
   EXISTS (SELECT 1 FROM dbo.Payments WHERE CurrencyType NOT IN (1, 2))
    THROW 51602, 'CurrencyType contains an unsupported value.', 1;

ALTER TABLE dbo.CartItems ALTER COLUMN CurrencyType tinyint NOT NULL;
ALTER TABLE dbo.OrderItems ALTER COLUMN CurrencyType tinyint NOT NULL;
ALTER TABLE dbo.Orders ALTER COLUMN CurrencyType tinyint NOT NULL;
ALTER TABLE dbo.Payments ALTER COLUMN CurrencyType tinyint NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.CartItems') AND name = N'DF_CartItems_CurrencyType')
    ALTER TABLE dbo.CartItems ADD CONSTRAINT DF_CartItems_CurrencyType DEFAULT (2) FOR CurrencyType;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'DF_OrderItems_CurrencyType')
    ALTER TABLE dbo.OrderItems ADD CONSTRAINT DF_OrderItems_CurrencyType DEFAULT (2) FOR CurrencyType;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Orders') AND name = N'DF_Orders_CurrencyType')
    ALTER TABLE dbo.Orders ADD CONSTRAINT DF_Orders_CurrencyType DEFAULT (2) FOR CurrencyType;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Payments') AND name = N'DF_Payments_CurrencyType')
    ALTER TABLE dbo.Payments ADD CONSTRAINT DF_Payments_CurrencyType DEFAULT (2) FOR CurrencyType;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.CartItems') AND name = N'CK_CartItems_CurrencyType')
    ALTER TABLE dbo.CartItems ADD CONSTRAINT CK_CartItems_CurrencyType CHECK (CurrencyType IN (1, 2));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'CK_OrderItems_CurrencyType')
    ALTER TABLE dbo.OrderItems ADD CONSTRAINT CK_OrderItems_CurrencyType CHECK (CurrencyType IN (1, 2));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Orders') AND name = N'CK_Orders_CurrencyType')
    ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Orders_CurrencyType CHECK (CurrencyType IN (1, 2));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Payments') AND name = N'CK_Payments_CurrencyType')
    ALTER TABLE dbo.Payments ADD CONSTRAINT CK_Payments_CurrencyType CHECK (CurrencyType IN (1, 2));

COMMIT TRANSACTION;
