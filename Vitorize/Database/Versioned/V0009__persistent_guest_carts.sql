/*
  FIX-03: persistent cart ownership for authenticated users and anonymous guests.
  The guest bearer capability is stored only as a SHA-256 hash.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Carts', 'GuestTokenHash') IS NULL
    ALTER TABLE dbo.Carts ADD GuestTokenHash varchar(64) NULL;
IF COL_LENGTH('dbo.Carts', 'LastActivityAt') IS NULL
    ALTER TABLE dbo.Carts ADD LastActivityAt datetime2 NULL;

-- SQL Server compiles a batch before conditional DDL runs. The next batch may safely
-- reference the two new columns on both fresh and already-upgraded databases.
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Carts') AND name = 'UserId' AND is_nullable = 0)
    ALTER TABLE dbo.Carts ALTER COLUMN UserId uniqueidentifier NULL;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Carts') AND name = 'UX_Carts_UserId')
    DROP INDEX UX_Carts_UserId ON dbo.Carts;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Carts') AND name = 'UX_Carts_UserId')
    CREATE UNIQUE INDEX UX_Carts_UserId ON dbo.Carts(UserId) WHERE UserId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Carts') AND name = 'UX_Carts_GuestTokenHash')
    CREATE UNIQUE INDEX UX_Carts_GuestTokenHash ON dbo.Carts(GuestTokenHash) WHERE GuestTokenHash IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Carts') AND name = 'IX_Carts_GuestLastActivityAt')
    CREATE INDEX IX_Carts_GuestLastActivityAt ON dbo.Carts(LastActivityAt) WHERE GuestTokenHash IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.Carts') AND name = 'CK_Carts_ExactlyOneOwner')
BEGIN
    ALTER TABLE dbo.Carts WITH CHECK ADD CONSTRAINT CK_Carts_ExactlyOneOwner CHECK
    ((UserId IS NOT NULL AND GuestTokenHash IS NULL) OR (UserId IS NULL AND GuestTokenHash IS NOT NULL));
    ALTER TABLE dbo.Carts CHECK CONSTRAINT CK_Carts_ExactlyOneOwner;
END

COMMIT TRANSACTION;
