/* Required non-secret operational seed; safe to rerun. */
SET NOCOUNT ON;

GO
/* Source: Database/Versioned\V0003__seed_reference_roles.sql */
/*
    Idempotent non-secret reference seed. Existing roles are preserved and no
    users, passwords or role assignments are created.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51020, 'dbo.Roles must exist before V0003 can run.', 1;

    DECLARE @Roles TABLE
    (
        Name nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(150) NOT NULL
    );

    INSERT @Roles (Name, DisplayName)
    VALUES
        (N'SuperAdmin', N'Ù…Ø¯ÛŒØ± Ú©Ù„'),
        (N'Admin', N'Ù…Ø¯ÛŒØ± ÙØ±ÙˆØ´Ú¯Ø§Ù‡'),
        (N'Support', N'Ù¾Ø´ØªÛŒØ¨Ø§Ù†'),
        (N'Customer', N'Ù…Ø´ØªØ±ÛŒ');

    INSERT dbo.Roles (Id, Name, DisplayName, CreatedAt)
    SELECT NEWID(), source.Name, source.DisplayName, SYSUTCDATETIME()
    FROM @Roles source
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Roles existing WHERE existing.Name = source.Name
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;


GO
/* Source: Database/2026-07-08_seed_settings_ui_customization.sql */
-- ============================================================================
-- Vitorize â€” Settings seed for the Final UI/UX Customization pass (2026-07-08)
-- NO SCHEMA CHANGES. Data-only. Idempotent â€” safe to run repeatedly.
--
-- Inserts every branding / SEO / homepage / footer / social / contact /
-- empty-state / error-page / logo & image Setting key used by the storefront
-- and admin panel. EXISTING ROWS ARE NEVER TOUCHED (values are preserved).
--
-- This mirrors VitorizeSeedService.SeedSettingsAsync, which also runs at API
-- startup. Applying this script is OPTIONAL â€” it only lets you provision the
-- keys without an app restart. Empty image/logo values fall back to built-in
-- defaults (e.g. the packaged mascot illustration).
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

