/*
    Vitorize post-deployment verification (READ ONLY).
    Returns all findings and fails the sqlcmd process when an ERROR is present.
*/
SET NOCOUNT ON;

DECLARE @Issues TABLE
(
    Severity varchar(10) NOT NULL,
    CheckName nvarchar(160) NOT NULL,
    Detail nvarchar(2000) NOT NULL
);

DECLARE @RequiredTables TABLE (Name sysname PRIMARY KEY);
INSERT @RequiredTables (Name) VALUES
    (N'Users'), (N'Roles'), (N'UserRoles'), (N'Settings'),
    (N'GiftCodeReservations'), (N'OtpCodes'),
    (N'SmsMessages'), (N'SmsMessageAttempts'),
    (N'ProductFeatures'), (N'ProductInputFields'),
    (N'CartItemInputValues'), (N'OrderItemInputValues'), (N'FontAssets'),
    (N'DatabaseScriptHistory'), (N'PaymentRefunds'), (N'FinancialAuditLogs'),
    (N'OrderItemKycStates'), (N'KycPolicies'), (N'KycPolicyVersions'),
    (N'KycDocumentTypes'), (N'KycPolicyDocumentRequirements'),
    (N'OrderNumberCounters');

INSERT @Issues
SELECT 'ERROR', N'Required table', N'dbo.' + expected.Name + N' is missing.'
FROM @RequiredTables expected
WHERE OBJECT_ID(N'dbo.' + expected.Name, N'U') IS NULL;

DECLARE @RequiredColumns TABLE (TableName sysname, ColumnName sysname);
INSERT @RequiredColumns VALUES
    (N'Users', N'Mobile'), (N'Roles', N'Name'), (N'Settings', N'Key'),
    (N'GiftCodeReservations', N'Status'), (N'OtpCodes', N'Purpose'),
    (N'SmsMessages', N'IdempotencyKey'), (N'SmsMessages', N'InternalNote'),
    (N'ProductFeatures', N'ProductId'), (N'ProductInputFields', N'Key'),
    (N'CartItems', N'InputFingerprint'), (N'FontAssets', N'FamilyName'),
    (N'PaymentCallbacks', N'CallbackKey'), (N'UserVerificationProfiles', N'EncryptedPayload'), (N'VerificationDocuments', N'KycDocumentTypeId'),
    (N'OrderItemDeliveries', N'EncryptionVersion'), (N'OutboxMessages', N'LockedAt'),
    (N'OrderItemKycStates', N'RowVersion'), (N'OrderItemKycStates', N'Status'), (N'OrderItemKycStates', N'CustomerActionDeadlineAt'),
    (N'OrderItems', N'KycCustomerActionDeadlineHours'), (N'KycPolicyVersions', N'CustomerActionDeadlineHours'),
    (N'KycPolicyDocumentRequirements', N'RedactionMode'), (N'KycPolicyDocumentRequirements', N'RedactionInstructions'),
    (N'Payments', N'MaskedCardPan');

INSERT @Issues
SELECT 'ERROR', N'Required column', N'dbo.' + TableName + N'.' + ColumnName + N' is missing.'
FROM @RequiredColumns
WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NULL;

IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NOT NULL
BEGIN
    DECLARE @RequiredVersions TABLE
    (
        Version nvarchar(50) PRIMARY KEY,
        ScriptName nvarchar(260) NOT NULL,
        ScriptHash char(64) COLLATE Latin1_General_100_BIN2 NOT NULL
    );
    INSERT @RequiredVersions VALUES
        (N'V0001', N'V0001__create_database_script_history.sql', '0d95329a1e6b5eafbb377b6898f6f43ade76054ad22c970a00c92ffcdc8c6053'),
        (N'V0002', N'V0002__normalize_gift_code_reservation_status_constraint.sql', '918491680f470df380fff99caaa3b291b8e3354309e28b144945950ae7bc4b45'),
        (N'H20260713-SMS-SCHEMA', N'2026-07-13_create_sms_history.sql', 'ece5f2dbebf7266c2c58e079377148a43bc02699d31ff9c3e853ca30b731a8f0'),
        (N'H20260714-PRODUCT-SCHEMA', N'2026-07-14_product_experience_schema.sql', '907cabcb1eefb753ae3b2ff19add608d2f011c448295f2e39a2a22e3799c393c'),
        (N'V0003', N'V0003__seed_reference_roles.sql', '9cd5ff472bb5d776269b43f14565870c6c1de862b0a275a36e342138e635be35'),
        (N'V0004', N'V0004__financial_integrity_and_security_hardening.sql', '8a896e8cdbfbee4d84a0c6415192c03cd4fda4088b51828acb73f9ea5c862ef4'),
        (N'V0005', N'V0005__seo_content_and_legacy_redirects.sql', 'ed6b02b7453590d09fc2d1a085ea3e8f006ab66659c046c911196d7af8955b22'),
        (N'V0006', N'V0006__preserve_currency_through_checkout.sql', '70c4485300b40cc94547177682fba3e82e90a7deb1937d2a66c27ea4be1287cc'),
        (N'V0007', N'V0007__support_fulfillment_ticket_uniqueness.sql', 'b39587eed17e512d60e6db99986d488f1d770c54b02f8cee4fac3e54331d2a10'),
        (N'V0008', N'V0008__seed_storefront_typography_settings.sql', 'fff9d2f0f22c6ac51629f3edee38c30a3e90dc7433d3914e10fbf2035eaade15'),
        (N'V0009', N'V0009__persistent_guest_carts.sql', '3763a5b6236065f47b6b461f42188672bbb5584408a495eb7f1bd30c327ab438'),
        (N'V0010', N'V0010__kyc_policy_and_order_item_snapshot.sql', '029bf21945b4d1f19c14b3620955a64cbcf37512767e92b482ac8b7d5c5557f0'),
        (N'V0011', N'V0011__order_item_kyc_lifecycle_state.sql', '0f838bdbe7d783d7e28f93a06d7c2f7aefd39e244ba00e11881cd903d6315181'),
        (N'V0012', N'V0012__verification_document_kyc_document_type.sql', '73d8c8b043c8f610fd8896762fedaf8e7cd56b2333994722478bd9d4d5add73a'),
        (N'V0013', N'V0013__kyc_document_redaction_configuration.sql', '714524653dbbb8a03e304ad10de1bd7664d643e4f0f9b5904c122c0653e87e98'),
        (N'V0014', N'V0014__kyc_customer_action_deadline.sql', '10f71bfaea811724ed1a7008ceaf80ba70a0c3b077377dee354d2dc66e23468f'),
        (N'V0015', N'V0015__kyc_finance_resolution.sql', '29a1af41c42b655785f6c90b419921992bb3029d41f943ebe3a721dc2ed9241a'),
        (N'V0016', N'V0016__order_vat_snapshot.sql', 'e44e9af54422816c603c87ef1812e54a2bd817f2ea815cbf20ae30959f21b16e'),
        (N'V0017', N'V0017__cms_system_pages_seed.sql', '7929e2007087f635eed95ecaa1b75b205e5cc34a45621e4a5f999d8b2eb538a1'),
        (N'V0018', N'V0018__notification_broadcasts.sql', '4e331e3b0475f07b4af8f623fcdf49b98cf21a04b62890148ca04916605b1473'),
        (N'V0019', N'V0019__loading_media_setting_seed.sql', '2397e2acba47a1af09d214d815118b04db61c557b45bac0f2557ae46add0711e'),
        (N'V0020', N'V0020__product_variant_managed_stock.sql', '1c87108b7e3ae8e762aeb1c3763c558a9adf269e8fb5d22e18805914a3cacdfe'),
        (N'V0021', N'V0021__default_variants_for_managed_products.sql', 'e0640d9ca8c6292bd69d7b53672f6b9cb20f1c832dee1c755d1239ffdb2cd587'),
        (N'V0022', N'V0022__force_out_of_stock_and_product_faqs.sql', 'd1d4e22e1afcde5188c51dc87a43467dd4cd2f1d77175f2ec1553e50ed973fff'),
        (N'V0023', N'V0023__product_categories_and_default_font.sql', 'c0e4ac5aca4fc1b76ce62b136038836a75a66ac3943eed95a7523ee3d751f4d0'),
        (N'V0024', N'V0024__customer_order_visibility.sql', 'b762994c35f64fdd0e779e91536fd28ca4091d8a5b821f8c4d18a13b72a8921b'),
        (N'V0025', N'V0025__peyda_default_and_sortmode_setting_type.sql', 'ad21b18d3918da4ab56221e1b00b1fd4c455de9f58cc50d1adc28513a713d5c6'),
        (N'V0026', N'V0026__remove_product_level_kyc.sql', 'ae1fe1f5313da1f65193864f3763eb642967d869c752d4e2ceb770fc80f73e53'),
        (N'V0027', N'V0027__order_total_kyc_settings_and_policy.sql', '9720ffb6a52cc72101db3ebcfe272982f95f8347350879dd9b9b1037f11f867e'),
        (N'V0028', N'V0028__payment_masked_card_pan.sql', 'f2cde7ef5bfab578f390bf76e8b436bee1057e0709039f8c028da04327b51881'),
        (N'V0029', N'V0029__trusted_footer_seals_and_custom_markup.sql', '7eccbfbf0ccb451a67b151b9bc2d79fca68d84ff00d67c92d825044e3a8324f0'),
        (N'V0030', N'V0030__remove_deprecated_custom_sms_settings.sql', '828eb33a554293092ed47ffddb946cf5c4cf0b511fe8100183dc73824477a53c'),
        (N'V0031', N'V0031__paid_order_number_sequence.sql', 'ca6958841bd412bd5aad10f1336aaed60dd81286727999036bc0a5f8db6d99b4'),
        (N'V0032', N'V0032__always_on_sms_remove_activation_switches.sql', '9981f260851fb928f8fea707e482ec147c39e2d2d9fe66eeddbe864c4264ec05'),
        (N'H20260708-UI', N'2026-07-08_seed_settings_ui_customization.sql', 'a9da7ed7e2b87e27298b8005befb10954c228a574786c3cf14f9db8c535b2ed3'),
        (N'H20260713-SMS-SEED', N'2026-07-13_seed_sms_settings.sql', 'a950e3b326fe99e197c6e08c0024e0a601e7bfdbcfceb130a40736f8281f2b6e'),
        (N'H20260714-PRODUCT-SEED', N'2026-07-14_seed_product_experience_settings.sql', '90ae9b6278a85536accf28e7a927755b980cc062b07afb65d1a6d43fcaad4c00');

    INSERT @Issues
    SELECT 'ERROR', N'Deployment ledger', N'Required version ' + expected.Version + N' is not recorded.'
    FROM @RequiredVersions expected
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.DatabaseScriptHistory actual
        WHERE actual.ScriptVersion = expected.Version
          AND actual.ScriptName = expected.ScriptName
          AND actual.ScriptHash = expected.ScriptHash
          AND actual.Success = 1
    );

    IF EXISTS (SELECT ScriptName FROM dbo.DatabaseScriptHistory GROUP BY ScriptName HAVING COUNT(*) > 1)
        INSERT @Issues VALUES ('ERROR', N'Deployment ledger', N'Duplicate ScriptName rows exist.');
    IF EXISTS (SELECT ScriptVersion FROM dbo.DatabaseScriptHistory GROUP BY ScriptVersion HAVING COUNT(*) > 1)
        INSERT @Issues VALUES ('ERROR', N'Deployment ledger', N'Duplicate ScriptVersion rows exist.');
    IF EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptHash LIKE '%[^0-9a-f]%' OR LEN(ScriptHash) <> 64 OR Success <> 1)
        INSERT @Issues VALUES ('ERROR', N'Deployment ledger', N'An invalid hash or unsuccessful row exists.');
