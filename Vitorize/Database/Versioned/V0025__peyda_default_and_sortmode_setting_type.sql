/*
    Final pre-deployment fixes: two settings rows, no schema change.

    Both are data corrections that the seeder cannot make. SeedSettingsAsync inserts a key only when
    it is absent and never touches an existing row - deliberately, so a redeploy cannot overwrite an
    administrator's choices - which means changing a default in code has no effect on a database that
    already holds the key. These two rows have to be corrected explicitly.

    1) StorefrontPersianFont -> Peyda

       V0023 moved this the other way, from Peyda to Vazirmatn, because a fresh install rendered the
       storefront in a different family from the admin panel. The inconsistency was real; the remedy
       picked the wrong side of it. Peyda is the intended face, and the typography contract has since
       been unified so one setting now governs the storefront, the customer panel and the admin panel
       together - admin.css previously did not even declare Peyda, so no setting could have reached it.

       Guarded the same way V0023 guarded its own change: only rows still holding the value that
       migration wrote are moved. An administrator who has since chosen a font keeps it.

    2) StorefrontDefaultProductSort -> ValueType 'sortmode'

       The key was seeded as a plain 'string', and the admin Settings screen renders anything it does
       not recognise as a free-text box. So the storefront's default ordering - a fixed set of seven
       modes - was editable as arbitrary text, and an unrecognised value was silently ignored by the
       query layer, which is why it looked like the setting simply did nothing. The 'sortmode' type
       renders the same select the Products page already uses, and the value is validated server-side.

       Any value outside the supported set is normalised to the default rather than left in place, so
       the select has something valid to preselect.

    Idempotent: every statement is guarded or naturally repeatable, so a second deployment is a no-op.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

-- 1 ---------------------------------------------------------------- Peyda as the default face
UPDATE dbo.Settings
SET    [Value] = N'Peyda',
       UpdatedAt = SYSUTCDATETIME()
WHERE  [Key] = N'StorefrontPersianFont'
  AND  [Value] = N'Vazirmatn';

-- 2 ---------------------------------------------------------------- the default sort is an enum
UPDATE dbo.Settings
SET    ValueType = N'sortmode',
       UpdatedAt = SYSUTCDATETIME()
WHERE  [Key] = N'StorefrontDefaultProductSort'
  AND  (ValueType IS NULL OR ValueType <> N'sortmode');

-- A value outside the supported set would leave the new select with nothing selected.
UPDATE dbo.Settings
SET    [Value] = N'AvailabilityFirst',
       UpdatedAt = SYSUTCDATETIME()
WHERE  [Key] = N'StorefrontDefaultProductSort'
  AND  ([Value] IS NULL OR [Value] NOT IN
        (N'AvailabilityFirst', N'BestSelling', N'Newest', N'Oldest',
         N'PriceLowToHigh', N'PriceHighToLow', N'MostDiscounted'));

COMMIT TRANSACTION;

-- ---------------------------------------------------------------- verification
DECLARE @errors nvarchar(max) = N'';

IF EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'StorefrontPersianFont' AND [Value] = N'Vazirmatn')
    SET @errors = @errors + N'StorefrontPersianFont still holds the value V0023 set. ';

IF EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'StorefrontDefaultProductSort' AND ValueType <> N'sortmode')
    SET @errors = @errors + N'StorefrontDefaultProductSort is not typed as sortmode. ';

IF EXISTS (
    SELECT 1 FROM dbo.Settings
    WHERE [Key] = N'StorefrontDefaultProductSort'
      AND [Value] NOT IN (N'AvailabilityFirst', N'BestSelling', N'Newest', N'Oldest',
                          N'PriceLowToHigh', N'PriceHighToLow', N'MostDiscounted'))
    SET @errors = @errors + N'StorefrontDefaultProductSort holds an unsupported ordering. ';

IF @errors <> N''
    THROW 51025, @errors, 1;
