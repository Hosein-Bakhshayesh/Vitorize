/*
  Customer-facing order references are allocated only after a successful payment.
  Existing order numbers are intentionally preserved; the first new paid order is vtrz-8000.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderNumberCounters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderNumberCounters
    (
        Id tinyint NOT NULL CONSTRAINT PK_OrderNumberCounters PRIMARY KEY,
        NextNumber bigint NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.OrderNumberCounters WHERE Id = 1)
BEGIN
    INSERT INTO dbo.OrderNumberCounters (Id, NextNumber)
    VALUES (1, 8000);
END;

COMMIT TRANSACTION;
