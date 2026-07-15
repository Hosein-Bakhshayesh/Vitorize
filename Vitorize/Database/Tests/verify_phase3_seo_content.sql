SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Issues TABLE (Message nvarchar(1000) NOT NULL);

IF OBJECT_ID(N'dbo.LegacyRedirects', N'U') IS NULL
    INSERT @Issues VALUES (N'dbo.LegacyRedirects is missing.');
IF COL_LENGTH(N'dbo.Products', N'FocusKeyword') IS NULL OR COL_LENGTH(N'dbo.Products', N'ThumbnailAltText') IS NULL
    INSERT @Issues VALUES (N'Product SEO/media columns are missing.');
IF COL_LENGTH(N'dbo.Categories', N'FocusKeyword') IS NULL OR COL_LENGTH(N'dbo.Categories', N'ImageAltText') IS NULL
    INSERT @Issues VALUES (N'Category SEO/media columns are missing.');
IF COL_LENGTH(N'dbo.Brands', N'SeoTitle') IS NULL OR COL_LENGTH(N'dbo.Brands', N'FocusKeyword') IS NULL OR COL_LENGTH(N'dbo.Brands', N'ImageAltText') IS NULL
    INSERT @Issues VALUES (N'Brand SEO/media columns are missing.');
IF COL_LENGTH(N'dbo.ProductTags', N'Aliases') IS NULL OR COL_LENGTH(N'dbo.ProductTags', N'IsActive') IS NULL
    INSERT @Issues VALUES (N'ProductTag editorial/search columns are missing.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LegacyRedirects') AND name = N'UX_LegacyRedirects_SourcePath' AND is_unique = 1)
    INSERT @Issues VALUES (N'Legacy redirect source uniqueness is missing.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductTagMappings') AND name = N'IX_ProductTagMappings_TagId_ProductId')
    INSERT @Issues VALUES (N'Product tag reverse lookup index is missing.');
IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Seo.CanonicalBaseUrl')
    INSERT @Issues VALUES (N'Seo.CanonicalBaseUrl setting is missing.');

IF EXISTS (SELECT 1 FROM @Issues)
BEGIN
    SELECT Message FROM @Issues;
    THROW 51130, 'Phase 3 SEO/content database verification failed.', 1;
END;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @Source nvarchar(1000) = N'/__phase3-redirect-' + CONVERT(nvarchar(36), NEWID());
    INSERT dbo.LegacyRedirects (Id, SourcePath, DestinationPath, StatusCode, IsActive, CreatedAt)
    VALUES (NEWID(), @Source, N'/product/phase3-target', 301, 1, SYSUTCDATETIME());

    BEGIN TRY
        INSERT dbo.LegacyRedirects (Id, SourcePath, DestinationPath, StatusCode, IsActive, CreatedAt)
        VALUES (NEWID(), @Source, N'/product/duplicate', 301, 1, SYSUTCDATETIME());
        THROW 51131, 'Duplicate redirect source was unexpectedly accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 51131 THROW;
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
    END CATCH;

    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT N'Phase 3 SEO/content database verification passed.';
