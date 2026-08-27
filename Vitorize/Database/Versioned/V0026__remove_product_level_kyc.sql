/*
  Product KYC has been retired. Verification will be evaluated from the final
  order amount in a later migration, so no product column, policy relation or
  product-specific threshold may remain active in the interim.

  OrderItems deliberately keep their immutable KYC snapshots. Those records
  describe completed or in-progress historical orders and are not product
  configuration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Products') AND name = N'CK_Products_KycConfiguration')
        ALTER TABLE dbo.Products DROP CONSTRAINT CK_Products_KycConfiguration;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Products') AND name = N'FK_Products_KycPolicyVersions')
        ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_KycPolicyVersions;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_KycPolicyVersionId')
        DROP INDEX IX_Products_KycPolicyVersionId ON dbo.Products;

    -- Default-constraint names differ between old installations, so resolve them from metadata.
    DECLARE @dropDefaults nvarchar(max) =
    (
        SELECT STRING_AGG(
            N'ALTER TABLE dbo.Products DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';',
            NCHAR(10))
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products')
          AND c.name IN (N'RequiresVerification', N'KycRequirementMode', N'KycThresholdAmount', N'KycPolicyVersionId')
    );

    IF @dropDefaults IS NOT NULL
        EXEC sys.sp_executesql @dropDefaults;

    IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN RequiresVerification;
    IF COL_LENGTH(N'dbo.Products', N'KycRequirementMode') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycRequirementMode;
    IF COL_LENGTH(N'dbo.Products', N'KycThresholdAmount') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycThresholdAmount;
    IF COL_LENGTH(N'dbo.Products', N'KycPolicyVersionId') IS NOT NULL
        ALTER TABLE dbo.Products DROP COLUMN KycPolicyVersionId;
END;

COMMIT TRANSACTION;

IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL
    THROW 51026, N'Products.RequiresVerification was not removed.', 1;
IF COL_LENGTH(N'dbo.Products', N'KycRequirementMode') IS NOT NULL
    THROW 51026, N'Products.KycRequirementMode was not removed.', 1;
IF COL_LENGTH(N'dbo.Products', N'KycThresholdAmount') IS NOT NULL
    THROW 51026, N'Products.KycThresholdAmount was not removed.', 1;
IF COL_LENGTH(N'dbo.Products', N'KycPolicyVersionId') IS NOT NULL
    THROW 51026, N'Products.KycPolicyVersionId was not removed.', 1;
