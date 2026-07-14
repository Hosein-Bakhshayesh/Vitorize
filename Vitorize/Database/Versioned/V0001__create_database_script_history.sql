/*
    Vitorize database deployment ledger.
    Database-First / SQL Server / idempotent.

    This table stores successful immutable script executions only. The deployment
    runner compares the current SHA-256 hash with ScriptHash before deciding to
    skip an applied script. A mismatch is a deployment error, never a silent skip.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DatabaseScriptHistory
        (
            Id bigint IDENTITY(1,1) NOT NULL,
            ScriptName nvarchar(260) NOT NULL,
            ScriptVersion nvarchar(50) NOT NULL,
            ScriptHash char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
            AppliedAt datetime2(7) NOT NULL
                CONSTRAINT DF_DatabaseScriptHistory_AppliedAt DEFAULT SYSUTCDATETIME(),
            AppliedBy nvarchar(128) NOT NULL
                CONSTRAINT DF_DatabaseScriptHistory_AppliedBy DEFAULT ORIGINAL_LOGIN(),
            Environment nvarchar(50) NOT NULL,
            Success bit NOT NULL
                CONSTRAINT DF_DatabaseScriptHistory_Success DEFAULT (1),
            Notes nvarchar(1000) NULL,
            CONSTRAINT PK_DatabaseScriptHistory PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT CK_DatabaseScriptHistory_Hash
                CHECK (LEN(ScriptHash) = 64 AND ScriptHash NOT LIKE '%[^0-9a-f]%'),
            CONSTRAINT CK_DatabaseScriptHistory_Success CHECK (Success = 1)
        );
    END;

    IF COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptName') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptVersion') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptHash') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'AppliedAt') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'AppliedBy') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'Environment') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'Success') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'Notes') IS NULL
    BEGIN
        THROW 51001, 'dbo.DatabaseScriptHistory exists but is incompatible with the Vitorize ledger contract.', 1;
    END;

    IF EXISTS
    (
        SELECT ScriptName
        FROM dbo.DatabaseScriptHistory
        GROUP BY ScriptName
        HAVING COUNT(*) > 1
    )
        THROW 51002, 'Duplicate ScriptName rows exist in dbo.DatabaseScriptHistory.', 1;

    IF EXISTS
    (
        SELECT ScriptVersion
        FROM dbo.DatabaseScriptHistory
        GROUP BY ScriptVersion
        HAVING COUNT(*) > 1
    )
        THROW 51003, 'Duplicate ScriptVersion rows exist in dbo.DatabaseScriptHistory.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.DatabaseScriptHistory')
          AND name = N'UX_DatabaseScriptHistory_ScriptName'
    )
        CREATE UNIQUE INDEX UX_DatabaseScriptHistory_ScriptName
            ON dbo.DatabaseScriptHistory(ScriptName);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.DatabaseScriptHistory')
          AND name = N'UX_DatabaseScriptHistory_ScriptVersion'
    )
        CREATE UNIQUE INDEX UX_DatabaseScriptHistory_ScriptVersion
            ON dbo.DatabaseScriptHistory(ScriptVersion);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER TRIGGER dbo.TR_DatabaseScriptHistory_Immutable
ON dbo.DatabaseScriptHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51004, 'Database script history is immutable. UPDATE and DELETE are not permitted.', 1;
END;
GO