END;

IF OBJECT_ID(N'dbo.GiftCodeReservations', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.GiftCodeReservations WHERE [Status] NOT BETWEEN 0 AND 3)
        INSERT @Issues VALUES ('ERROR', N'Gift-code reservation status', N'Unsupported Status values exist.');
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.GiftCodeReservations')
          AND name = N'CK_GiftCodeReservations_Status'
          AND is_disabled = 0 AND is_not_trusted = 0
    )
        INSERT @Issues VALUES ('ERROR', N'Gift-code reservation constraint', N'The canonical trusted CHECK constraint is missing.');
END;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
    INSERT @Issues VALUES ('ERROR', N'Foreign keys', N'One or more foreign keys are disabled or untrusted.');
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1)
    INSERT @Issues VALUES ('ERROR', N'CHECK constraints', N'One or more CHECK constraints are disabled or untrusted.');

IF OBJECT_ID(N'dbo.Settings', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT [Key] FROM dbo.Settings GROUP BY [Key] HAVING COUNT(*) > 1)
        INSERT @Issues VALUES ('ERROR', N'Settings uniqueness', N'Duplicate Settings.Key values exist.');

    DECLARE @RequiredSettings TABLE ([Key] nvarchar(200) PRIMARY KEY);
    INSERT @RequiredSettings VALUES
        (N'HeaderLogoPath'), (N'FaviconPath'), (N'Sms.OtpTemplateId'),
        (N'Sms.NotificationTemplateId'), (N'Typography.FontFamily'),
        (N'StorefrontPersianFont'), (N'StorefrontEnglishFont'),
        (N'Branding.AssetVersion'), (N'Verification.OrderAmountThresholdToman'),
        (N'Verification.CustomerNotice'), (N'TrustSeal.FooterHtml');

    INSERT @Issues
    SELECT 'ERROR', N'Required setting', N'Setting ' + expected.[Key] + N' is missing.'
    FROM @RequiredSettings expected
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings actual WHERE actual.[Key] = expected.[Key]);

