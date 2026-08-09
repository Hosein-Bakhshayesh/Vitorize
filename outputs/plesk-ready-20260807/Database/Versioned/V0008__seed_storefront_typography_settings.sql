/*
    Storefront typography settings. This data-only migration is idempotent:
    existing administrator-selected values are never overwritten.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @StorefrontTypography TABLE
(
    [Key] nvarchar(200) NOT NULL PRIMARY KEY,
    [Value] nvarchar(max) NOT NULL,
    GroupName nvarchar(100) NOT NULL,
    ValueType nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL
);

INSERT @StorefrontTypography ([Key], [Value], GroupName, ValueType, [Description]) VALUES
    (N'StorefrontPersianFont', N'Peyda', N'Typography', N'font', N'Default Persian storefront font.'),
    (N'StorefrontEnglishFont', N'Funnel Display', N'Typography', N'font', N'Default English storefront font.');

INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), seed.[Key], seed.[Value], seed.GroupName, seed.ValueType, seed.[Description], SYSUTCDATETIME()
FROM @StorefrontTypography seed
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Settings existing
    WHERE existing.[Key] = seed.[Key]
);
