/* Run after 2026-07-14_product_experience_schema.sql. Inserts missing keys only. */
SET NOCOUNT ON;
DECLARE @Seed TABLE ([Key] nvarchar(200), [Value] nvarchar(max), GroupName nvarchar(100), ValueType nvarchar(50), [Description] nvarchar(500));
INSERT @Seed VALUES
(N'Typography.FontFamily',N'Vazirmatn',N'Typography',N'string',N'نام فونت فعال؛ پیش‌فرض Vazirmatn'),
(N'Typography.FontPath',N'',N'Typography',N'string',N'مسیر فایل فونت فعال'),
(N'Typography.FontFormat',N'woff2',N'Typography',N'string',N'فرمت فایل فونت فعال'),
(N'Typography.Scope',N'3',N'Typography',N'int',N'محدوده اعمال: ۱ فروشگاه، ۲ مدیریت، ۳ کل برنامه'),
(N'Typography.Version',N'1',N'Typography',N'string',N'نسخه کش فونت'),
(N'Typography.MaxUploadMb',N'5',N'Typography',N'int',N'حداکثر حجم فونت'),
(N'Branding.AssetVersion',N'1',N'Branding',N'string',N'نسخه کش دارایی‌های برند'),
(N'TrustSeal.Enamad.Enabled',N'false',N'TrustSeals',N'bool',N'نمایش Enamad'),
(N'TrustSeal.Enamad.Title',N'نماد اعتماد الکترونیکی',N'TrustSeals',N'string',N'عنوان نماد'),
(N'TrustSeal.Enamad.Url',N'',N'TrustSeals',N'string',N'نشانی HTTPS رسمی enamad.ir'),
(N'TrustSeal.Enamad.ImagePath',N'',N'TrustSeals',N'image',N'تصویر نماد'),
(N'TrustSeal.Enamad.Alt',N'نماد اعتماد الکترونیکی',N'TrustSeals',N'string',N'متن جایگزین'),
(N'TrustSeal.Enamad.SortOrder',N'10',N'TrustSeals',N'int',N'ترتیب نمایش'),
(N'TrustSeal.Enamad.NewTab',N'true',N'TrustSeals',N'bool',N'باز شدن در زبانه جدید'),
(N'TrustSeal.Ecunion.Enabled',N'false',N'TrustSeals',N'bool',N'نمایش ecunion'),
(N'TrustSeal.Ecunion.Title',N'اتحادیه کسب‌وکارهای مجازی',N'TrustSeals',N'string',N'عنوان مجوز'),
(N'TrustSeal.Ecunion.Url',N'',N'TrustSeals',N'string',N'نشانی HTTPS رسمی ecunion.ir'),
(N'TrustSeal.Ecunion.ImagePath',N'',N'TrustSeals',N'image',N'تصویر مجوز'),
(N'TrustSeal.Ecunion.Alt',N'مجوز اتحادیه کسب‌وکارهای مجازی',N'TrustSeals',N'string',N'متن جایگزین'),
(N'TrustSeal.Ecunion.SortOrder',N'20',N'TrustSeals',N'int',N'ترتیب نمایش'),
(N'TrustSeal.Ecunion.NewTab',N'true',N'TrustSeals',N'bool',N'باز شدن در زبانه جدید'),
(N'TrustSeal.Samandehi.Enabled',N'false',N'TrustSeals',N'bool',N'نمایش ساماندهی'),
(N'TrustSeal.Samandehi.Title',N'نشان ملی ثبت رسانه‌های دیجیتال',N'TrustSeals',N'string',N'عنوان نشان'),
(N'TrustSeal.Samandehi.Url',N'',N'TrustSeals',N'string',N'نشانی HTTPS رسمی samandehi.ir'),
(N'TrustSeal.Samandehi.ImagePath',N'',N'TrustSeals',N'image',N'تصویر نشان'),
(N'TrustSeal.Samandehi.Alt',N'نشان ساماندهی',N'TrustSeals',N'string',N'متن جایگزین'),
(N'TrustSeal.Samandehi.SortOrder',N'30',N'TrustSeals',N'int',N'ترتیب نمایش'),
(N'TrustSeal.Samandehi.NewTab',N'true',N'TrustSeals',N'bool',N'باز شدن در زبانه جدید');

INSERT dbo.Settings (Id,[Key],[Value],GroupName,ValueType,[Description],UpdatedAt)
SELECT NEWID(),s.[Key],s.[Value],s.GroupName,s.ValueType,s.[Description],SYSUTCDATETIME()
FROM @Seed s WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings x WHERE x.[Key]=s.[Key]);
