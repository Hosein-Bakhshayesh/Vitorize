SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- Phase-3A-B uses one paid item and one pending verification profile per
-- destructive browser journey.  Never share these rows between projects:
-- a successful upload is intentionally consumed by the application.
DECLARE @PasswordHash nvarchar(400) = N'$2a$11$oRlRYEDBoNTt6xcAxAEcmeoOi/Ketcai3BWjZBLeCjnLhrwxIWc2y';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000053';
DECLARE @DocA uniqueidentifier = '31000000-0000-0000-0000-000000000044';
DECLARE @DocB uniqueidentifier = '31000000-0000-0000-0000-000000000045';

DECLARE @Scenarios TABLE (
    Code nvarchar(40) NOT NULL PRIMARY KEY, CustomerId uniqueidentifier NOT NULL, Mobile nvarchar(20) NOT NULL,
    ProfileId uniqueidentifier NOT NULL, PolicyId uniqueidentifier NOT NULL, VersionId uniqueidentifier NOT NULL,
    OrderId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL, RedactionMode tinyint NOT NULL, Instructions nvarchar(1000) NULL);
INSERT @Scenarios VALUES
(N'DL',         '33000000-0000-0000-0000-000000000071', N'09120000031', '34000000-0000-0000-0000-000000000071', '35000000-0000-0000-0000-000000000071', '36000000-0000-0000-0000-000000000071', '37000000-0000-0000-0000-000000000071', '38000000-0000-0000-0000-000000000071', 2, N'FIX09-3AB-DL required instruction'),
(N'DD',         '33000000-0000-0000-0000-000000000072', N'09120000032', '34000000-0000-0000-0000-000000000072', '35000000-0000-0000-0000-000000000072', '36000000-0000-0000-0000-000000000072', '37000000-0000-0000-0000-000000000072', '38000000-0000-0000-0000-000000000072', 2, N'FIX09-3AB-DD required instruction'),
(N'ML',         '33000000-0000-0000-0000-000000000073', N'09120000033', '34000000-0000-0000-0000-000000000073', '35000000-0000-0000-0000-000000000073', '36000000-0000-0000-0000-000000000073', '37000000-0000-0000-0000-000000000073', '38000000-0000-0000-0000-000000000073', 2, N'FIX09-3AB-ML required instruction'),
(N'MD',         '33000000-0000-0000-0000-000000000074', N'09120000034', '34000000-0000-0000-0000-000000000074', '35000000-0000-0000-0000-000000000074', '36000000-0000-0000-0000-000000000074', '37000000-0000-0000-0000-000000000074', '38000000-0000-0000-0000-000000000074', 2, N'FIX09-3AB-MD required instruction'),
(N'OPT-DIRECT', '33000000-0000-0000-0000-000000000075', N'09120000035', '34000000-0000-0000-0000-000000000075', '35000000-0000-0000-0000-000000000075', '36000000-0000-0000-0000-000000000075', '37000000-0000-0000-0000-000000000075', '38000000-0000-0000-0000-000000000075', 1, N'FIX09-3AB optional instruction'),
(N'OPT-REDACT', '33000000-0000-0000-0000-000000000076', N'09120000036', '34000000-0000-0000-0000-000000000076', '35000000-0000-0000-0000-000000000076', '36000000-0000-0000-0000-000000000076', '37000000-0000-0000-0000-000000000076', '38000000-0000-0000-0000-000000000076', 1, N'FIX09-3AB optional instruction'),
(N'NONE',       '33000000-0000-0000-0000-000000000077', N'09120000037', '34000000-0000-0000-0000-000000000077', '35000000-0000-0000-0000-000000000077', '36000000-0000-0000-0000-000000000077', '37000000-0000-0000-0000-000000000077', '38000000-0000-0000-0000-000000000077', 0, NULL),
(N'V1',         '33000000-0000-0000-0000-000000000078', N'09120000038', '34000000-0000-0000-0000-000000000078', '35000000-0000-0000-0000-000000000078', '36000000-0000-0000-0000-000000000078', '37000000-0000-0000-0000-000000000078', '38000000-0000-0000-0000-000000000078', 1, N'FIX09-3AB V1 instruction'),
(N'V2',         '33000000-0000-0000-0000-000000000079', N'09120000039', '34000000-0000-0000-0000-000000000079', '35000000-0000-0000-0000-000000000079', '36000000-0000-0000-0000-000000000079', '37000000-0000-0000-0000-000000000079', '38000000-0000-0000-0000-000000000079', 2, N'FIX09-3AB V2 instruction'),
(N'REPLACE',    '33000000-0000-0000-0000-000000000080', N'09120000040', '34000000-0000-0000-0000-000000000080', '35000000-0000-0000-0000-000000000080', '36000000-0000-0000-0000-000000000080', '37000000-0000-0000-0000-000000000080', '38000000-0000-0000-0000-000000000080', 2, N'FIX09-3AB replacement instruction'),
(N'MULTI',      '33000000-0000-0000-0000-000000000081', N'09120000041', '34000000-0000-0000-0000-000000000081', '35000000-0000-0000-0000-000000000081', '36000000-0000-0000-0000-000000000081', '37000000-0000-0000-0000-000000000081', '38000000-0000-0000-0000-000000000081', 2, N'FIX09-3AB multi required instruction'),
-- FIX-10 owns these three outright. The header rule above is not decorative: a browser journey
-- that submits a profile or spends an upload slot leaves the row unusable for the next spec, and
-- FIX-10 previously borrowed DL, ML and MULTI from the Phase-3A specs that already consume them.
(N'FIX10-DOB',    '33000000-0000-0000-0000-000000000082', N'09120000042', '34000000-0000-0000-0000-000000000082', '35000000-0000-0000-0000-000000000082', '36000000-0000-0000-0000-000000000082', '37000000-0000-0000-0000-000000000082', '38000000-0000-0000-0000-000000000082', 2, N'FIX10 desktop DOB instruction'),
(N'FIX10-MOBILE', '33000000-0000-0000-0000-000000000083', N'09120000043', '34000000-0000-0000-0000-000000000083', '35000000-0000-0000-0000-000000000083', '36000000-0000-0000-0000-000000000083', '37000000-0000-0000-0000-000000000083', '38000000-0000-0000-0000-000000000083', 2, N'FIX10 mobile DOB instruction'),
(N'FIX10-MULTI',  '33000000-0000-0000-0000-000000000084', N'09120000044', '34000000-0000-0000-0000-000000000084', '35000000-0000-0000-0000-000000000084', '36000000-0000-0000-0000-000000000084', '37000000-0000-0000-0000-000000000084', '38000000-0000-0000-0000-000000000084', 2, N'FIX10 multi required instruction');

