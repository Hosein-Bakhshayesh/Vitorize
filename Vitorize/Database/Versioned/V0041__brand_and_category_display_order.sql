/*
  Display control for the two lists the homepage draws from.

  Brands had no ordering column at all: the storefront sorted them by title and the page then
  reversed that, so the strip read as a backwards alphabet with nothing an administrator could do
  about it. Categories could be hidden only by deactivating them, which removes them from the whole
  site rather than from one section, so the homepage showed every root in catalogue order.

  Defaults preserve today's behaviour: every brand starts at 0, which leaves the existing title sort
  as the tie-break, and every category starts visible in both places.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Brands', N'U') IS NULL
    THROW 51041, N'dbo.Brands is required before V0041 can run.', 1;

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
    THROW 51041, N'dbo.Categories is required before V0041 can run.', 1;

IF COL_LENGTH(N'dbo.Brands', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE dbo.Brands ADD SortOrder int NOT NULL
        CONSTRAINT DF_Brands_SortOrder DEFAULT (0);
END;

IF COL_LENGTH(N'dbo.Categories', N'ShowOnHome') IS NULL
BEGIN
    ALTER TABLE dbo.Categories ADD ShowOnHome bit NOT NULL
        CONSTRAINT DF_Categories_ShowOnHome DEFAULT (1);
END;

IF COL_LENGTH(N'dbo.Categories', N'ShowInMenu') IS NULL
BEGIN
    ALTER TABLE dbo.Categories ADD ShowInMenu bit NOT NULL
        CONSTRAINT DF_Categories_ShowInMenu DEFAULT (1);
END;

IF COL_LENGTH(N'dbo.Categories', N'MenuSortOrder') IS NULL
BEGIN
    ALTER TABLE dbo.Categories ADD MenuSortOrder int NOT NULL
        CONSTRAINT DF_Categories_MenuSortOrder DEFAULT (0);
END;

IF COL_LENGTH(N'dbo.Brands', N'SortOrder') IS NULL
    THROW 51041, N'Brands.SortOrder could not be created.', 1;

IF COL_LENGTH(N'dbo.Categories', N'ShowOnHome') IS NULL
    OR COL_LENGTH(N'dbo.Categories', N'ShowInMenu') IS NULL
    OR COL_LENGTH(N'dbo.Categories', N'MenuSortOrder') IS NULL
    THROW 51041, N'Category display columns could not be created.', 1;
