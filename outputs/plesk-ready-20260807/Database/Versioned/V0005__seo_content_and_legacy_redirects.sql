SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Products', N'FocusKeyword') IS NULL
        ALTER TABLE dbo.Products ADD FocusKeyword nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.Products', N'ThumbnailAltText') IS NULL
        ALTER TABLE dbo.Products ADD ThumbnailAltText nvarchar(250) NULL;

    IF COL_LENGTH(N'dbo.Categories', N'FocusKeyword') IS NULL
        ALTER TABLE dbo.Categories ADD FocusKeyword nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.Categories', N'ImageAltText') IS NULL
        ALTER TABLE dbo.Categories ADD ImageAltText nvarchar(250) NULL;

    IF COL_LENGTH(N'dbo.Brands', N'Description') IS NULL
        ALTER TABLE dbo.Brands ADD Description nvarchar(2000) NULL;
    IF COL_LENGTH(N'dbo.Brands', N'SeoTitle') IS NULL
        ALTER TABLE dbo.Brands ADD SeoTitle nvarchar(250) NULL;
    IF COL_LENGTH(N'dbo.Brands', N'SeoDescription') IS NULL
        ALTER TABLE dbo.Brands ADD SeoDescription nvarchar(500) NULL;
    IF COL_LENGTH(N'dbo.Brands', N'FocusKeyword') IS NULL
        ALTER TABLE dbo.Brands ADD FocusKeyword nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.Brands', N'ImageAltText') IS NULL
        ALTER TABLE dbo.Brands ADD ImageAltText nvarchar(250) NULL;
    IF COL_LENGTH(N'dbo.Brands', N'UpdatedAt') IS NULL
        ALTER TABLE dbo.Brands ADD UpdatedAt datetime2(7) NULL;

    IF COL_LENGTH(N'dbo.BlogPosts', N'FocusKeyword') IS NULL
        ALTER TABLE dbo.BlogPosts ADD FocusKeyword nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.BlogPosts', N'CoverImageAltText') IS NULL
        ALTER TABLE dbo.BlogPosts ADD CoverImageAltText nvarchar(250) NULL;
    IF COL_LENGTH(N'dbo.Pages', N'FocusKeyword') IS NULL
        ALTER TABLE dbo.Pages ADD FocusKeyword nvarchar(200) NULL;

    IF COL_LENGTH(N'dbo.Banners', N'AltText') IS NULL
        ALTER TABLE dbo.Banners ADD AltText nvarchar(250) NULL;
    IF COL_LENGTH(N'dbo.Banners', N'MobileAltText') IS NULL
        ALTER TABLE dbo.Banners ADD MobileAltText nvarchar(250) NULL;

    IF COL_LENGTH(N'dbo.ProductTags', N'Aliases') IS NULL
        ALTER TABLE dbo.ProductTags ADD Aliases nvarchar(1000) NULL;
    IF COL_LENGTH(N'dbo.ProductTags', N'IsActive') IS NULL
        ALTER TABLE dbo.ProductTags ADD IsActive bit NOT NULL CONSTRAINT DF_ProductTags_IsActive DEFAULT (1) WITH VALUES;
    IF COL_LENGTH(N'dbo.ProductTags', N'CreatedAt') IS NULL
        ALTER TABLE dbo.ProductTags ADD CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ProductTags_CreatedAt DEFAULT (sysutcdatetime()) WITH VALUES;
    IF COL_LENGTH(N'dbo.ProductTags', N'UpdatedAt') IS NULL
        ALTER TABLE dbo.ProductTags ADD UpdatedAt datetime2(7) NULL;

    IF OBJECT_ID(N'dbo.LegacyRedirects', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LegacyRedirects
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_LegacyRedirects_Id DEFAULT (newsequentialid()),
            SourcePath nvarchar(750) NOT NULL,
            DestinationPath nvarchar(1000) NULL,
            StatusCode smallint NOT NULL CONSTRAINT DF_LegacyRedirects_StatusCode DEFAULT (301),
            IsActive bit NOT NULL CONSTRAINT DF_LegacyRedirects_IsActive DEFAULT (1),
            CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_LegacyRedirects_CreatedAt DEFAULT (sysutcdatetime()),
            UpdatedAt datetime2(7) NULL,
            CONSTRAINT PK_LegacyRedirects PRIMARY KEY (Id),
            CONSTRAINT CK_LegacyRedirects_StatusCode CHECK (StatusCode IN (301, 308, 410)),
            CONSTRAINT CK_LegacyRedirects_Destination CHECK
            (
                (StatusCode IN (301, 308) AND DestinationPath IS NOT NULL AND LEFT(DestinationPath, 1) = N'/') OR
                (StatusCode = 410 AND DestinationPath IS NULL)
            ),
            CONSTRAINT CK_LegacyRedirects_SourcePath CHECK (LEFT(SourcePath, 1) = N'/' AND SourcePath NOT LIKE N'%?%' AND SourcePath NOT LIKE N'%#%')
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LegacyRedirects') AND name = N'UX_LegacyRedirects_SourcePath')
        CREATE UNIQUE INDEX UX_LegacyRedirects_SourcePath ON dbo.LegacyRedirects(SourcePath);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LegacyRedirects') AND name = N'IX_LegacyRedirects_Active_Source')
        CREATE INDEX IX_LegacyRedirects_Active_Source ON dbo.LegacyRedirects(IsActive, SourcePath) INCLUDE (DestinationPath, StatusCode, UpdatedAt);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductTagMappings') AND name = N'IX_ProductTagMappings_TagId_ProductId')
        CREATE INDEX IX_ProductTagMappings_TagId_ProductId ON dbo.ProductTagMappings(TagId, ProductId);

    IF OBJECT_ID(N'dbo.Settings', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Seo.CanonicalBaseUrl')
    BEGIN
        INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
        VALUES (NEWID(), N'Seo.CanonicalBaseUrl', N'', N'SEO', N'string',
                N'آدرس پایه HTTPS و میزبان اصلی برای canonical، robots و sitemap', SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
