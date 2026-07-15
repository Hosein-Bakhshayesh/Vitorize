/*
    Vitorize database deployment preflight (READ ONLY).
    This script reports prerequisites and known conflicts. It deliberately does
    not create temporary tables, change options, or modify application data.
*/
SET NOCOUNT ON;

DECLARE @Findings TABLE
(
    Severity varchar(10) NOT NULL,
    CheckName nvarchar(160) NOT NULL,
    Detail nvarchar(2000) NOT NULL
);
DECLARE @LedgerCompatible bit = 0;

SELECT
    DB_NAME() AS DatabaseName,
    CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)) AS ServerName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(256)) AS Edition,
    d.compatibility_level AS CompatibilityLevel,
    d.collation_name AS Collation,
    SUSER_SNAME() AS ConnectedLogin,
    SYSUTCDATETIME() AS CheckedAtUtc
FROM sys.databases d
WHERE d.database_id = DB_ID();

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    INSERT @Findings VALUES ('ERROR', N'Target database', N'System databases are never valid Vitorize deployment targets.');

IF CAST(SERVERPROPERTY('ProductMajorVersion') AS int) < 15
    INSERT @Findings VALUES ('ERROR', N'SQL Server version', N'Vitorize requires SQL Server 2019 (15.x) or newer.');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Users'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.Users is missing; publish the reviewed baseline before running upgrades.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Roles'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.Roles is missing; publish the reviewed baseline before running upgrades.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Settings'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.Settings is missing; publish the reviewed baseline before running upgrades.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.GiftCodeReservations'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.GiftCodeReservations is missing; V0002 requires the core schema.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Products'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.Products is missing; the product-experience schema cannot be applied.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.CartItems'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.CartItems is missing; the product-experience schema cannot be applied.');
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.OrderItems'))
    INSERT @Findings VALUES ('ERROR', N'Core baseline', N'dbo.OrderItems is missing; the product-experience schema cannot be applied.');

DECLARE @ExpectedColumns TABLE (TableName sysname, ColumnName sysname, IsCore bit);
INSERT @ExpectedColumns VALUES
    (N'Users', N'Id', 1), (N'Users', N'Mobile', 1),
    (N'Roles', N'Id', 1), (N'Roles', N'Name', 1),
    (N'Settings', N'Key', 1), (N'Settings', N'Value', 1),
    (N'GiftCodeReservations', N'Status', 1),
    (N'OtpCodes', N'Purpose', 1),
    (N'SmsMessages', N'IdempotencyKey', 0),
    (N'SmsMessages', N'InternalNote', 0),
    (N'ProductFeatures', N'ProductId', 0),
    (N'ProductInputFields', N'Key', 0),
    (N'CartItems', N'InputFingerprint', 0),
    (N'FontAssets', N'FamilyName', 0);

INSERT @Findings
SELECT CASE WHEN IsCore = 1 THEN 'ERROR' ELSE 'INFO' END,
       N'Required column',
       N'dbo.' + TableName + N'.' + ColumnName + N' is missing.'
FROM @ExpectedColumns
WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NULL;

DECLARE @ExpectedIndexes TABLE (TableName sysname, IndexName sysname);
INSERT @ExpectedIndexes VALUES
    (N'Settings', N'UX_Settings_Key'),
    (N'Roles', N'UX_Roles_Name'),
    (N'SmsMessages', N'UX_SmsMessages_IdempotencyKey'),
    (N'ProductFeatures', N'IX_ProductFeatures_Product_Order'),
    (N'ProductInputFields', N'UX_ProductInputFields_Product_Key'),
    (N'CartItemInputValues', N'UX_CartItemInputValues_Item_Key'),
    (N'OrderItemInputValues', N'UX_OrderItemInputValues_Item_Key');

