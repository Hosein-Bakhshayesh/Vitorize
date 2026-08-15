/* FIX-09 Phase 3A: versioned customer-side document redaction configuration. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.KycPolicyDocumentRequirements', N'RedactionMode') IS NULL
    ALTER TABLE dbo.KycPolicyDocumentRequirements
        ADD RedactionMode tinyint NOT NULL
            CONSTRAINT DF_KycPolicyDocumentRequirements_RedactionMode DEFAULT (0);

IF COL_LENGTH(N'dbo.KycPolicyDocumentRequirements', N'RedactionInstructions') IS NULL
    ALTER TABLE dbo.KycPolicyDocumentRequirements
        ADD RedactionInstructions nvarchar(1000) NULL;

-- SQL Server resolves check-constraint column names during batch compilation;
-- run it after the ALTER batches so a fresh V0012 upgrade can see the columns.
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.KycPolicyDocumentRequirements')
      AND name = N'CK_KycPolicyDocumentRequirements_RedactionMode'
)
    ALTER TABLE dbo.KycPolicyDocumentRequirements
        ADD CONSTRAINT CK_KycPolicyDocumentRequirements_RedactionMode
        CHECK (RedactionMode BETWEEN 0 AND 2);

COMMIT TRANSACTION;
