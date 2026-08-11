/*
  FIX-09 Phase 1: versioned KYC policy configuration and immutable order-item
  evaluation snapshots.  Existing RequiresVerification remains as a compatibility
  projection; product/order behaviour now uses the versioned fields below.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.KycPolicies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KycPolicies
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_KycPolicies PRIMARY KEY,
        Code nvarchar(100) NOT NULL,
        Name nvarchar(250) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_KycPolicies_IsActive DEFAULT (1),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_KycPolicies_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2 NULL,
        CONSTRAINT UX_KycPolicies_Code UNIQUE (Code)
    );
END;

IF OBJECT_ID(N'dbo.KycPolicyVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KycPolicyVersions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_KycPolicyVersions PRIMARY KEY,
        KycPolicyId uniqueidentifier NOT NULL,
        Version int NOT NULL,
        Status tinyint NOT NULL CONSTRAINT DF_KycPolicyVersions_Status DEFAULT (1),
        CustomerTitle nvarchar(250) NOT NULL,
        CustomerInstructions nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_KycPolicyVersions_CreatedAt DEFAULT (sysutcdatetime()),
        PublishedAt datetime2 NULL,
        CONSTRAINT UX_KycPolicyVersions_Policy_Version UNIQUE (KycPolicyId, Version),
        CONSTRAINT CK_KycPolicyVersions_Status CHECK (Status IN (1, 2)),
        CONSTRAINT FK_KycPolicyVersions_KycPolicies FOREIGN KEY (KycPolicyId) REFERENCES dbo.KycPolicies(Id)
    );
END;

IF OBJECT_ID(N'dbo.KycDocumentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KycDocumentTypes
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_KycDocumentTypes PRIMARY KEY,
        Code nvarchar(100) NOT NULL,
        Title nvarchar(250) NOT NULL,
        Description nvarchar(1000) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_KycDocumentTypes_IsActive DEFAULT (1),
        AllowedExtensions nvarchar(250) NOT NULL CONSTRAINT DF_KycDocumentTypes_AllowedExtensions DEFAULT (N'jpg,jpeg,png,webp'),
        MaxFileSizeBytes bigint NOT NULL CONSTRAINT DF_KycDocumentTypes_MaxFileSizeBytes DEFAULT (5242880),
        SortOrder int NOT NULL CONSTRAINT DF_KycDocumentTypes_SortOrder DEFAULT (0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_KycDocumentTypes_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2 NULL,
        CONSTRAINT UX_KycDocumentTypes_Code UNIQUE (Code),
        CONSTRAINT CK_KycDocumentTypes_MaxFileSizeBytes CHECK (MaxFileSizeBytes > 0)
    );
END;

IF OBJECT_ID(N'dbo.KycPolicyDocumentRequirements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KycPolicyDocumentRequirements
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_KycPolicyDocumentRequirements PRIMARY KEY,
        KycPolicyVersionId uniqueidentifier NOT NULL,
        KycDocumentTypeId uniqueidentifier NOT NULL,
        IsRequired bit NOT NULL CONSTRAINT DF_KycPolicyDocumentRequirements_IsRequired DEFAULT (1),
        SortOrder int NOT NULL CONSTRAINT DF_KycPolicyDocumentRequirements_SortOrder DEFAULT (0),
        Instructions nvarchar(1000) NULL,
        CONSTRAINT UX_KycPolicyDocumentRequirements_Version_Document UNIQUE (KycPolicyVersionId, KycDocumentTypeId),
        CONSTRAINT FK_KycPolicyDocumentRequirements_KycPolicyVersions FOREIGN KEY (KycPolicyVersionId) REFERENCES dbo.KycPolicyVersions(Id),
        CONSTRAINT FK_KycPolicyDocumentRequirements_KycDocumentTypes FOREIGN KEY (KycDocumentTypeId) REFERENCES dbo.KycDocumentTypes(Id)
    );
END;

IF COL_LENGTH(N'dbo.Products', N'KycRequirementMode') IS NULL
    ALTER TABLE dbo.Products ADD KycRequirementMode tinyint NOT NULL CONSTRAINT DF_Products_KycRequirementMode DEFAULT (0);
IF COL_LENGTH(N'dbo.Products', N'KycThresholdAmount') IS NULL
    ALTER TABLE dbo.Products ADD KycThresholdAmount decimal(18,2) NULL;
IF COL_LENGTH(N'dbo.Products', N'KycPolicyVersionId') IS NULL
    ALTER TABLE dbo.Products ADD KycPolicyVersionId uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.OrderItems', N'KycRequirementMode') IS NULL
    ALTER TABLE dbo.OrderItems ADD KycRequirementMode tinyint NOT NULL CONSTRAINT DF_OrderItems_KycRequirementMode DEFAULT (0);
IF COL_LENGTH(N'dbo.OrderItems', N'KycThresholdAmount') IS NULL
    ALTER TABLE dbo.OrderItems ADD KycThresholdAmount decimal(18,2) NULL;
IF COL_LENGTH(N'dbo.OrderItems', N'KycEvaluatedAmount') IS NULL
    ALTER TABLE dbo.OrderItems ADD KycEvaluatedAmount decimal(18,2) NOT NULL CONSTRAINT DF_OrderItems_KycEvaluatedAmount DEFAULT (0);
IF COL_LENGTH(N'dbo.OrderItems', N'KycPolicyVersionId') IS NULL
    ALTER TABLE dbo.OrderItems ADD KycPolicyVersionId uniqueidentifier NULL;

-- SQL Server binds names for the entire batch before executing ALTER TABLE.
-- Start a new batch so the backfill below can safely reference these columns.
GO

DECLARE @LegacyPolicyId uniqueidentifier = (SELECT Id FROM dbo.KycPolicies WHERE Code = N'legacy-profile-verification');
IF @LegacyPolicyId IS NULL
BEGIN
    SET @LegacyPolicyId = NEWID();
    INSERT dbo.KycPolicies (Id, Code, Name, IsActive, CreatedAt)
    VALUES (@LegacyPolicyId, N'legacy-profile-verification', N'احراز هویت پروفایل (سیاست انتقالی)', 1, sysutcdatetime());
END;

DECLARE @LegacyPolicyVersionId uniqueidentifier = (SELECT Id FROM dbo.KycPolicyVersions WHERE KycPolicyId = @LegacyPolicyId AND Version = 1);
IF @LegacyPolicyVersionId IS NULL
BEGIN
    SET @LegacyPolicyVersionId = NEWID();
    INSERT dbo.KycPolicyVersions (Id, KycPolicyId, Version, Status, CustomerTitle, CustomerInstructions, CreatedAt, PublishedAt)
    VALUES (@LegacyPolicyVersionId, @LegacyPolicyId, 1, 2, N'احراز هویت لازم است', N'برای ادامه خرید، تأیید شماره همراه و احراز هویت حساب خود را تکمیل کنید.', sysutcdatetime(), sysutcdatetime());
END;

UPDATE dbo.Products
SET KycRequirementMode = CASE WHEN RequiresVerification = 1 THEN 1 ELSE 0 END,
    KycThresholdAmount = NULL,
    KycPolicyVersionId = CASE WHEN RequiresVerification = 1 THEN @LegacyPolicyVersionId ELSE NULL END
WHERE KycPolicyVersionId IS NULL;

UPDATE dbo.OrderItems
SET KycRequirementMode = CASE WHEN RequiresVerification = 1 THEN 1 ELSE 0 END,
    KycThresholdAmount = NULL,
    KycEvaluatedAmount = TotalPrice,
    KycPolicyVersionId = CASE WHEN RequiresVerification = 1 THEN @LegacyPolicyVersionId ELSE NULL END
WHERE KycPolicyVersionId IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_KycPolicyVersionId')
    CREATE INDEX IX_Products_KycPolicyVersionId ON dbo.Products(KycPolicyVersionId) WHERE KycPolicyVersionId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'IX_OrderItems_KycPolicyVersionId')
    CREATE INDEX IX_OrderItems_KycPolicyVersionId ON dbo.OrderItems(KycPolicyVersionId) WHERE KycPolicyVersionId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_KycPolicyVersions')
    ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_KycPolicyVersions FOREIGN KEY (KycPolicyVersionId) REFERENCES dbo.KycPolicyVersions(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItems_KycPolicyVersions')
    ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_KycPolicyVersions FOREIGN KEY (KycPolicyVersionId) REFERENCES dbo.KycPolicyVersions(Id);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_KycConfiguration')
    ALTER TABLE dbo.Products ADD CONSTRAINT CK_Products_KycConfiguration CHECK
    ((KycRequirementMode = 0 AND KycThresholdAmount IS NULL AND KycPolicyVersionId IS NULL) OR
     (KycRequirementMode = 1 AND KycThresholdAmount IS NULL AND KycPolicyVersionId IS NOT NULL) OR
     (KycRequirementMode = 2 AND KycThresholdAmount > 0 AND KycPolicyVersionId IS NOT NULL));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_OrderItems_KycSnapshot')
    ALTER TABLE dbo.OrderItems ADD CONSTRAINT CK_OrderItems_KycSnapshot CHECK
    (KycRequirementMode IN (0,1,2) AND KycEvaluatedAmount >= 0 AND
     ((KycRequirementMode = 0 AND KycThresholdAmount IS NULL AND KycPolicyVersionId IS NULL) OR
      (KycRequirementMode = 1 AND KycThresholdAmount IS NULL AND KycPolicyVersionId IS NOT NULL) OR
      (KycRequirementMode = 2 AND KycThresholdAmount > 0 AND KycPolicyVersionId IS NOT NULL)));

COMMIT TRANSACTION;