INSERT @Findings
SELECT 'INFO', N'Required index', N'dbo.' + expected.TableName + N'.' + expected.IndexName + N' is missing.'
FROM @ExpectedIndexes expected
WHERE NOT EXISTS
(
    SELECT 1 FROM sys.indexes actual
    WHERE actual.object_id = OBJECT_ID(N'dbo.' + expected.TableName)
      AND actual.name = expected.IndexName
);

IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
    INSERT @Findings VALUES ('INFO', N'Deployment ledger', N'dbo.DatabaseScriptHistory is not installed; V0001 will create it.');
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptName') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptVersion') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'ScriptHash') IS NULL OR
       COL_LENGTH(N'dbo.DatabaseScriptHistory', N'Success') IS NULL
        INSERT @Findings VALUES ('ERROR', N'Deployment ledger', N'The existing ledger is incompatible with the canonical contract.');
    ELSE
    BEGIN
        SET @LedgerCompatible = 1;
        IF EXISTS (SELECT ScriptName FROM dbo.DatabaseScriptHistory GROUP BY ScriptName HAVING COUNT(*) > 1)
            INSERT @Findings VALUES ('ERROR', N'Deployment ledger', N'Duplicate ScriptName rows exist.');
        IF EXISTS (SELECT ScriptVersion FROM dbo.DatabaseScriptHistory GROUP BY ScriptVersion HAVING COUNT(*) > 1)
            INSERT @Findings VALUES ('ERROR', N'Deployment ledger', N'Duplicate ScriptVersion rows exist.');
        IF EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptHash LIKE '%[^0-9a-f]%' OR LEN(ScriptHash) <> 64 OR Success <> 1)
            INSERT @Findings VALUES ('ERROR', N'Deployment ledger', N'One or more ledger rows violate the hash/success contract.');
    END;
END;

IF OBJECT_ID(N'dbo.GiftCodeReservations', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.GiftCodeReservations WHERE [Status] NOT BETWEEN 0 AND 3)
        INSERT @Findings VALUES ('ERROR', N'Gift-code reservation status', N'Rows outside the supported Status range 0..3 must be corrected manually before V0002.');

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.GiftCodeReservations')
          AND is_disabled = 1
    )
        INSERT @Findings VALUES ('ERROR', N'Gift-code constraints', N'A disabled CHECK constraint exists on dbo.GiftCodeReservations.');
END;

IF OBJECT_ID(N'dbo.OtpCodes', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.OtpCodes')
      AND cc.definition LIKE N'%Purpose%'
      AND cc.definition NOT LIKE N'%4%'
)
    INSERT @Findings VALUES ('WARN', N'OTP Purpose constraint', N'A legacy Purpose constraint may reject Login purpose 4; review the environment-specific OTP corrective script.');

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
    INSERT @Findings VALUES ('ERROR', N'Foreign keys', N'One or more foreign keys are disabled or untrusted.');
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1)
    INSERT @Findings VALUES ('ERROR', N'CHECK constraints', N'One or more CHECK constraints are disabled or untrusted.');

IF OBJECT_ID(N'dbo.Settings', N'U') IS NOT NULL
AND EXISTS (SELECT [Key] FROM dbo.Settings GROUP BY [Key] HAVING COUNT(*) > 1)
    INSERT @Findings VALUES ('ERROR', N'Settings uniqueness', N'Duplicate Settings.Key values exist.');

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM dbo.Users WHERE Mobile IN (N'09123456789', N'09378149896'))
    INSERT @Findings VALUES ('WARN', N'Known legacy users', N'A historical default/demo mobile exists. Verify ownership, disable it if unsafe, and revoke its refresh tokens before Production deployment.');

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.UserRoles ur
    LEFT JOIN dbo.Users u ON u.Id = ur.UserId
    LEFT JOIN dbo.Roles r ON r.Id = ur.RoleId
    WHERE u.Id IS NULL OR r.Id IS NULL
)
    INSERT @Findings VALUES ('ERROR', N'Orphaned role assignments', N'dbo.UserRoles contains an orphaned UserId or RoleId.');