END;

IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
BEGIN
    DECLARE @RequiredRoles TABLE (Name nvarchar(100) PRIMARY KEY);
    INSERT @RequiredRoles VALUES (N'SuperAdmin'), (N'Admin'), (N'Support'), (N'Customer');
    INSERT @Issues
    SELECT 'ERROR', N'Required role', N'Role ' + expected.Name + N' is missing.'
    FROM @RequiredRoles expected
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles actual WHERE actual.Name = expected.Name);
END;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM dbo.Users WHERE Mobile IN (N'09123456789', N'09378149896'))
    INSERT @Issues VALUES ('WARN', N'Known legacy users', N'A historical default/demo mobile still exists. Verify ownership, disable the account if unsafe, and revoke its refresh tokens.');

IF OBJECT_ID(N'dbo.OtpCodes', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.OtpCodes')
      AND cc.definition LIKE N'%Purpose%'
      AND cc.definition NOT LIKE N'%4%'
)
    INSERT @Issues VALUES ('ERROR', N'OTP Purpose constraint', N'A CHECK constraint appears to reject Login purpose 4.');

IF OBJECT_ID(N'dbo.ProductFeatures', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductFeatures') AND name = N'IX_ProductFeatures_Product_Order')
    INSERT @Issues VALUES ('ERROR', N'Product feature index', N'IX_ProductFeatures_Product_Order is missing.');

IF OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'UX_SmsMessages_IdempotencyKey')
    INSERT @Issues VALUES ('ERROR', N'SMS idempotency index', N'UX_SmsMessages_IdempotencyKey is missing.');

IF OBJECT_ID(N'dbo.FontAssets', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.FontAssets WHERE FamilyName = N'Vazirmatn' AND IsBuiltIn = 1)
    INSERT @Issues VALUES ('ERROR', N'Default font', N'The built-in Vazirmatn font asset is missing.');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_Gateway_Authority' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Payment authority', N'Unique gateway/authority index is missing.');
IF COL_LENGTH(N'dbo.Products', N'RequiresVerification') IS NOT NULL
    INSERT @Issues VALUES ('ERROR', N'Product-level KYC', N'Products.RequiresVerification must be removed; final order amount controls KYC.');
IF EXISTS
(
    SELECT 1 FROM dbo.Settings
    WHERE [Key] IN
    (
        N'SmsEnabled', N'Sms.IsEnabled', N'Sms.CustomSendEnabled', N'Sms.CustomTextEnabled',
        N'Sms.UseOutbox', N'Sms.RequireConfirmation', N'Sms.AllowImmediateSend', N'Sms.AllowRetryFailed'
    )
)
    INSERT @Issues VALUES ('ERROR', N'Deprecated SMS activation settings', N'SMS is always active; obsolete activation switches must be removed.');
IF COL_LENGTH(N'dbo.CartItems', N'CurrencyType') IS NULL OR
   COL_LENGTH(N'dbo.Orders', N'CurrencyType') IS NULL OR
   COL_LENGTH(N'dbo.OrderItems', N'CurrencyType') IS NULL OR
   COL_LENGTH(N'dbo.Payments', N'CurrencyType') IS NULL
    INSERT @Issues VALUES ('ERROR', N'Currency snapshots', N'CurrencyType is missing from a checkout aggregate.');
IF COL_LENGTH(N'dbo.Carts', N'GuestTokenHash') IS NULL OR COL_LENGTH(N'dbo.Carts', N'LastActivityAt') IS NULL
    INSERT @Issues VALUES ('ERROR', N'Guest cart columns', N'GuestTokenHash or LastActivityAt is missing from dbo.Carts.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Carts') AND name = N'UX_Carts_GuestTokenHash' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Guest cart token uniqueness', N'UX_Carts_GuestTokenHash is missing or not unique.');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Carts') AND name = N'CK_Carts_ExactlyOneOwner' AND is_disabled = 0 AND is_not_trusted = 0)
    INSERT @Issues VALUES ('ERROR', N'Guest cart ownership constraint', N'CK_Carts_ExactlyOneOwner is missing or untrusted.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WalletTransactions') AND name = N'UX_WalletTransactions_FinancialReference' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Wallet idempotency', N'Unique financial reference index is missing.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PaymentCallbacks') AND name = N'UX_PaymentCallbacks_PaymentId_CallbackKey' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Callback idempotency', N'Unique callback key index is missing.');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItemKycStates') AND name = N'UX_OrderItemKycStates_OrderItemId' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Order-item KYC lifecycle uniqueness', N'Unique OrderItemId constraint is missing.');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.OrderItemKycStates') AND name = N'CK_OrderItemKycStates_Status' AND is_disabled = 0 AND is_not_trusted = 0)
    INSERT @Issues VALUES ('ERROR', N'Order-item KYC lifecycle status constraint', N'CK_OrderItemKycStates_Status is missing or untrusted.');

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.UserRoles child
    LEFT JOIN dbo.Users userRow ON userRow.Id = child.UserId
    LEFT JOIN dbo.Roles roleRow ON roleRow.Id = child.RoleId
    WHERE userRow.Id IS NULL OR roleRow.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned role assignments', N'dbo.UserRoles contains an orphaned UserId or RoleId.');

