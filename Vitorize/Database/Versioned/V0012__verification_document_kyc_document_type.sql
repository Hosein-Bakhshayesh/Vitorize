/* FIX-09 Phase 2D: explicit, non-guessing mapping from uploaded documents
   to the versioned policy document type they satisfy. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.VerificationDocuments', N'KycDocumentTypeId') IS NULL
    ALTER TABLE dbo.VerificationDocuments ADD KycDocumentTypeId uniqueidentifier NULL;

-- SQL Server binds names per batch; the index below references the newly
-- added column, so it must execute in the following batch.
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VerificationDocuments') AND name = N'IX_VerificationDocuments_KycDocumentTypeId')
    CREATE INDEX IX_VerificationDocuments_KycDocumentTypeId ON dbo.VerificationDocuments(KycDocumentTypeId) WHERE KycDocumentTypeId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VerificationDocuments_KycDocumentTypes')
    ALTER TABLE dbo.VerificationDocuments ADD CONSTRAINT FK_VerificationDocuments_KycDocumentTypes
        FOREIGN KEY (KycDocumentTypeId) REFERENCES dbo.KycDocumentTypes(Id);

-- No backfill: legacy byte-only uploads cannot be safely mapped to arbitrary
-- admin-configured policy document types.
COMMIT TRANSACTION;