-- Keep the fixture independently repeatable when a runner preserves its
-- disposable database for diagnostics.
DELETE d FROM dbo.VerificationDocuments d JOIN @Scenarios s ON s.ProfileId = d.UserVerificationProfileId;
DELETE k FROM dbo.OrderItemKycStates k JOIN @Scenarios s ON s.ItemId = k.OrderItemId;
DELETE FROM dbo.OrderItems WHERE Id IN (SELECT ItemId FROM @Scenarios);
DELETE FROM dbo.Orders WHERE Id IN (SELECT OrderId FROM @Scenarios);
DELETE FROM dbo.UserVerificationProfiles WHERE Id IN (SELECT ProfileId FROM @Scenarios);
-- V0026 removed Products.KycPolicyVersionId; products no longer reference policy versions.
DELETE r FROM dbo.KycPolicyDocumentRequirements r JOIN @Scenarios s ON s.VersionId = r.KycPolicyVersionId;
-- Order items bought during a run keep an immutable purchase-time reference to these policy
-- versions, so they cannot be dropped and recreated. Upsert them instead, which leaves the
-- fixture repeatable on a database that has already served a run.
DELETE ur FROM dbo.UserRoles ur JOIN @Scenarios s ON s.CustomerId = ur.UserId;
DELETE FROM dbo.Users WHERE Id IN (SELECT CustomerId FROM @Scenarios);

INSERT dbo.Users (Id, FullName, Mobile, Email, PasswordHash, Status, VerificationStatus, IsMobileConfirmed, CreatedAt)
SELECT CustomerId, N'FIX09-3AB-' + Code, Mobile, LOWER(Code) + N'@fix09-3ab.test', @PasswordHash, 1, 0, 1, SYSUTCDATETIME()
FROM @Scenarios s WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Id = s.CustomerId);
INSERT dbo.UserRoles (UserId, RoleId)
SELECT s.CustomerId, r.Id FROM @Scenarios s CROSS JOIN dbo.Roles r
WHERE r.Name = N'Customer' AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = s.CustomerId AND ur.RoleId = r.Id);

