/*
  OPTIONAL, IDEMPOTENT data cleanup for legacy Vitorize icon aliases.
  The application already renders these aliases safely, so this script is not required for deployment.
  Review affected rows before execution and back up the database. No schema changes are made.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

UPDATE dbo.Categories
SET Icon = CASE LOWER(LTRIM(RTRIM(Icon)))
    WHEN 'alert' THEN 'triangle-alert'
    WHEN 'bar-chart' THEN 'chart-no-axes-column'
    WHEN 'cart' THEN 'shopping-cart'
    WHEN 'check-circle' THEN 'circle-check'
    WHEN 'dashboard' THEN 'layout-dashboard'
    WHEN 'dots' THEN 'ellipsis'
    WHEN 'edit' THEN 'pencil'
    WHEN 'external' THEN 'external-link'
    WHEN 'grid' THEN 'layout-grid'
    WHEN 'logout' THEN 'log-out'
    WHEN 'message' THEN 'message-circle'
    WHEN 'refresh' THEN 'refresh-cw'
    WHEN 'sliders' THEN 'sliders-horizontal'
    ELSE Icon END
WHERE LOWER(LTRIM(RTRIM(Icon))) IN
    ('alert','bar-chart','cart','check-circle','dashboard','dots','edit','external','grid','logout','message','refresh','sliders');

IF OBJECT_ID(N'dbo.ProductFeatures', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.ProductFeatures
    SET IconKey = CASE LOWER(LTRIM(RTRIM(IconKey)))
        WHEN 'alert' THEN 'triangle-alert' WHEN 'bar-chart' THEN 'chart-no-axes-column'
        WHEN 'cart' THEN 'shopping-cart' WHEN 'check-circle' THEN 'circle-check'
        WHEN 'dashboard' THEN 'layout-dashboard' WHEN 'dots' THEN 'ellipsis'
        WHEN 'edit' THEN 'pencil' WHEN 'external' THEN 'external-link'
        WHEN 'grid' THEN 'layout-grid' WHEN 'logout' THEN 'log-out'
        WHEN 'message' THEN 'message-circle' WHEN 'refresh' THEN 'refresh-cw'
        WHEN 'sliders' THEN 'sliders-horizontal' ELSE IconKey END
    WHERE LOWER(LTRIM(RTRIM(IconKey))) IN
        ('alert','bar-chart','cart','check-circle','dashboard','dots','edit','external','grid','logout','message','refresh','sliders');
END;

UPDATE dbo.Settings
SET [Value] = REPLACE([Value], N'"icon":"grid"', N'"icon":"layout-grid"'),
    UpdatedAt = SYSUTCDATETIME()
WHERE [Key] IN (N'TrustBadgesJson', N'HomeFeaturesJson')
  AND [Value] LIKE N'%"icon":"grid"%';

COMMIT TRANSACTION;
