/*
    Replaces the fixed URL/image trust-seal controls with one ordered HTML/script field.
    Official Enamad, Zarinpal, Emalls, Torob and future provider snippets render directly
    in the storefront footer. Custom head/footer snippets are also promoted to trustedhtml.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'TrustSeal.Enamad.Enabled', N'TrustSeal.Enamad.Title', N'TrustSeal.Enamad.Url',
    N'TrustSeal.Enamad.ImagePath', N'TrustSeal.Enamad.Alt', N'TrustSeal.Enamad.SortOrder', N'TrustSeal.Enamad.NewTab',
    N'TrustSeal.Ecunion.Enabled', N'TrustSeal.Ecunion.Title', N'TrustSeal.Ecunion.Url',
    N'TrustSeal.Ecunion.ImagePath', N'TrustSeal.Ecunion.Alt', N'TrustSeal.Ecunion.SortOrder', N'TrustSeal.Ecunion.NewTab',
    N'TrustSeal.Samandehi.Enabled', N'TrustSeal.Samandehi.Title', N'TrustSeal.Samandehi.Url',
    N'TrustSeal.Samandehi.ImagePath', N'TrustSeal.Samandehi.Alt', N'TrustSeal.Samandehi.SortOrder', N'TrustSeal.Samandehi.NewTab'
);

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'TrustSeal.FooterHtml')
    INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
    VALUES (NEWID(), N'TrustSeal.FooterHtml', N'', N'TrustSeals', N'trustedhtml',
        N'کدهای رسمی نمادهای اعتماد در فوتر (اینماد، زرین‌پال، ایمالز، ترب و ...)', SYSUTCDATETIME());

UPDATE dbo.Settings
SET ValueType = N'trustedhtml',
    [Description] = CASE [Key]
        WHEN N'CustomHeadHtml' THEN N'کد سفارشی داخل <head> (فقط کد مورداعتماد مانند تحلیل و تگ‌ها)'
        ELSE N'کد سفارشی انتهای سایت (فقط کد مورداعتماد)'
    END,
    UpdatedAt = SYSUTCDATETIME()
WHERE [Key] IN (N'CustomHeadHtml', N'CustomFooterHtml');

COMMIT TRANSACTION;
