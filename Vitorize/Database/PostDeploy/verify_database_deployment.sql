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
    (N'DatabaseScriptHistory');

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
    (N'CartItems', N'InputFingerprint'), (N'FontAssets', N'FamilyName');

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
        (N'Branding.AssetVersion'), (N'TrustSeal.Enamad.Enabled');

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
    INSERT @Issues VALUES ('ERROR', N'Known legacy users', N'A historical default/demo mobile still exists. Verify ownership, disable the account if unsafe, and revoke its refresh tokens.');

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

SELECT Severity, CheckName, Detail FROM @Issues ORDER BY CheckName, Detail;
SELECT COUNT(*) AS ErrorCount FROM @Issues WHERE Severity = 'ERROR';

IF EXISTS (SELECT 1 FROM @Issues WHERE Severity = 'ERROR')
    THROW 51100, 'Vitorize post-deployment verification failed. Review the preceding findings.', 1;

SELECT N'Vitorize database deployment verification passed.' AS Result, SYSUTCDATETIME() AS VerifiedAtUtc;
