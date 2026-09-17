/*
  Slides for the promotional block at the foot of the homepage.

  The block used to be a fixed card whose only editable parts were two settings, beside dot controls
  that were decoration: they were aria-hidden, never changed, and nothing sat behind them. A slide
  carries artwork and copy together, which is why this is its own table rather than more columns on
  dbo.Banners - no banner has a heading or body text, and nothing else would ever use them.

  Alt text is present from the start. dbo.Banners has carried those columns for a long time while no
  admin path ever wrote them, so every banner fell back to its title for search engines and screen
  readers; there is no reason to repeat that here.

  The existing copy is carried over as the first slide so the block does not empty on deploy. Its
  image is left null: the frame is 3.5:1 and no artwork of that shape exists yet, and the storefront
  skips a slide without one.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.HomeSlides', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HomeSlides
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_HomeSlides PRIMARY KEY
            CONSTRAINT DF_HomeSlides_Id DEFAULT (newsequentialid()),
        Title nvarchar(200) NOT NULL,
        Subtitle nvarchar(500) NULL,
        ImagePath nvarchar(500) NULL,
        MobileImagePath nvarchar(500) NULL,
        AltText nvarchar(250) NULL,
        MobileAltText nvarchar(250) NULL,
        LinkUrl nvarchar(500) NULL,
        LinkText nvarchar(100) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_HomeSlides_SortOrder DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_HomeSlides_IsActive DEFAULT (1),
        StartsAt datetime2 NULL,
        EndsAt datetime2 NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_HomeSlides_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2 NULL,
        CONSTRAINT CK_HomeSlides_Schedule CHECK (StartsAt IS NULL OR EndsAt IS NULL OR StartsAt <= EndsAt)
    );

    -- The storefront reads active slides in order; the admin list reads every row in the same order.
    CREATE INDEX IX_HomeSlides_IsActive_SortOrder ON dbo.HomeSlides(IsActive, SortOrder);
END;

IF OBJECT_ID(N'dbo.HomeSlides', N'U') IS NULL
    THROW 51040, N'dbo.HomeSlides could not be created.', 1;

/* Carry the current copy over, once, and only into an empty table. */
IF NOT EXISTS (SELECT 1 FROM dbo.HomeSlides)
BEGIN
    /* The heading and body were never settings rows - only defaults hard-coded in the page - so the
       literals below are the copy the site actually shows today. A row that does exist wins. */
    DECLARE @title nvarchar(200) = ISNULL((
        SELECT TOP (1) NULLIF(LTRIM(RTRIM([Value])), N'')
        FROM dbo.Settings WHERE [Key] = N'InstagramBannerTitle'), N'اینستا رو هم فالو کن ناموسن.');

    DECLARE @subtitle nvarchar(500) = ISNULL((
        SELECT TOP (1) NULLIF(LTRIM(RTRIM([Value])), N'')
        FROM dbo.Settings WHERE [Key] = N'InstagramBannerSubtitle'), N'هراز چند مزه می‌ریزیم اونجا');

    DECLARE @linkUrl nvarchar(500) = (
        SELECT TOP (1) NULLIF(LTRIM(RTRIM([Value])), N'')
        FROM dbo.Settings WHERE [Key] = N'InstagramUrl');

    IF @title IS NOT NULL
    BEGIN
        INSERT dbo.HomeSlides (Title, Subtitle, LinkUrl, LinkText, SortOrder, IsActive)
        VALUES (@title, @subtitle, @linkUrl,
                CASE WHEN @linkUrl IS NULL THEN NULL ELSE N'برو اینستاگرام' END, 0, 1);
    END;
END;