INSERT dbo.KycPolicies (Id, Code, Name, IsActive, CreatedAt)
SELECT PolicyId, N'FIX09-3AB-' + Code, N'FIX09 3AB ' + Code, 1, SYSUTCDATETIME()
FROM @Scenarios s WHERE NOT EXISTS (SELECT 1 FROM dbo.KycPolicies p WHERE p.Id = s.PolicyId);
UPDATE p SET p.Code = N'FIX09-3AB-' + s.Code, p.Name = N'FIX09 3AB ' + s.Code, p.IsActive = 1
FROM dbo.KycPolicies p JOIN @Scenarios s ON s.PolicyId = p.Id;
INSERT dbo.KycPolicyVersions (Id, KycPolicyId, Version, Status, CustomerTitle, CustomerInstructions, CreatedAt, PublishedAt)
SELECT VersionId, PolicyId, 1, 2, N'FIX09 3AB ' + Code, Instructions, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM @Scenarios s WHERE NOT EXISTS (SELECT 1 FROM dbo.KycPolicyVersions v WHERE v.Id = s.VersionId);
UPDATE v SET v.KycPolicyId = s.PolicyId, v.Version = 1, v.Status = 2,
    v.CustomerTitle = N'FIX09 3AB ' + s.Code, v.CustomerInstructions = s.Instructions
FROM dbo.KycPolicyVersions v JOIN @Scenarios s ON s.VersionId = v.Id;
INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder, Instructions, RedactionMode, RedactionInstructions)
SELECT NEWID(), VersionId, @DocA, 1, 10,
    CASE WHEN Code IN (N'MULTI', N'FIX10-MULTI') THEN N'راهنمای مدرک الزامی - خط اول' + CHAR(10) + N'<strong>این متن باید عادی نمایش داده شود</strong>' END,
    RedactionMode, Instructions FROM @Scenarios;
INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder, Instructions, RedactionMode)
SELECT NEWID(), VersionId, @DocB, 0, 20, N'راهنمای مدرک اختیاری - خط اول' + CHAR(10) + N'خط دوم', 0 FROM @Scenarios WHERE Code IN (N'MULTI', N'FIX10-MULTI');

INSERT dbo.UserVerificationProfiles (Id, UserId, FirstName, LastName, NationalCode, Status, CreatedAt)
-- These are replacement/redaction journeys.  A rejected profile is deliberately
-- editable; Pending correctly remains read-only in the Customer UI.
SELECT ProfileId, CustomerId, N'FIX09', Code, RIGHT(N'0000000000' + RIGHT(Code, 1), 10), 2, SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.Orders (Id, UserId, OrderNumber, Status, PaymentStatus, SubtotalAmount, FinalAmount, CurrencyType, CreatedAt, PaidAt)
SELECT OrderId, CustomerId, N'FIX09-3AB-' + Code, 2, 2, 6000, 6000, 2, SYSUTCDATETIME(), SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductTitle, Quantity, UnitPrice, TotalPrice, CurrencyType, DeliveryType, DeliveryStatus, RequiresVerification, KycRequirementMode, KycThresholdAmount, KycEvaluatedAmount, KycPolicyVersionId, CreatedAt)
SELECT ItemId, OrderId, @ProductId, N'FIX09-3AB-' + Code, 1, 6000, 6000, 2, 2, 1, 1, 2, 5000, 6000, VersionId, SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.OrderItemKycStates (Id, OrderItemId, Status, CreatedAt, UpdatedAt)
SELECT NEWID(), ItemId, 3, SYSUTCDATETIME(), SYSUTCDATETIME() FROM @Scenarios;

-- The V1 order above keeps an immutable optional snapshot and these tests read that snapshot, never
-- the product's current pointer. The pointer is therefore deliberately left alone: it belongs to the
-- Phase-2F fixture, and overwriting it here made every order placed live by Phase-2G capture this
-- policy instead of the one Phase-2F published.
PRINT N'FIX-09 Phase-3A-B isolated browser fixtures are ready.';
