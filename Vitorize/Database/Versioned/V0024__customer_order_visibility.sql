/*
    Client batch 3.

    dbo.Orders.HiddenByCustomerAt — the customer may remove an abandoned order from their own list.

    An unpaid order that a customer walked away from used to stay in their panel forever with no
    action available. They can now cancel it and then hide it, but hiding must not destroy anything:
    order rows, payment attempts, the order number and the status history are financial and audit
    records, and Admin must keep seeing every one of them.

    So this is a per-customer VISIBILITY stamp, not a delete and not a soft-delete. Only the
    customer's own order list filters on it; every administrative query ignores it entirely. NULL
    (the default for all existing rows) means visible, so applying this migration changes nothing
    that any customer currently sees.

    Idempotent: guarded, so a second deployment is a no-op.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1 ---------------------------------------------------------------- column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'HiddenByCustomerAt')
BEGIN
    ALTER TABLE dbo.Orders ADD HiddenByCustomerAt datetime2(7) NULL;
END;

-- 2 ---------------------------------------------------------------- index
-- The customer order list is filtered by owner and now also by visibility. Statements below run
-- through sp_executesql because SQL Server resolves column names for the whole batch up front, so a
-- statement naming a column added in this same batch would fail to compile even though the ALTER
-- above has already run.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_Orders_UserId_HiddenByCustomerAt')
BEGIN
    EXEC sp_executesql N'
        CREATE NONCLUSTERED INDEX IX_Orders_UserId_HiddenByCustomerAt
            ON dbo.Orders (UserId, HiddenByCustomerAt) INCLUDE (CreatedAt);';
END;

-- 3 ---------------------------------------------------------------- verification
DECLARE @errors nvarchar(max) = N'';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'HiddenByCustomerAt')
    SET @errors = @errors + N'Orders.HiddenByCustomerAt missing. ';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_Orders_UserId_HiddenByCustomerAt')
    SET @errors = @errors + N'IX_Orders_UserId_HiddenByCustomerAt missing. ';

-- The column must be nullable: existing orders have to stay visible.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'HiddenByCustomerAt' AND is_nullable = 0)
    SET @errors = @errors + N'Orders.HiddenByCustomerAt must be nullable. ';

-- No existing order may have been hidden as a side effect of this migration.
EXEC sp_executesql N'
    IF EXISTS (SELECT 1 FROM dbo.Orders WHERE HiddenByCustomerAt IS NOT NULL AND UpdatedAt IS NULL)
        THROW 51024, N''Migration hid an order that was never touched by a customer.'', 1;';

IF @errors <> N''
    THROW 51024, @errors, 1;
