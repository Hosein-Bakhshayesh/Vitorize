/*
  One entity for both homepage slideshows.

  The middle of the homepage and its foot were doing the same job through different tables. The
  middle one was a Banner at position 'home-secondary' — artwork only, no copy, no button, and no
  automatic rotation, so any wording had to be burnt into the image. The foot was a HomeSlide, which
  already carries every Banner column plus Subtitle and LinkText. Keeping both meant two admin
  screens for one idea, and an image-only middle slot that could never be captioned.

  HomeSlides gains a Placement, the two 'home-secondary' banners move across, and Banners is left
  with the job it does well: the four fixed tiles of the hero mosaic, which are a mosaic and not a
  slideshow at all.

  Existing slides default to 'home-bottom', which is where they render today. The moved banners keep
  their sort order, schedule and active flag; their Title becomes the slide heading, so a banner
  whose artwork already contains the wording would print it twice — those rows are moved with an
  empty heading and the operator can add copy deliberately.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.HomeSlides', N'U') IS NULL
    THROW 51043, N'dbo.HomeSlides is required before V0043 can run.', 1;

IF OBJECT_ID(N'dbo.Banners', N'U') IS NULL
    THROW 51043, N'dbo.Banners is required before V0043 can run.', 1;

IF COL_LENGTH(N'dbo.HomeSlides', N'Placement') IS NULL
BEGIN
    ALTER TABLE dbo.HomeSlides ADD Placement nvarchar(40) NOT NULL
        CONSTRAINT DF_HomeSlides_Placement DEFAULT (N'home-bottom');
END;

/* Everything below touches the column this script just added, so it runs through sp_executesql: a
   column added in the same batch is not yet visible to the parser, and the house scripts carry no GO
   separators because the deployer sends each file as a single batch. Values travel as parameters
   rather than literals, so nothing depends on counting nested quotes.

   Title is required on a slide but a moved banner has no separate heading: its wording lives in the
   artwork. An empty string keeps the column's contract without inventing copy. */
EXEC sp_executesql
    N'IF NOT EXISTS (SELECT 1 FROM dbo.HomeSlides WHERE Placement = @placement)
      BEGIN
          INSERT INTO dbo.HomeSlides
              (Id, Title, Subtitle, ImagePath, MobileImagePath, AltText, MobileAltText,
               LinkUrl, LinkText, Placement, SortOrder, IsActive, StartsAt, EndsAt, CreatedAt, UpdatedAt)
          SELECT
              NEWID(), @blank, NULL, b.ImagePath, b.MobileImagePath, b.AltText, b.MobileAltText,
              b.LinkUrl, NULL, @placement, b.SortOrder, b.IsActive, b.StartsAt, b.EndsAt,
              b.CreatedAt, NULL
          FROM dbo.Banners b
          WHERE b.Position = @position;

          DELETE FROM dbo.Banners WHERE Position = @position;
      END;',
    N'@placement nvarchar(40), @position nvarchar(40), @blank nvarchar(200)',
    @placement = N'home-middle',
    @position  = N'home-secondary',
    @blank     = N'';

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_HomeSlides_Placement_SortOrder'
                 AND object_id = OBJECT_ID(N'dbo.HomeSlides'))
BEGIN
    EXEC sp_executesql
        N'CREATE NONCLUSTERED INDEX IX_HomeSlides_Placement_SortOrder
              ON dbo.HomeSlides (Placement, SortOrder)
              WHERE IsActive = 1;';
END;

IF COL_LENGTH(N'dbo.HomeSlides', N'Placement') IS NULL
    THROW 51043, N'HomeSlides.Placement could not be created.', 1;

IF EXISTS (SELECT 1 FROM dbo.Banners WHERE Position = N'home-secondary')
    THROW 51043, N'home-secondary banners were not migrated to HomeSlides.', 1;
