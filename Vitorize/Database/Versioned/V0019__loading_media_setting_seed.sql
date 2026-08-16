/*
    FIX-17 — admin-configurable initial loading media.

    Registers the LoadingMediaPath setting so it appears on the Admin "لوگو و تصاویر" tab.
    The seeded value is intentionally empty: an empty value means "use the built-in Vitorize
    loader", which is the documented fallback. This data-only migration is idempotent and never
    overwrites an administrator-selected value.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @LoadingMedia TABLE
(
    [Key] nvarchar(200) NOT NULL PRIMARY KEY,
    [Value] nvarchar(max) NOT NULL,
    GroupName nvarchar(100) NOT NULL,
    ValueType nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL
);

INSERT @LoadingMedia ([Key], [Value], GroupName, ValueType, [Description]) VALUES
    (N'LoadingMediaPath', N'', N'Logos', N'image', N'تصویر یا GIF بارگذاری اولیه (خالی = لودر پیش‌فرض ویتورایز)');

INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), seed.[Key], seed.[Value], seed.GroupName, seed.ValueType, seed.[Description], SYSUTCDATETIME()
FROM @LoadingMedia seed
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Settings existing
    WHERE existing.[Key] = seed.[Key]
);
