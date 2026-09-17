/*
  Settings the panel offered that nothing read.

  Each key below was checked against every storefront component, by property name and by key string,
  before removal. An administrator could edit any of them, save, and see no change anywhere - which
  costs more trust than a missing option does.

    Newsletter*        the redesigned homepage has no newsletter section at all
    HomeFeatures*      the benefits row carries no heading in the current design
    TrustBadgesJson    the trust-badge strip is gone; footer seals are a separate key and stay
    About*             the about page is served from the CMS pages, not from these
    HeroBackgroundPath offered with upload guidance, but no component renders it
    MetaKeywords       no page emits a keywords meta tag, and search engines ignore it regardless
    InstagramBanner*   superseded by dbo.HomeSlides (V0040); these were never rows in any case

  Removing a row is safe: every consumer reads settings through a helper with a code-side fallback,
  so a key that reappears later simply takes over again.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
    THROW 51042, N'dbo.Settings is required before V0042 can run.', 1;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'NewsletterTitle',
    N'NewsletterSubtitle',
    N'NewsletterPlaceholder',
    N'NewsletterCtaText',
    N'HomeFeaturesKicker',
    N'HomeFeaturesTitle',
    N'TrustBadgesJson',
    N'AboutTitle',
    N'AboutText',
    N'HeroBackgroundPath',
    N'MetaKeywords',
    N'InstagramBannerTitle',
    N'InstagramBannerSubtitle'
);

COMMIT TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] IN (N'NewsletterTitle', N'TrustBadgesJson', N'MetaKeywords'))
    THROW 51042, N'Inert homepage settings could not be removed.', 1;
