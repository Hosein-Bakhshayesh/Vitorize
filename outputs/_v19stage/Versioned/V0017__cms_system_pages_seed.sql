/*
  FIX-14 (Client Issue #1): CMS system page identity and seed.

  1. Adds dbo.Pages.IsSystem so About/Terms/Privacy/Contact can be protected from deletion and
     from having their slug renamed. Existing rows default to 0 (custom).
  2. Idempotently seeds the four system pages as UNPUBLISHED. Publishing incomplete legal or
     company content automatically would be worse than a 404, so an administrator must fill the
     content and publish explicitly.

  Rerun safety: an existing row with a system slug is only marked IsSystem = 1; its Title,
  ContentHtml and SEO fields are never overwritten by the seed text.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Pages', N'IsSystem') IS NULL
    ALTER TABLE dbo.Pages ADD IsSystem bit NOT NULL CONSTRAINT DF_Pages_IsSystem DEFAULT (0);

-- SQL Server binds names for the whole batch before executing ALTER TABLE. Start a new batch so
-- the statements below can reference the column added above.
GO

DECLARE @SystemPages TABLE
(
    Slug nvarchar(250) NOT NULL PRIMARY KEY,
    Title nvarchar(250) NOT NULL,
    ContentHtml nvarchar(max) NOT NULL,
    SeoDescription nvarchar(500) NOT NULL
);

-- Short, professional Persian starter text only where a NOT NULL column requires a value.
INSERT @SystemPages (Slug, Title, ContentHtml, SeoDescription) VALUES
    (N'about',   N'درباره ما',          N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'معرفی فروشگاه ویتورایز'),
    (N'terms',   N'قوانین و مقررات',    N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'قوانین و مقررات استفاده از فروشگاه ویتورایز'),
    (N'privacy', N'حریم خصوصی',         N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'سیاست حریم خصوصی فروشگاه ویتورایز'),
    (N'contact', N'تماس با ما',          N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'راه‌های ارتباط با پشتیبانی ویتورایز');

-- Insert only the system pages that do not exist yet.
INSERT dbo.Pages (Id, Title, Slug, ContentHtml, SeoTitle, SeoDescription, IsPublished, IsSystem, CreatedAt)
SELECT NEWID(), seed.Title, seed.Slug, seed.ContentHtml, seed.Title, seed.SeoDescription, 0, 1, SYSUTCDATETIME()
FROM @SystemPages seed
WHERE NOT EXISTS (SELECT 1 FROM dbo.Pages existing WHERE existing.Slug = seed.Slug);

-- An already-present page with a system slug keeps all of its administrator-authored content and
-- its current publication state; only the system identity flag is applied.
UPDATE p
SET p.IsSystem = 1
FROM dbo.Pages p
INNER JOIN @SystemPages seed ON seed.Slug = p.Slug
WHERE p.IsSystem = 0;

COMMIT TRANSACTION;