;WITH seed([Key], [Value], GroupName, ValueType, [Description]) AS (
    SELECT * FROM (VALUES
        -- General
        (N'MaintenanceMode', N'false', N'General', N'bool', N'Ø­Ø§Ù„Øª ØªØ¹Ù…ÛŒØ± Ùˆ Ù†Ú¯Ù‡Ø¯Ø§Ø±ÛŒ'),
        (N'MaintenanceMessage', N'Ø¨Ù‡â€ŒØ²ÙˆØ¯ÛŒ Ø¨Ø§ Ù†Ø³Ø®Ù‡â€ŒØ§ÛŒ Ø¨Ù‡ØªØ± Ø¨Ø±Ù…ÛŒâ€ŒÚ¯Ø±Ø¯ÛŒÙ…. Ø§Ø² ØµØ¨ÙˆØ±ÛŒ Ø´Ù…Ø§ Ø³Ù¾Ø§Ø³Ú¯Ø²Ø§Ø±ÛŒÙ….', N'General', N'string', N'Ù¾ÛŒØ§Ù… ØµÙØ­Ù‡ Ø­Ø§Ù„Øª ØªØ¹Ù…ÛŒØ±'),
        -- Branding
        (N'BrandPrimaryColor', N'', N'Branding', N'color', N'Ø±Ù†Ú¯ Ø§ØµÙ„ÛŒ Ø¨Ø±Ù†Ø¯'),
        -- Logos & Images
        (N'LogoPath', N'', N'Logos', N'image', N'Ù„ÙˆÚ¯ÙˆÛŒ Ø§ØµÙ„ÛŒ (ØªÙ… Ø±ÙˆØ´Ù†)'),
        (N'LogoDarkPath', N'', N'Logos', N'image', N'Ù„ÙˆÚ¯ÙˆÛŒ ØªÙ… ØªÛŒØ±Ù‡'),
        (N'LogoSmallPath', N'', N'Logos', N'image', N'Ù„ÙˆÚ¯ÙˆÛŒ Ú©ÙˆÚ†Ú© / Ø¢ÛŒÚ©ÙˆÙ†'),
        (N'HeaderLogoPath', N'', N'Logos', N'image', N'Ù„ÙˆÚ¯ÙˆÛŒ Ù‡Ø¯Ø±'),
        (N'FooterLogoPath', N'', N'Logos', N'image', N'Ù„ÙˆÚ¯ÙˆÛŒ ÙÙˆØªØ±'),
        (N'FaviconPath', N'', N'Logos', N'image', N'ÙØ§ÙˆØ¢ÛŒÚ©ÙˆÙ† Ø³Ø§ÛŒØª'),
        (N'AppleTouchIconPath', N'', N'Logos', N'image', N'Ø¢ÛŒÚ©ÙˆÙ† Apple Touch'),
        (N'OgImagePath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± OpenGraph'),
        (N'TwitterImagePath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± ØªÙˆÛŒÛŒØªØ± / X'),
        (N'SocialPreviewImagePath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± Ù¾ÛŒØ´â€ŒÙ†Ù…Ø§ÛŒØ´ Ø´Ø¨Ú©Ù‡â€ŒÙ‡Ø§ÛŒ Ø§Ø¬ØªÙ…Ø§Ø¹ÛŒ'),
        (N'HeroBackgroundPath', N'', N'Logos', N'image', N'Ù¾Ø³â€ŒØ²Ù…ÛŒÙ†Ù‡ Hero'),
        (N'Error404IllustrationPath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± ØµÙØ­Ù‡ Û´Û°Û´'),
        (N'Error500IllustrationPath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± ØµÙØ­Ù‡ ÛµÛ°Û°'),
        (N'MaintenanceIllustrationPath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± ØµÙØ­Ù‡ ØªØ¹Ù…ÛŒØ±'),
        (N'EmptyStateIllustrationPath', N'', N'Logos', N'image', N'ØªØµÙˆÛŒØ± Ù¾ÛŒØ´â€ŒÙØ±Ø¶ Ø­Ø§Ù„Øª Ø®Ø§Ù„ÛŒ'),
        -- SEO
        (N'MetaTitle', N'ÙˆÛŒØªÙˆØ±Ø§ÛŒØ² | Ø¨Ø§Ø²Ø§Ø±Ú¯Ø§Ù‡ Ø¯ÛŒØ¬ÛŒØªØ§Ù„ Ú¯ÛŒÙ…ÛŒÙ†Ú¯ Ùˆ Ø®Ø¯Ù…Ø§Øª Ø¢Ù†Ù„Ø§ÛŒÙ†', N'SEO', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ù…ØªØ§ÛŒ Ù¾ÛŒØ´â€ŒÙØ±Ø¶'),
        (N'MetaDescription', N'Ø®Ø±ÛŒØ¯ Ø³Ø±ÛŒØ¹ØŒ Ù…Ø·Ù…Ø¦Ù† Ùˆ Ø±Ø³Ù…ÛŒ Ú¯ÛŒÙØª Ú©Ø§Ø±ØªØŒ Ø§Ø´ØªØ±Ø§Ú© Ùˆ Ø®Ø¯Ù…Ø§Øª Ø¯ÛŒØ¬ÛŒØªØ§Ù„ Ø¨Ø§ ØªØ­ÙˆÛŒÙ„ Ø¢Ù†ÛŒ Ùˆ Ù¾Ø´ØªÛŒØ¨Ø§Ù†ÛŒ Û²Û´ Ø³Ø§Ø¹ØªÙ‡.', N'SEO', N'string', N'ØªÙˆØ¶ÛŒØ­ Ù…ØªØ§ÛŒ Ù¾ÛŒØ´â€ŒÙØ±Ø¶'),
        (N'MetaKeywords', N'Ú¯ÛŒÙØª Ú©Ø§Ø±Øª, Ø§Ø´ØªØ±Ø§Ú©, Ø®Ø¯Ù…Ø§Øª Ø¯ÛŒØ¬ÛŒØªØ§Ù„, Ø¨Ø§Ø²ÛŒ, Ú¯ÛŒÙ…ÛŒÙ†Ú¯, ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²', N'SEO', N'string', N'Ú©Ù„Ù…Ø§Øª Ú©Ù„ÛŒØ¯ÛŒ'),
        (N'SeoTitleTemplate', N'{page} | {site}', N'SEO', N'string', N'Ù‚Ø§Ù„Ø¨ Ø¹Ù†ÙˆØ§Ù† ØµÙØ­Ø§Øª'),
        (N'GoogleAnalyticsId', N'', N'SEO', N'string', N'Ø´Ù†Ø§Ø³Ù‡ Google Analytics'),
        -- Homepage
        (N'HeroCtaUrl', N'/shop', N'Homepage', N'string', N'Ù„ÛŒÙ†Ú© Ø¯Ú©Ù…Ù‡ Ø§ØµÙ„ÛŒ Hero'),
        (N'HeroSecondaryCtaText', N'Ø¯Ø³ØªÙ‡â€ŒØ¨Ù†Ø¯ÛŒâ€ŒÙ‡Ø§', N'Homepage', N'string', N'Ù…ØªÙ† Ø¯Ú©Ù…Ù‡ Ø¯ÙˆÙ… Hero'),
        (N'HeroSecondaryCtaUrl', N'/categories', N'Homepage', N'string', N'Ù„ÛŒÙ†Ú© Ø¯Ú©Ù…Ù‡ Ø¯ÙˆÙ… Hero'),
        (N'NewsletterTitle', N'Ø§Ø² Ø¬Ø¯ÛŒØ¯ØªØ±ÛŒÙ†â€ŒÙ‡Ø§ Ø¨Ø§Ø®Ø¨Ø± Ø´Ùˆ', N'Homepage', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ø®Ø¨Ø±Ù†Ø§Ù…Ù‡'),
        (N'NewsletterSubtitle', N'Ø¨Ø§ Ø¹Ø¶ÙˆÛŒØª Ø¯Ø± Ø®Ø¨Ø±Ù†Ø§Ù…Ù‡ØŒ Ø§Ø² ØªØ®ÙÛŒÙâ€ŒÙ‡Ø§ Ùˆ Ù…Ø­ØµÙˆÙ„Ø§Øª ØªØ§Ø²Ù‡ Ø²ÙˆØ¯ØªØ± Ø§Ø² Ù‡Ù…Ù‡ Ù…Ø·Ù„Ø¹ Ø´Ùˆ.', N'Homepage', N'string', N'Ø²ÛŒØ±Ø¹Ù†ÙˆØ§Ù† Ø®Ø¨Ø±Ù†Ø§Ù…Ù‡'),
        (N'NewsletterCtaText', N'Ø¹Ø¶ÙˆÛŒØª', N'Homepage', N'string', N'Ù…ØªÙ† Ø¯Ú©Ù…Ù‡ Ø®Ø¨Ø±Ù†Ø§Ù…Ù‡'),
        (N'NewsletterPlaceholder', N'Ø§ÛŒÙ…ÛŒÙ„ Ø®ÙˆØ¯ Ø±Ø§ ÙˆØ§Ø±Ø¯ Ú©Ù†ÛŒØ¯', N'Homepage', N'string', N'Ø±Ø§Ù‡Ù†Ù…Ø§ÛŒ ÙˆØ±ÙˆØ¯ÛŒ Ø®Ø¨Ø±Ù†Ø§Ù…Ù‡'),
        -- About
        (N'AboutTitle', N'Ø¯Ø±Ø¨Ø§Ø±Ù‡ ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²', N'About', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ø¯Ø±Ø¨Ø§Ø±Ù‡ Ù…Ø§'),
        (N'AboutText', N'ÙˆÛŒØªÙˆØ±Ø§ÛŒØ² Ø¨Ø§Ø²Ø§Ø±Ú¯Ø§Ù‡ÛŒ Ø¯ÛŒØ¬ÛŒØªØ§Ù„ Ø¨Ø±Ø§ÛŒ Ø®Ø±ÛŒØ¯ Ø§Ù…Ù† Ùˆ Ø¢Ù†ÛŒ Ú¯ÛŒÙØª Ú©Ø§Ø±ØªØŒ Ø§Ø´ØªØ±Ø§Ú© Ùˆ Ø®Ø¯Ù…Ø§Øª Ø¢Ù†Ù„Ø§ÛŒÙ† Ø§Ø³Øª.', N'About', N'string', N'Ù…ØªÙ† Ø¯Ø±Ø¨Ø§Ø±Ù‡ Ù…Ø§'),
        -- Trust badges & features (JSON)
        (N'TrustBadgesJson', N'[{"icon":"shield-check","title":"ØªØ¶Ù…ÛŒÙ† Ø§ØµØ§Ù„Øª","text":"Ù…Ø­ØµÙˆÙ„Ø§Øª Ø±Ø³Ù…ÛŒ Ùˆ Ø§ÙˆØ±Ø¬ÛŒÙ†Ø§Ù„"},{"icon":"zap","title":"ØªØ­ÙˆÛŒÙ„ Ø¢Ù†ÛŒ","text":"Ø³Ø±ÛŒØ¹ Ùˆ Ø¨Ø¯ÙˆÙ† Ø§Ù†ØªØ¸Ø§Ø±"},{"icon":"headphones","title":"Ù¾Ø´ØªÛŒØ¨Ø§Ù†ÛŒ Û²Û´/Û·","text":"Ù‡Ù…ÛŒØ´Ù‡ Ú©Ù†Ø§Ø± Ø´Ù…Ø§"},{"icon":"lock","title":"Ù¾Ø±Ø¯Ø§Ø®Øª Ø§Ù…Ù†","text":"Ø¯Ø±Ú¯Ø§Ù‡â€ŒÙ‡Ø§ÛŒ Ù…Ø¹ØªØ¨Ø±"}]', N'Trust', N'json', N'Ù†Ø´Ø§Ù†â€ŒÙ‡Ø§ÛŒ Ø§Ø¹ØªÙ…Ø§Ø¯'),
        (N'HomeFeaturesKicker', N'Ú†Ø±Ø§ ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²ØŸ', N'Trust', N'string', N'Ø¨Ø±Ú†Ø³Ø¨ Ø¨Ø®Ø´ Ú†Ø±Ø§ Ù…Ø§'),
        (N'HomeFeaturesTitle', N'Ø®Ø±ÛŒØ¯ Ø¯ÛŒØ¬ÛŒØªØ§Ù„ØŒ Ø³Ø§Ø¯Ù‡ Ùˆ Ù…Ø·Ù…Ø¦Ù†', N'Trust', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ø¨Ø®Ø´ Ú†Ø±Ø§ Ù…Ø§'),
        (N'HomeFeaturesJson', N'[{"icon":"layout-grid","title":"Ø§Ù†ØªØ®Ø§Ø¨ Ù…Ø­ØµÙˆÙ„","text":"Ø§Ø² Ù…ÛŒØ§Ù† Ù‡Ø²Ø§Ø±Ø§Ù† Ú¯ÛŒÙØª Ú©Ø§Ø±ØªØŒ Ø§Ø´ØªØ±Ø§Ú© Ùˆ Ø®Ø¯Ù…Øª Ø¯ÛŒØ¬ÛŒØªØ§Ù„ØŒ Ù…Ø­ØµÙˆÙ„ Ù…ÙˆØ±Ø¯ Ù†Ø¸Ø±Øª Ø±Ø§ Ù¾ÛŒØ¯Ø§ Ú©Ù†."},{"icon":"credit-card","title":"Ù¾Ø±Ø¯Ø§Ø®Øª Ø§Ù…Ù†","text":"Ø¨Ø§ Ø¯Ø±Ú¯Ø§Ù‡â€ŒÙ‡Ø§ÛŒ Ù…Ø¹ØªØ¨Ø± Ø¨Ø§Ù†Ú©ÛŒ ÛŒØ§ Ú©ÛŒÙ Ù¾ÙˆÙ„ ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²ØŒ Ù¾Ø±Ø¯Ø§Ø®Øª Ø³Ø±ÛŒØ¹ Ùˆ Ø§Ù…Ù† Ø§Ù†Ø¬Ø§Ù… Ø¨Ø¯Ù‡."},{"icon":"zap","title":"ØªØ­ÙˆÛŒÙ„ Ø¢Ù†ÛŒ","text":"Ú©Ø¯ ÛŒØ§ Ø®Ø¯Ù…Øª Ø¯ÛŒØ¬ÛŒØªØ§Ù„ Ø¨Ù„Ø§ÙØ§ØµÙ„Ù‡ Ù¾Ø³ Ø§Ø² Ù¾Ø±Ø¯Ø§Ø®Øª Ø¯Ø± Ø­Ø³Ø§Ø¨ Ú©Ø§Ø±Ø¨Ø±ÛŒâ€ŒØ§Øª ÙØ¹Ø§Ù„ Ù…ÛŒâ€ŒØ´ÙˆØ¯."}]', N'Trust', N'json', N'Ù…Ø±Ø§Ø­Ù„ ØµÙØ­Ù‡ Ø§ÙˆÙ„'),
        -- Footer
        (N'FooterText', N'', N'Footer', N'string', N'Ù…ØªÙ† Ø¢Ø²Ø§Ø¯ ÙÙˆØªØ±'),
        -- Social
        (N'WhatsAppUrl', N'', N'Social', N'string', N'ÙˆØ§ØªØ³Ø§Ù¾'),
        (N'XUrl', N'', N'Social', N'string', N'X (ØªÙˆÛŒÛŒØªØ±)'),
        (N'LinkedInUrl', N'', N'Social', N'string', N'Ù„ÛŒÙ†Ú©Ø¯ÛŒÙ†'),
        (N'DiscordUrl', N'', N'Social', N'string', N'Ø¯ÛŒØ³Ú©ÙˆØ±Ø¯'),
        (N'YouTubeUrl', N'', N'Social', N'string', N'ÛŒÙˆØªÛŒÙˆØ¨'),
        (N'FacebookUrl', N'', N'Social', N'string', N'ÙÛŒØ³Ø¨ÙˆÚ©'),
        -- Contact
        (N'ContactAddress', N'', N'Contact', N'string', N'Ø¢Ø¯Ø±Ø³'),
        (N'WorkingHours', N'Ø´Ù†Ø¨Ù‡ ØªØ§ Ù¾Ù†Ø¬Ø´Ù†Ø¨Ù‡ØŒ Û¹ ØªØ§ Û±Û¸', N'Contact', N'string', N'Ø³Ø§Ø¹Ø§Øª Ú©Ø§Ø±ÛŒ'),
        -- Empty states
        (N'EmptyCartText', N'Ø³Ø¨Ø¯ Ø®Ø±ÛŒØ¯ Ø´Ù…Ø§ Ø®Ø§Ù„ÛŒ Ø§Ø³Øª.', N'Empty', N'string', N'Ø³Ø¨Ø¯ Ø®Ø±ÛŒØ¯ Ø®Ø§Ù„ÛŒ'),
        (N'EmptyWishlistText', N'Ù‡Ù†ÙˆØ² Ù…Ø­ØµÙˆÙ„ÛŒ Ø¨Ù‡ Ø¹Ù„Ø§Ù‚Ù‡â€ŒÙ…Ù†Ø¯ÛŒâ€ŒÙ‡Ø§ Ø§Ø¶Ø§ÙÙ‡ Ù†Ú©Ø±Ø¯Ù‡â€ŒØ§ÛŒØ¯.', N'Empty', N'string', N'Ø¹Ù„Ø§Ù‚Ù‡â€ŒÙ…Ù†Ø¯ÛŒ Ø®Ø§Ù„ÛŒ'),
        (N'EmptyOrdersText', N'Ù‡Ù†ÙˆØ² Ø³ÙØ§Ø±Ø´ÛŒ Ø«Ø¨Øª Ù†Ú©Ø±Ø¯Ù‡â€ŒØ§ÛŒØ¯.', N'Empty', N'string', N'Ø³ÙØ§Ø±Ø´â€ŒÙ‡Ø§ÛŒ Ø®Ø§Ù„ÛŒ'),
        (N'EmptySearchText', N'Ù†ØªÛŒØ¬Ù‡â€ŒØ§ÛŒ Ø¨Ø±Ø§ÛŒ Ø¬Ø³ØªØ¬ÙˆÛŒ Ø´Ù…Ø§ Ù¾ÛŒØ¯Ø§ Ù†Ø´Ø¯.', N'Empty', N'string', N'Ø¬Ø³ØªØ¬ÙˆÛŒ Ø¨Ø¯ÙˆÙ† Ù†ØªÛŒØ¬Ù‡'),
        (N'EmptyNotificationsText', N'Ø§Ø¹Ù„Ø§Ù† Ø¬Ø¯ÛŒØ¯ÛŒ Ù†Ø¯Ø§Ø±ÛŒØ¯.', N'Empty', N'string', N'Ø§Ø¹Ù„Ø§Ù† Ø®Ø§Ù„ÛŒ'),
        (N'EmptyTicketsText', N'ØªÛŒÚ©ØªÛŒ Ø«Ø¨Øª Ù†Ú©Ø±Ø¯Ù‡â€ŒØ§ÛŒØ¯.', N'Empty', N'string', N'ØªÛŒÚ©Øª Ø®Ø§Ù„ÛŒ'),
        (N'EmptyReviewsText', N'Ù‡Ù†ÙˆØ² Ù†Ø¸Ø±ÛŒ Ø«Ø¨Øª Ù†Ø´Ø¯Ù‡ Ø§Ø³Øª.', N'Empty', N'string', N'Ù†Ø¸Ø±Ø§Øª Ø®Ø§Ù„ÛŒ'),
        (N'NoProductsText', N'Ù…Ø­ØµÙˆÙ„ÛŒ Ø¨Ø±Ø§ÛŒ Ù†Ù…Ø§ÛŒØ´ ÙˆØ¬ÙˆØ¯ Ù†Ø¯Ø§Ø±Ø¯.', N'Empty', N'string', N'Ù†Ø¨ÙˆØ¯ Ù…Ø­ØµÙˆÙ„'),
        -- Error / status page texts
        (N'Error404Title', N'ØµÙØ­Ù‡ Ù¾ÛŒØ¯Ø§ Ù†Ø´Ø¯', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Û´Û°Û´'),
        (N'Error404Text', N'ØµÙØ­Ù‡â€ŒØ§ÛŒ Ú©Ù‡ Ø¯Ù†Ø¨Ø§Ù„ Ø¢Ù† Ù‡Ø³ØªÛŒØ¯ ÙˆØ¬ÙˆØ¯ Ù†Ø¯Ø§Ø±Ø¯ ÛŒØ§ Ù…Ù†ØªÙ‚Ù„ Ø´Ø¯Ù‡ Ø§Ø³Øª.', N'Errors', N'string', N'Ù…ØªÙ† Û´Û°Û´'),
        (N'Error400Title', N'Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ù†Ø§Ù…Ø¹ØªØ¨Ø±', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Û´Û°Û°'),
        (N'Error400Text', N'Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ø´Ù…Ø§ Ù…Ø¹ØªØ¨Ø± Ù†ÛŒØ³Øª. Ù„Ø·ÙØ§Ù‹ Ø¯ÙˆØ¨Ø§Ø±Ù‡ ØªÙ„Ø§Ø´ Ú©Ù†ÛŒØ¯.', N'Errors', N'string', N'Ù…ØªÙ† Û´Û°Û°'),
        (N'Error401Title', N'Ù†ÛŒØ§Ø² Ø¨Ù‡ ÙˆØ±ÙˆØ¯', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Û´Û°Û±'),
        (N'Error401Text', N'Ø¨Ø±Ø§ÛŒ Ù…Ø´Ø§Ù‡Ø¯Ù‡ Ø§ÛŒÙ† ØµÙØ­Ù‡ Ø§Ø¨ØªØ¯Ø§ ÙˆØ§Ø±Ø¯ Ø­Ø³Ø§Ø¨ Ú©Ø§Ø±Ø¨Ø±ÛŒ Ø´ÙˆÛŒØ¯.', N'Errors', N'string', N'Ù…ØªÙ† Û´Û°Û±'),
        (N'Error403Title', N'Ø¯Ø³ØªØ±Ø³ÛŒ Ù…Ø¬Ø§Ø² Ù†ÛŒØ³Øª', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Û´Û°Û³'),
        (N'Error403Text', N'Ø´Ù…Ø§ Ø§Ø¬Ø§Ø²Ù‡ Ø¯Ø³ØªØ±Ø³ÛŒ Ø¨Ù‡ Ø§ÛŒÙ† Ø¨Ø®Ø´ Ø±Ø§ Ù†Ø¯Ø§Ø±ÛŒØ¯.', N'Errors', N'string', N'Ù…ØªÙ† Û´Û°Û³'),
        (N'Error500Title', N'Ø®Ø·Ø§ÛŒ ØºÛŒØ±Ù…Ù†ØªØ¸Ø±Ù‡', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† ÛµÛ°Û°'),
        (N'Error500Text', N'Ù…Ø´Ú©Ù„ÛŒ Ø¯Ø± Ø³Ø±ÙˆØ± Ø±Ø® Ø¯Ø§Ø¯. ØªÛŒÙ… Ù…Ø§ Ø¯Ø± Ø­Ø§Ù„ Ø¨Ø±Ø±Ø³ÛŒ Ø§Ø³Øª.', N'Errors', N'string', N'Ù…ØªÙ† ÛµÛ°Û°'),
        (N'Error503Title', N'Ø¯Ø± Ø­Ø§Ù„ Ø¨Ù‡â€ŒØ±ÙˆØ²Ø±Ø³Ø§Ù†ÛŒ', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† ÛµÛ°Û³'),
        (N'Error503Text', N'Ø³Ø§ÛŒØª Ù…ÙˆÙ‚ØªØ§Ù‹ Ø¯Ø± Ø¯Ø³ØªØ±Ø³ Ù†ÛŒØ³Øª. Ø¨Ù‡â€ŒØ²ÙˆØ¯ÛŒ Ø¨Ø±Ù…ÛŒâ€ŒÚ¯Ø±Ø¯ÛŒÙ….', N'Errors', N'string', N'Ù…ØªÙ† ÛµÛ°Û³'),
        (N'SessionExpiredTitle', N'Ù†Ø´Ø³Øª Ø´Ù…Ø§ Ù…Ù†Ù‚Ø¶ÛŒ Ø´Ø¯', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ù†Ø´Ø³Øª Ù…Ù†Ù‚Ø¶ÛŒ'),
        (N'SessionExpiredText', N'Ø¨Ø±Ø§ÛŒ Ø§Ø¯Ø§Ù…Ù‡ Ø¯ÙˆØ¨Ø§Ø±Ù‡ ÙˆØ§Ø±Ø¯ Ø´ÙˆÛŒØ¯.', N'Errors', N'string', N'Ù…ØªÙ† Ù†Ø´Ø³Øª Ù…Ù†Ù‚Ø¶ÛŒ'),
        (N'NetworkErrorTitle', N'Ø®Ø·Ø§ÛŒ Ø§Ø±ØªØ¨Ø§Ø·', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ø®Ø·Ø§ÛŒ Ø´Ø¨Ú©Ù‡'),
        (N'NetworkErrorText', N'Ø§Ø±ØªØ¨Ø§Ø· Ø¨Ø§ Ø³Ø±ÙˆØ± Ø¨Ø±Ù‚Ø±Ø§Ø± Ù†Ø´Ø¯. Ø§ØªØµØ§Ù„ Ø§ÛŒÙ†ØªØ±Ù†Øª Ø®ÙˆØ¯ Ø±Ø§ Ø¨Ø±Ø±Ø³ÛŒ Ú©Ù†ÛŒØ¯.', N'Errors', N'string', N'Ù…ØªÙ† Ø®Ø·Ø§ÛŒ Ø´Ø¨Ú©Ù‡'),
        (N'OfflineTitle', N'Ø§ØªØµØ§Ù„ Ø§ÛŒÙ†ØªØ±Ù†Øª Ù‚Ø·Ø¹ Ø§Ø³Øª', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† Ø¢ÙÙ„Ø§ÛŒÙ†'),
        (N'OfflineText', N'Ø¨Ù‡ Ù†Ø¸Ø± Ù…ÛŒâ€ŒØ±Ø³Ø¯ Ø§ÛŒÙ†ØªØ±Ù†Øª Ø´Ù…Ø§ Ù‚Ø·Ø¹ Ø´Ø¯Ù‡ Ø§Ø³Øª.', N'Errors', N'string', N'Ù…ØªÙ† Ø¢ÙÙ„Ø§ÛŒÙ†'),
        (N'PageRemovedTitle', N'Ø§ÛŒÙ† ØµÙØ­Ù‡ Ø­Ø°Ù Ø´Ø¯Ù‡ Ø§Ø³Øª', N'Errors', N'string', N'Ø¹Ù†ÙˆØ§Ù† ØµÙØ­Ù‡ Ø­Ø°Ùâ€ŒØ´Ø¯Ù‡'),
        (N'PageRemovedText', N'Ù…Ø­ØªÙˆØ§ÛŒÛŒ Ú©Ù‡ Ø¯Ù†Ø¨Ø§Ù„ Ø¢Ù† Ø¨ÙˆØ¯ÛŒØ¯ Ø¯ÛŒÚ¯Ø± Ø¯Ø± Ø¯Ø³ØªØ±Ø³ Ù†ÛŒØ³Øª.', N'Errors', N'string', N'Ù…ØªÙ† ØµÙØ­Ù‡ Ø­Ø°Ùâ€ŒØ´Ø¯Ù‡'),
        -- Custom scripts
        (N'CustomHeadHtml', N'', N'Scripts', N'string', N'Ú©Ø¯ Ø³ÙØ§Ø±Ø´ÛŒ <head>'),
        (N'CustomFooterHtml', N'', N'Scripts', N'string', N'Ú©Ø¯ Ø³ÙØ§Ø±Ø´ÛŒ Ø§Ù†ØªÙ‡Ø§ÛŒ ØµÙØ­Ù‡'),
        -- Email (SMTP)
        (N'SmtpHost', N'', N'Email', N'string', N'Ù…ÛŒØ²Ø¨Ø§Ù† SMTP'),
        (N'SmtpPort', N'587', N'Email', N'int', N'Ù¾ÙˆØ±Øª SMTP'),
        (N'SmtpUsername', N'', N'Email', N'string', N'Ù†Ø§Ù… Ú©Ø§Ø±Ø¨Ø±ÛŒ SMTP'),
        (N'SmtpFromEmail', N'', N'Email', N'string', N'Ø§ÛŒÙ…ÛŒÙ„ ÙØ±Ø³ØªÙ†Ø¯Ù‡'),
        (N'SmtpFromName', N'ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²', N'Email', N'string', N'Ù†Ø§Ù… ÙØ±Ø³ØªÙ†Ø¯Ù‡'),
        (N'SmtpEnableSsl', N'true', N'Email', N'bool', N'Ø§Ø³ØªÙØ§Ø¯Ù‡ Ø§Ø² SSL'),
        -- Security
        (N'RequireEmailConfirmation', N'false', N'Security', N'bool', N'Ø§Ù„Ø²Ø§Ù… ØªØ£ÛŒÛŒØ¯ Ø§ÛŒÙ…ÛŒÙ„'),
        (N'MinPasswordLength', N'8', N'Security', N'int', N'Ø­Ø¯Ø§Ù‚Ù„ Ø·ÙˆÙ„ Ø±Ù…Ø²'),
        (N'MaxLoginAttempts', N'5', N'Security', N'int', N'Ø­Ø¯Ø§Ú©Ø«Ø± ØªÙ„Ø§Ø´ ÙˆØ±ÙˆØ¯'),
        -- Uploads
        (N'MaxUploadSizeMb', N'2', N'Uploads', N'int', N'Ø­Ø¯Ø§Ú©Ø«Ø± Ø­Ø¬Ù… Ø¢Ù¾Ù„ÙˆØ¯ (Ù…Ú¯Ø§Ø¨Ø§ÛŒØª)'),
        (N'AllowedImageFormats', N'jpg,jpeg,png,webp', N'Uploads', N'string', N'ÙØ±Ù…Øªâ€ŒÙ‡Ø§ÛŒ Ù…Ø¬Ø§Ø² ØªØµÙˆÛŒØ±')
    ) AS v([Key], [Value], GroupName, ValueType, [Description])
)
INSERT INTO Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), s.[Key], s.[Value], s.GroupName, s.ValueType, s.[Description], SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM Settings e WHERE e.[Key] = s.[Key]);

PRINT 'Vitorize UI/UX settings seed complete. Existing values were preserved.';

GO
/* Source: Database/2026-07-13_seed_sms_settings.sql */
-- ============================================================================
-- Vitorize â€” SMS (SMS.ir) settings seed  (2026-07-13)
-- NO SCHEMA CHANGES. Data-only. Idempotent â€” safe to run repeatedly.
--
-- Provisions every Sms.* Setting key used by the SMS subsystem. EXISTING ROWS
-- ARE NEVER TOUCHED (production values, including the API key, are preserved).
--
-- This mirrors VitorizeSeedService.SeedSettingsAsync, which also runs at API
-- startup. Applying this script is OPTIONAL â€” it only lets you provision the
-- keys without an app restart.
--
-- SECURITY: The "SMS" group is NOT part of the public settings endpoint, and
-- Sms.ApiKey / Sms.DefaultLineNumber are additionally masked by the admin API.
-- Seeded secret values are EMPTY; set them from the Admin â€º Settings â€º SMS panel.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Optional deployment inputs. Leave blank and enter the two IDs in Admin,
-- or set them before running this script to seed/synchronize all compatibility keys.
DECLARE @UniversalOtpTemplateId nvarchar(50) = N'';
DECLARE @UniversalNotificationTemplateId nvarchar(50) = N'';

;WITH seed([Key], [Value], GroupName, ValueType, [Description]) AS (
    SELECT * FROM (VALUES
        (N'Sms.IsEnabled',                     N'false',   N'SMS', N'bool',   N'ÙØ¹Ø§Ù„â€ŒØ³Ø§Ø²ÛŒ Ø³Ø±ÙˆÛŒØ³ Ù¾ÛŒØ§Ù…Ú© SMS.ir'),
        (N'Sms.Provider',                      N'SMS.ir',  N'SMS', N'string', N'Ø§Ø±Ø§Ø¦Ù‡â€ŒØ¯Ù‡Ù†Ø¯Ù‡ Ù¾ÛŒØ§Ù…Ú©'),
        (N'Sms.ApiKey',                        N'',        N'SMS', N'secret', N'Ú©Ù„ÛŒØ¯ API Ù¾Ù†Ù„ SMS.ir (Ù…Ø­Ø±Ù…Ø§Ù†Ù‡)'),
        (N'Sms.DefaultLineNumber',             N'',        N'SMS', N'string', N'Ø´Ù…Ø§Ø±Ù‡ Ø®Ø· Ø§Ø®ØªØµØ§ØµÛŒ Ø¨Ø±Ø§ÛŒ Ù¾ÛŒØ§Ù…Ú© Ù…ØªÙ†ÛŒ (Ù…Ø­Ø±Ù…Ø§Ù†Ù‡)'),
        (N'Sms.SenderName',                    N'ÙˆÛŒØªÙˆØ±Ø§ÛŒØ²', N'SMS', N'string', N'Ù†Ø§Ù… ÙØ±Ø³ØªÙ†Ø¯Ù‡'),
        (N'Sms.OtpTemplateId',                 @UniversalOtpTemplateId,          N'SMS', N'int', N'Ø´Ù†Ø§Ø³Ù‡ Ù‚Ø§Ù„Ø¨ Ú©Ø¯ ÛŒÚ©Ø¨Ø§Ø± Ù…ØµØ±Ù'),
        (N'Sms.NotificationTemplateId',        @UniversalNotificationTemplateId, N'SMS', N'int', N'Ø´Ù†Ø§Ø³Ù‡ Ù‚Ø§Ù„Ø¨ Ø§Ø·Ù„Ø§Ø¹â€ŒØ±Ø³Ø§Ù†ÛŒ Ø¹Ù…ÙˆÙ…ÛŒ'),
        (N'Sms.LoginOtpTemplateId',            @UniversalOtpTemplateId,          N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.RegisterOtpTemplateId',         @UniversalOtpTemplateId,          N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.ForgotPasswordTemplateId',      @UniversalOtpTemplateId,          N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
        (N'Sms.OrderPaidTemplateId',           @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OrderCompletedTemplateId',      @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OrderStatusChangedTemplateId',  @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.GiftCodeDeliveredTemplateId',   @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.TicketReplyTemplateId',         @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.VerificationApprovedTemplateId',@UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.VerificationRejectedTemplateId',@UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.WalletTopUpSuccessTemplateId',  @UniversalNotificationTemplateId, N'SMS', N'int', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
        (N'Sms.OtpExpiryMinutes',              N'3',       N'SMS', N'int',    N'Ù…Ø¯Øª Ø§Ø¹ØªØ¨Ø§Ø± Ú©Ø¯ ÛŒÚ©Ø¨Ø§Ø±â€ŒÙ…ØµØ±Ù (Ø¯Ù‚ÛŒÙ‚Ù‡)'),
        (N'Sms.OtpResendCooldownSeconds',      N'90',      N'SMS', N'int',    N'ÙØ§ØµÙ„Ù‡ Ø§Ø±Ø³Ø§Ù„ Ù…Ø¬Ø¯Ø¯ Ú©Ø¯ (Ø«Ø§Ù†ÛŒÙ‡)'),
        (N'Sms.OtpMaxAttempts',                N'5',       N'SMS', N'int',    N'Ø­Ø¯Ø§Ú©Ø«Ø± ØªÙ„Ø§Ø´ Ù…Ø¬Ø§Ø² Ø¨Ø±Ø§ÛŒ Ù‡Ø± Ú©Ø¯'),
        (N'Sms.DailyOtpLimitPerMobile',        N'10',      N'SMS', N'int',    N'Ø³Ù‚Ù Ú©Ø¯ Ø±ÙˆØ²Ø§Ù†Ù‡ Ø¨Ø±Ø§ÛŒ Ù‡Ø± Ø´Ù…Ø§Ø±Ù‡'),
        (N'Sms.DailySmsLimitPerMobile',        N'30',      N'SMS', N'int',    N'Ø³Ù‚Ù Ù¾ÛŒØ§Ù…Ú© Ø±ÙˆØ²Ø§Ù†Ù‡ Ø¨Ø±Ø§ÛŒ Ù‡Ø± Ø´Ù…Ø§Ø±Ù‡'),
        (N'Sms.MaxRetryCount',                 N'5',       N'SMS', N'int',    N'Ø­Ø¯Ø§Ú©Ø«Ø± ØªØ¹Ø¯Ø§Ø¯ Ø¨Ø§Ø²ØªÙ„Ø§Ø´ Ø§Ø±Ø³Ø§Ù„'),
        (N'Sms.RetryDelaySeconds',             N'30',      N'SMS', N'int',    N'Ù¾Ø§ÛŒÙ‡ ØªØ£Ø®ÛŒØ± Ø¨Ø§Ø²ØªÙ„Ø§Ø´ (Ø«Ø§Ù†ÛŒÙ‡)'),
        (N'Sms.UseOutbox',                     N'true',    N'SMS', N'bool',   N'Ø§Ø±Ø³Ø§Ù„ Ù¾ÛŒØ§Ù…Ú© Ø±ÙˆÛŒØ¯Ø§Ø¯Ù‡Ø§ÛŒ ØªØ¬Ø§Ø±ÛŒ Ø§Ø² Ø·Ø±ÛŒÙ‚ Outbox'),
        (N'Sms.CustomSendEnabled',              N'false',   N'SMS', N'bool',   N'ÙØ¹Ø§Ù„â€ŒØ³Ø§Ø²ÛŒ Ø§Ø±Ø³Ø§Ù„ Ù¾ÛŒØ§Ù…Ú© Ø³ÙØ§Ø±Ø´ÛŒ ØªÙˆØ³Ø· Ù…Ø¯ÛŒØ±'),
        (N'Sms.CustomTextEnabled',              N'false',   N'SMS', N'bool',   N'ÙØ¹Ø§Ù„â€ŒØ³Ø§Ø²ÛŒ Ù¾ÛŒØ§Ù…Ú© Ù…ØªÙ†ÛŒ Ø³ÙØ§Ø±Ø´ÛŒ'),
        (N'Sms.MaxCustomRecipients',            N'1',       N'SMS', N'int',    N'Ø­Ø¯Ø§Ú©Ø«Ø± Ú¯ÛŒØ±Ù†Ø¯Ù‡ Ø¯Ø± Ù‡Ø± Ø§Ø±Ø³Ø§Ù„ Ø³ÙØ§Ø±Ø´ÛŒ'),
        (N'Sms.MaxCustomTextLength',            N'500',     N'SMS', N'int',    N'Ø­Ø¯Ø§Ú©Ø«Ø± Ø·ÙˆÙ„ Ù¾ÛŒØ§Ù…Ú© Ù…ØªÙ†ÛŒ Ø³ÙØ§Ø±Ø´ÛŒ'),
        (N'Sms.RequireConfirmation',            N'true',    N'SMS', N'bool',   N'Ù†ÛŒØ§Ø² Ø¨Ù‡ ØªØ§ÛŒÛŒØ¯ Ù†Ù‡Ø§ÛŒÛŒ Ù¾ÛŒØ´ Ø§Ø² Ø§Ø±Ø³Ø§Ù„ Ø³ÙØ§Ø±Ø´ÛŒ'),
        (N'Sms.AllowImmediateSend',             N'false',   N'SMS', N'bool',   N'Ø§Ø¬Ø§Ø²Ù‡ Ø§Ø±Ø³Ø§Ù„ ÙÙˆØ±ÛŒ Ø¨Ù‡ Ø¬Ø§ÛŒ ØµÙ'),
        (N'Sms.HistoryRetentionDays',           N'180',     N'SMS', N'int',    N'Ù…Ø¯Øª Ù†Ú¯Ù‡Ø¯Ø§Ø±ÛŒ ØªØ§Ø±ÛŒØ®Ú†Ù‡ Ù¾ÛŒØ§Ù…Ú© Ø¨Ø± Ø­Ø³Ø¨ Ø±ÙˆØ²'),
        (N'Sms.MaskMobileInAdmin',              N'true',    N'SMS', N'bool',   N'Ù¾Ù†Ù‡Ø§Ù†â€ŒØ³Ø§Ø²ÛŒ Ø´Ù…Ø§Ø±Ù‡ Ù…ÙˆØ¨Ø§ÛŒÙ„ Ø¯Ø± ØªØ§Ø±ÛŒØ®Ú†Ù‡ Ù…Ø¯ÛŒØ±'),
        (N'Sms.AllowAdminViewFullMobile',       N'false',   N'SMS', N'bool',   N'Ø§Ø¬Ø§Ø²Ù‡ Ù…Ø´Ø§Ù‡Ø¯Ù‡ Ø´Ù…Ø§Ø±Ù‡ Ú©Ø§Ù…Ù„ Ø¨Ø±Ø§ÛŒ Ù…Ø¯ÛŒØ± Ú©Ù„'),
        (N'Sms.AllowRetryFailed',               N'true',    N'SMS', N'bool',   N'Ø§Ø¬Ø§Ø²Ù‡ Ø¨Ø§Ø²ØªÙ„Ø§Ø´ Ø§Ù…Ù† Ù¾ÛŒØ§Ù…Ú© Ù†Ø§Ù…ÙˆÙÙ‚'),
        (N'Sms.LogSensitiveData',              N'false',   N'SMS', N'bool',   N'Ù„Ø§Ú¯â€ŒÚ©Ø±Ø¯Ù† Ø¯Ø§Ø¯Ù‡ Ø­Ø³Ø§Ø³ (ÙÙ‚Ø· ØªÙˆØ³Ø¹Ù‡)')
    ) AS v([Key], [Value], GroupName, ValueType, [Description])
)
INSERT INTO Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), s.[Key], s.[Value], s.GroupName, s.ValueType, s.[Description], SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM Settings e WHERE e.[Key] = s.[Key]);

-- Refresh template metadata on existing installations without touching their IDs.
UPDATE target
SET [Description] = metadata.[Description],
    GroupName = N'SMS',
    ValueType = N'int',
    UpdatedAt = SYSUTCDATETIME()
FROM Settings target
INNER JOIN (VALUES
    (N'Sms.OtpTemplateId',                  N'Ø´Ù†Ø§Ø³Ù‡ Ù‚Ø§Ù„Ø¨ Ú©Ø¯ ÛŒÚ©Ø¨Ø§Ø± Ù…ØµØ±Ù'),
    (N'Sms.NotificationTemplateId',         N'Ø´Ù†Ø§Ø³Ù‡ Ù‚Ø§Ù„Ø¨ Ø§Ø·Ù„Ø§Ø¹â€ŒØ±Ø³Ø§Ù†ÛŒ Ø¹Ù…ÙˆÙ…ÛŒ'),
    (N'Sms.LoginOtpTemplateId',             N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.RegisterOtpTemplateId',          N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.ForgotPasswordTemplateId',       N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ OTPØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.OtpTemplateId (CODE, EXPIRE)'),
    (N'Sms.OrderPaidTemplateId',            N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.OrderCompletedTemplateId',       N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.OrderStatusChangedTemplateId',   N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.GiftCodeDeliveredTemplateId',    N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.TicketReplyTemplateId',          N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.VerificationApprovedTemplateId', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.VerificationRejectedTemplateId', N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)'),
    (N'Sms.WalletTopUpSuccessTemplateId',   N'Ú©Ù„ÛŒØ¯ Ø³Ø§Ø²Ú¯Ø§Ø±ÛŒ Ø§Ø·Ù„Ø§Ø¹ Ø±Ø³Ø§Ù†ÛŒØ› Ù‡Ù…Ú¯Ø§Ù… Ø¨Ø§ Sms.NotificationTemplateId (ORDER_NUMBER)')
) metadata([Key], [Description]) ON metadata.[Key] = target.[Key]
WHERE ISNULL(target.[Description], N'') <> metadata.[Description]
   OR ISNULL(target.GroupName, N'') <> N'SMS'
   OR ISNULL(target.ValueType, N'') <> N'int';

-- If deployment variables were supplied, synchronize existing compatibility rows too.
IF NULLIF(@UniversalOtpTemplateId, N'') IS NOT NULL
    UPDATE Settings
    SET [Value] = @UniversalOtpTemplateId, UpdatedAt = SYSUTCDATETIME()
    WHERE [Key] IN (
        N'Sms.OtpTemplateId', N'Sms.LoginOtpTemplateId',
        N'Sms.RegisterOtpTemplateId', N'Sms.ForgotPasswordTemplateId');

IF NULLIF(@UniversalNotificationTemplateId, N'') IS NOT NULL
    UPDATE Settings
    SET [Value] = @UniversalNotificationTemplateId, UpdatedAt = SYSUTCDATETIME()
    WHERE [Key] IN (
        N'Sms.NotificationTemplateId', N'Sms.OrderPaidTemplateId',
        N'Sms.OrderCompletedTemplateId', N'Sms.OrderStatusChangedTemplateId',
        N'Sms.GiftCodeDeliveredTemplateId', N'Sms.TicketReplyTemplateId',
        N'Sms.VerificationApprovedTemplateId', N'Sms.VerificationRejectedTemplateId',
        N'Sms.WalletTopUpSuccessTemplateId');

PRINT 'Vitorize SMS settings seed complete. Existing values (including any API key) were preserved.';

GO
/* Source: Database/2026-07-14_seed_product_experience_settings.sql */
/* Run after 2026-07-14_product_experience_schema.sql. Inserts missing keys only. */
SET NOCOUNT ON;
DECLARE @Seed TABLE ([Key] nvarchar(200), [Value] nvarchar(max), GroupName nvarchar(100), ValueType nvarchar(50), [Description] nvarchar(500));
INSERT @Seed VALUES
(N'StorefrontPersianFont',N'Peyda',N'Typography',N'font',N'Default Persian storefront font.'),
(N'StorefrontEnglishFont',N'Funnel Display',N'Typography',N'font',N'Default English storefront font.'),
(N'Typography.FontFamily',N'Vazirmatn',N'Typography',N'string',N'Ù†Ø§Ù… ÙÙˆÙ†Øª ÙØ¹Ø§Ù„Ø› Ù¾ÛŒØ´â€ŒÙØ±Ø¶ Vazirmatn'),
(N'Typography.FontPath',N'',N'Typography',N'string',N'Ù…Ø³ÛŒØ± ÙØ§ÛŒÙ„ ÙÙˆÙ†Øª ÙØ¹Ø§Ù„'),
(N'Typography.FontFormat',N'woff2',N'Typography',N'string',N'ÙØ±Ù…Øª ÙØ§ÛŒÙ„ ÙÙˆÙ†Øª ÙØ¹Ø§Ù„'),
(N'Typography.Scope',N'3',N'Typography',N'int',N'Ù…Ø­Ø¯ÙˆØ¯Ù‡ Ø§Ø¹Ù…Ø§Ù„: Û± ÙØ±ÙˆØ´Ú¯Ø§Ù‡ØŒ Û² Ù…Ø¯ÛŒØ±ÛŒØªØŒ Û³ Ú©Ù„ Ø¨Ø±Ù†Ø§Ù…Ù‡'),
(N'Typography.Version',N'1',N'Typography',N'string',N'Ù†Ø³Ø®Ù‡ Ú©Ø´ ÙÙˆÙ†Øª'),
(N'Typography.MaxUploadMb',N'5',N'Typography',N'int',N'Ø­Ø¯Ø§Ú©Ø«Ø± Ø­Ø¬Ù… ÙÙˆÙ†Øª'),
(N'Branding.AssetVersion',N'1',N'Branding',N'string',N'Ù†Ø³Ø®Ù‡ Ú©Ø´ Ø¯Ø§Ø±Ø§ÛŒÛŒâ€ŒÙ‡Ø§ÛŒ Ø¨Ø±Ù†Ø¯'),
(N'TrustSeal.Enamad.Enabled',N'false',N'TrustSeals',N'bool',N'Ù†Ù…Ø§ÛŒØ´ Enamad'),
(N'TrustSeal.Enamad.Title',N'Ù†Ù…Ø§Ø¯ Ø§Ø¹ØªÙ…Ø§Ø¯ Ø§Ù„Ú©ØªØ±ÙˆÙ†ÛŒÚ©ÛŒ',N'TrustSeals',N'string',N'Ø¹Ù†ÙˆØ§Ù† Ù†Ù…Ø§Ø¯'),
(N'TrustSeal.Enamad.Url',N'',N'TrustSeals',N'string',N'Ù†Ø´Ø§Ù†ÛŒ HTTPS Ø±Ø³Ù…ÛŒ enamad.ir'),
(N'TrustSeal.Enamad.ImagePath',N'',N'TrustSeals',N'image',N'ØªØµÙˆÛŒØ± Ù†Ù…Ø§Ø¯'),
(N'TrustSeal.Enamad.Alt',N'Ù†Ù…Ø§Ø¯ Ø§Ø¹ØªÙ…Ø§Ø¯ Ø§Ù„Ú©ØªØ±ÙˆÙ†ÛŒÚ©ÛŒ',N'TrustSeals',N'string',N'Ù…ØªÙ† Ø¬Ø§ÛŒÚ¯Ø²ÛŒÙ†'),
(N'TrustSeal.Enamad.SortOrder',N'10',N'TrustSeals',N'int',N'ØªØ±ØªÛŒØ¨ Ù†Ù…Ø§ÛŒØ´'),
(N'TrustSeal.Enamad.NewTab',N'true',N'TrustSeals',N'bool',N'Ø¨Ø§Ø² Ø´Ø¯Ù† Ø¯Ø± Ø²Ø¨Ø§Ù†Ù‡ Ø¬Ø¯ÛŒØ¯'),
(N'TrustSeal.Ecunion.Enabled',N'false',N'TrustSeals',N'bool',N'Ù†Ù…Ø§ÛŒØ´ ecunion'),
(N'TrustSeal.Ecunion.Title',N'Ø§ØªØ­Ø§Ø¯ÛŒÙ‡ Ú©Ø³Ø¨â€ŒÙˆÚ©Ø§Ø±Ù‡Ø§ÛŒ Ù…Ø¬Ø§Ø²ÛŒ',N'TrustSeals',N'string',N'Ø¹Ù†ÙˆØ§Ù† Ù…Ø¬ÙˆØ²'),
(N'TrustSeal.Ecunion.Url',N'',N'TrustSeals',N'string',N'Ù†Ø´Ø§Ù†ÛŒ HTTPS Ø±Ø³Ù…ÛŒ ecunion.ir'),
(N'TrustSeal.Ecunion.ImagePath',N'',N'TrustSeals',N'image',N'ØªØµÙˆÛŒØ± Ù…Ø¬ÙˆØ²'),
(N'TrustSeal.Ecunion.Alt',N'Ù…Ø¬ÙˆØ² Ø§ØªØ­Ø§Ø¯ÛŒÙ‡ Ú©Ø³Ø¨â€ŒÙˆÚ©Ø§Ø±Ù‡Ø§ÛŒ Ù…Ø¬Ø§Ø²ÛŒ',N'TrustSeals',N'string',N'Ù…ØªÙ† Ø¬Ø§ÛŒÚ¯Ø²ÛŒÙ†'),
(N'TrustSeal.Ecunion.SortOrder',N'20',N'TrustSeals',N'int',N'ØªØ±ØªÛŒØ¨ Ù†Ù…Ø§ÛŒØ´'),
(N'TrustSeal.Ecunion.NewTab',N'true',N'TrustSeals',N'bool',N'Ø¨Ø§Ø² Ø´Ø¯Ù† Ø¯Ø± Ø²Ø¨Ø§Ù†Ù‡ Ø¬Ø¯ÛŒØ¯'),
(N'TrustSeal.Samandehi.Enabled',N'false',N'TrustSeals',N'bool',N'Ù†Ù…Ø§ÛŒØ´ Ø³Ø§Ù…Ø§Ù†Ø¯Ù‡ÛŒ'),
(N'TrustSeal.Samandehi.Title',N'Ù†Ø´Ø§Ù† Ù…Ù„ÛŒ Ø«Ø¨Øª Ø±Ø³Ø§Ù†Ù‡â€ŒÙ‡Ø§ÛŒ Ø¯ÛŒØ¬ÛŒØªØ§Ù„',N'TrustSeals',N'string',N'Ø¹Ù†ÙˆØ§Ù† Ù†Ø´Ø§Ù†'),
(N'TrustSeal.Samandehi.Url',N'',N'TrustSeals',N'string',N'Ù†Ø´Ø§Ù†ÛŒ HTTPS Ø±Ø³Ù…ÛŒ samandehi.ir'),
(N'TrustSeal.Samandehi.ImagePath',N'',N'TrustSeals',N'image',N'ØªØµÙˆÛŒØ± Ù†Ø´Ø§Ù†'),
(N'TrustSeal.Samandehi.Alt',N'Ù†Ø´Ø§Ù† Ø³Ø§Ù…Ø§Ù†Ø¯Ù‡ÛŒ',N'TrustSeals',N'string',N'Ù…ØªÙ† Ø¬Ø§ÛŒÚ¯Ø²ÛŒÙ†'),
(N'TrustSeal.Samandehi.SortOrder',N'30',N'TrustSeals',N'int',N'ØªØ±ØªÛŒØ¨ Ù†Ù…Ø§ÛŒØ´'),
(N'TrustSeal.Samandehi.NewTab',N'true',N'TrustSeals',N'bool',N'Ø¨Ø§Ø² Ø´Ø¯Ù† Ø¯Ø± Ø²Ø¨Ø§Ù†Ù‡ Ø¬Ø¯ÛŒØ¯');

INSERT dbo.Settings (Id,[Key],[Value],GroupName,ValueType,[Description],UpdatedAt)
SELECT NEWID(),s.[Key],s.[Value],s.GroupName,s.ValueType,s.[Description],SYSUTCDATETIME()
FROM @Seed s WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings x WHERE x.[Key]=s.[Key]);

GO
/* Required pre-startup payment configuration. Values are syntactically valid
   non-live placeholders; deployers must replace merchant ID/callback before enabling payments. */
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @RequiredPaymentSettings TABLE ([Key] nvarchar(200) NOT NULL PRIMARY KEY, [Value] nvarchar(max) NOT NULL, [Description] nvarchar(500) NOT NULL);
    INSERT @RequiredPaymentSettings VALUES
      (N'ZarinpalMerchantId',N'00000000-0000-0000-0000-000000000000',N'Non-live deployment sentinel; replace through protected admin configuration before payment activation.'),
      (N'ZarinpalSandbox',N'false',N'Production-safe default; live gateway certification is required before payments are enabled.'),
      (N'ZarinpalBaseUrl',N'https://payment.zarinpal.com/pg/v4/payment',N'Canonical production gateway base URL.'),
      (N'ZarinpalStartPayUrl',N'https://payment.zarinpal.com/pg/StartPay',N'Canonical production start-payment URL.'),
      (N'ZarinpalCallbackUrl',N'https://vitorize.invalid/api/payments/zarinpal/callback',N'Non-live placeholder; replace with the final HTTPS public callback URL before payment activation.');
    INSERT dbo.Settings (Id,[Key],[Value],GroupName,ValueType,[Description],UpdatedAt)
    SELECT NEWID(),s.[Key],s.[Value],N'Payment',N'string',s.[Description],SYSUTCDATETIME()
    FROM @RequiredPaymentSettings s WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings x WHERE x.[Key]=s.[Key]);
    COMMIT;
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
GO
/* Immutable clean-bootstrap deployment ledger. */
INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes) SELECT v.n,v.v,v.h,N'Production',1,N'Included in clean-production bootstrap' FROM (VALUES (N'V0001__create_database_script_history.sql',N'V0001',N'0d95329a1e6b5eafbb377b6898f6f43ade76054ad22c970a00c92ffcdc8c6053'),(N'V0002__normalize_gift_code_reservation_status_constraint.sql',N'V0002',N'918491680f470df380fff99caaa3b291b8e3354309e28b144945950ae7bc4b45'),(N'2026-07-13_create_sms_history.sql',N'H20260713-SMS-SCHEMA',N'ece5f2dbebf7266c2c58e079377148a43bc02699d31ff9c3e853ca30b731a8f0'),(N'2026-07-14_product_experience_schema.sql',N'H20260714-PRODUCT-SCHEMA',N'907cabcb1eefb753ae3b2ff19add608d2f011c448295f2e39a2a22e3799c393c'),(N'V0003__seed_reference_roles.sql',N'V0003',N'9cd5ff472bb5d776269b43f14565870c6c1de862b0a275a36e342138e635be35'),(N'V0004__financial_integrity_and_security_hardening.sql',N'V0004',N'8a896e8cdbfbee4d84a0c6415192c03cd4fda4088b51828acb73f9ea5c862ef4'),(N'V0005__seo_content_and_legacy_redirects.sql',N'V0005',N'ed6b02b7453590d09fc2d1a085ea3e8f006ab66659c046c911196d7af8955b22'),(N'V0006__preserve_currency_through_checkout.sql',N'V0006',N'70c4485300b40cc94547177682fba3e82e90a7deb1937d2a66c27ea4be1287cc'),(N'V0007__support_fulfillment_ticket_uniqueness.sql',N'V0007',N'b39587eed17e512d60e6db99986d488f1d770c54b02f8cee4fac3e54331d2a10'),(N'2026-07-08_seed_settings_ui_customization.sql',N'H20260708-UI',N'a9da7ed7e2b87e27298b8005befb10954c228a574786c3cf14f9db8c535b2ed3'),(N'2026-07-13_seed_sms_settings.sql',N'H20260713-SMS-SEED',N'a950e3b326fe99e197c6e08c0024e0a601e7bfdbcfceb130a40736f8281f2b6e'),(N'2026-07-14_seed_product_experience_settings.sql',N'H20260714-PRODUCT-SEED',N'90ae9b6278a85536accf28e7a927755b980cc062b07afb65d1a6d43fcaad4c00'))v(n,v,h) WHERE NOT EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory h WHERE h.ScriptVersion=v.v);
