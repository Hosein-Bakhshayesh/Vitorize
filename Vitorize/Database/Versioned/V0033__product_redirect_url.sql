/*
  An optional customer-facing destination for a product. Existing products retain their normal
  detail-page links because the new field is nullable.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH(N'dbo.Products', N'RedirectUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD RedirectUrl nvarchar(2048) NULL;
END;

IF COL_LENGTH(N'dbo.Products', N'RedirectUrl') IS NULL
    THROW 51033, N'Products.RedirectUrl could not be created.', 1;