IF OBJECT_ID(N'dbo.SmsMessageAttempts', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.SmsMessageAttempts child
    LEFT JOIN dbo.SmsMessages parent ON parent.Id = child.SmsMessageId
    WHERE parent.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned SMS attempts', N'dbo.SmsMessageAttempts contains an orphaned SmsMessageId.');

IF OBJECT_ID(N'dbo.ProductFeatures', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.ProductFeatures child
    LEFT JOIN dbo.Products parent ON parent.Id = child.ProductId
    WHERE parent.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned product features', N'dbo.ProductFeatures contains an orphaned ProductId.');

IF OBJECT_ID(N'dbo.ProductInputFields', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.ProductInputFields child
    LEFT JOIN dbo.Products parent ON parent.Id = child.ProductId
    WHERE parent.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned product input fields', N'dbo.ProductInputFields contains an orphaned ProductId.');

IF OBJECT_ID(N'dbo.CartItemInputValues', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CartItems', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.CartItemInputValues child
    LEFT JOIN dbo.CartItems parent ON parent.Id = child.CartItemId
    WHERE parent.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned cart input values', N'dbo.CartItemInputValues contains an orphaned CartItemId.');

IF OBJECT_ID(N'dbo.OrderItemInputValues', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.OrderItemInputValues child
    LEFT JOIN dbo.OrderItems parent ON parent.Id = child.OrderItemId
    WHERE parent.Id IS NULL
)
    INSERT @Issues VALUES ('ERROR', N'Orphaned order input values', N'dbo.OrderItemInputValues contains an orphaned OrderItemId.');

IF OBJECT_ID(N'dbo.LegacyRedirects', N'U') IS NULL
    INSERT @Issues VALUES ('ERROR', N'Legacy redirects', N'dbo.LegacyRedirects is missing.');
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LegacyRedirects') AND name = N'UX_LegacyRedirects_SourcePath' AND is_unique = 1)
    INSERT @Issues VALUES ('ERROR', N'Legacy redirect uniqueness', N'UX_LegacyRedirects_SourcePath is missing or not unique.');

IF COL_LENGTH(N'dbo.Products', N'FocusKeyword') IS NULL OR COL_LENGTH(N'dbo.Products', N'ThumbnailAltText') IS NULL
    INSERT @Issues VALUES ('ERROR', N'Product SEO columns', N'Products.FocusKeyword or Products.ThumbnailAltText is missing.');
IF COL_LENGTH(N'dbo.Products', N'RedirectUrl') IS NULL
    INSERT @Issues VALUES ('ERROR', N'Product redirect column', N'Products.RedirectUrl is missing.');
IF COL_LENGTH(N'dbo.ProductTags', N'Aliases') IS NULL OR COL_LENGTH(N'dbo.ProductTags', N'IsActive') IS NULL
    INSERT @Issues VALUES ('ERROR', N'ProductTag SEO columns', N'ProductTags alias/activation columns are missing.');
IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE [Key] = N'Seo.CanonicalBaseUrl')
    INSERT @Issues VALUES ('ERROR', N'Canonical base setting', N'Seo.CanonicalBaseUrl is missing.');

SELECT Severity, CheckName, Detail FROM @Issues ORDER BY CheckName, Detail;
SELECT COUNT(*) AS ErrorCount FROM @Issues WHERE Severity = 'ERROR';

IF EXISTS (SELECT 1 FROM @Issues WHERE Severity = 'ERROR')
    THROW 51100, 'Vitorize post-deployment verification failed. Review the preceding findings.', 1;

SELECT N'Vitorize database deployment verification passed.' AS Result, SYSUTCDATETIME() AS VerifiedAtUtc;