IF OBJECT_ID(N'dbo.SmsMessageAttempts', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL
AND EXISTS
(
    SELECT 1 FROM dbo.SmsMessageAttempts child
    LEFT JOIN dbo.SmsMessages parent ON parent.Id = child.SmsMessageId
    WHERE parent.Id IS NULL
)
    INSERT @Findings VALUES ('ERROR', N'Orphaned SMS attempts', N'dbo.SmsMessageAttempts contains an orphaned SmsMessageId.');

IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL AND EXISTS
(
    SELECT Gateway, Authority FROM dbo.Payments WHERE Authority IS NOT NULL
    GROUP BY Gateway, Authority HAVING COUNT(*) > 1
)
    INSERT @Findings VALUES ('ERROR', N'Payment authority uniqueness', N'Duplicate Gateway/Authority values must be reconciled before V0004.');

IF OBJECT_ID(N'dbo.WalletTopUps', N'U') IS NOT NULL AND EXISTS
(
    SELECT Gateway, Authority FROM dbo.WalletTopUps WHERE Authority IS NOT NULL
    GROUP BY Gateway, Authority HAVING COUNT(*) > 1
)
    INSERT @Findings VALUES ('ERROR', N'Wallet top-up authority uniqueness', N'Duplicate Gateway/Authority values must be reconciled before V0004.');

IF OBJECT_ID(N'dbo.WalletTransactions', N'U') IS NOT NULL AND EXISTS
(
    SELECT UserId, ReferenceType, ReferenceId, [Type] FROM dbo.WalletTransactions
    WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL
    GROUP BY UserId, ReferenceType, ReferenceId, [Type] HAVING COUNT(*) > 1
)
    INSERT @Findings VALUES ('ERROR', N'Wallet financial idempotency', N'Duplicate financial references must be reconciled before V0004.');

SELECT Severity, CheckName, Detail
FROM @Findings
ORDER BY CASE Severity WHEN 'ERROR' THEN 1 WHEN 'WARN' THEN 2 ELSE 3 END, CheckName;

SELECT
    COALESCE(SUM(CASE WHEN Severity = 'ERROR' THEN 1 ELSE 0 END), 0) AS ErrorCount,
    COALESCE(SUM(CASE WHEN Severity = 'WARN' THEN 1 ELSE 0 END), 0) AS WarningCount,
    COALESCE(SUM(CASE WHEN Severity = 'INFO' THEN 1 ELSE 0 END), 0) AS InformationCount
FROM @Findings;

DECLARE @RequiredVersions TABLE (Version nvarchar(50) PRIMARY KEY);
INSERT @RequiredVersions VALUES
    (N'V0001'), (N'V0002'), (N'H20260713-SMS-SCHEMA'),
    (N'H20260714-PRODUCT-SCHEMA'), (N'V0003'), (N'V0004'), (N'H20260708-UI'),
    (N'H20260713-SMS-SEED'), (N'H20260714-PRODUCT-SEED');

IF @LedgerCompatible = 1
BEGIN
    SELECT expected.Version,
           CASE WHEN actual.ScriptVersion IS NULL THEN N'PENDING' ELSE N'APPLIED' END AS DeploymentState,
           actual.ScriptName, actual.ScriptHash, actual.AppliedAt, actual.Environment
    FROM @RequiredVersions expected
    LEFT JOIN dbo.DatabaseScriptHistory actual ON actual.ScriptVersion = expected.Version
    ORDER BY expected.Version;

    SELECT ScriptVersion, ScriptName, ScriptHash, AppliedAt, AppliedBy, Environment, Success, Notes
    FROM dbo.DatabaseScriptHistory
    ORDER BY Id;
END
ELSE
    SELECT Version, N'PENDING' AS DeploymentState FROM @RequiredVersions ORDER BY Version;

IF EXISTS (SELECT 1 FROM @Findings WHERE Severity = 'ERROR')
    THROW 51090, 'Vitorize database preflight failed. Resolve the reported errors before deployment.', 1;
