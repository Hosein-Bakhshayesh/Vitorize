SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- Dedicated Phase-3B-B rows.  They are intentionally separate from Phase-3A
-- replacement journeys: every destructive browser operation has its own owner.
DECLARE @PasswordHash nvarchar(400) = N'$2a$11$oRlRYEDBoNTt6xcAxAEcmeoOi/Ketcai3BWjZBLeCjnLhrwxIWc2y';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000053';
DECLARE @DocA uniqueidentifier = '31000000-0000-0000-0000-000000000044';

DECLARE @Scenarios TABLE (
    Code nvarchar(40) NOT NULL PRIMARY KEY, CustomerId uniqueidentifier NOT NULL, Mobile nvarchar(20) NOT NULL,
    ProfileId uniqueidentifier NOT NULL, PolicyId uniqueidentifier NOT NULL, VersionId uniqueidentifier NOT NULL,
    OrderId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL, Lifecycle tinyint NOT NULL,
    DeadlineOffsetHours int NULL, DeadlineHours int NULL, RedactionMode tinyint NOT NULL, DeliveryType tinyint NOT NULL);
INSERT @Scenarios VALUES
 (N'FUTURE',   '33000000-0000-0000-0000-000000000091', N'09120000051', '34000000-0000-0000-0000-000000000091', '35000000-0000-0000-0000-000000000091', '36000000-0000-0000-0000-000000000091', '37000000-0000-0000-0000-000000000091', '38000000-0000-0000-0000-000000000091', 3, 48, 72, 0, 2),
 (N'OVERDUE',  '33000000-0000-0000-0000-000000000092', N'09120000052', '34000000-0000-0000-0000-000000000092', '35000000-0000-0000-0000-000000000092', '36000000-0000-0000-0000-000000000092', '37000000-0000-0000-0000-000000000092', '38000000-0000-0000-0000-000000000092', 3, -48, 72, 0, 2),
 (N'REJECTED', '33000000-0000-0000-0000-000000000093', N'09120000053', '34000000-0000-0000-0000-000000000093', '35000000-0000-0000-0000-000000000093', '36000000-0000-0000-0000-000000000093', '37000000-0000-0000-0000-000000000093', '38000000-0000-0000-0000-000000000093', 5, -48, 72, 0, 2),
 (N'EXPIRED',  '33000000-0000-0000-0000-000000000094', N'09120000054', '34000000-0000-0000-0000-000000000094', '35000000-0000-0000-0000-000000000094', '36000000-0000-0000-0000-000000000094', '37000000-0000-0000-0000-000000000094', '38000000-0000-0000-0000-000000000094', 7, NULL, 72, 0, 2),
 (N'REOPEN',   '33000000-0000-0000-0000-000000000095', N'09120000055', '34000000-0000-0000-0000-000000000095', '35000000-0000-0000-0000-000000000095', '36000000-0000-0000-0000-000000000095', '37000000-0000-0000-0000-000000000095', '38000000-0000-0000-0000-000000000095', 7, NULL, 72, 2, 2),
 (N'FINAL',    '33000000-0000-0000-0000-000000000096', N'09120000056', '34000000-0000-0000-0000-000000000096', '35000000-0000-0000-0000-000000000096', '36000000-0000-0000-0000-000000000096', '37000000-0000-0000-0000-000000000096', '38000000-0000-0000-0000-000000000096', 7, NULL, 72, 0, 2),
 (N'REVIEW',   '33000000-0000-0000-0000-000000000097', N'09120000057', '34000000-0000-0000-0000-000000000097', '35000000-0000-0000-0000-000000000097', '36000000-0000-0000-0000-000000000097', '37000000-0000-0000-0000-000000000097', '38000000-0000-0000-0000-000000000097', 4, NULL, 72, 0, 2),
 (N'NODEADLINE','33000000-0000-0000-0000-000000000098',N'09120000058', '34000000-0000-0000-0000-000000000098', '35000000-0000-0000-0000-000000000098', '36000000-0000-0000-0000-000000000098', '37000000-0000-0000-0000-000000000098', '38000000-0000-0000-0000-000000000098', 3, NULL, NULL, 0, 2),
 (N'INSTANT',  '33000000-0000-0000-0000-000000000099', N'09120000059', '34000000-0000-0000-0000-000000000099', '35000000-0000-0000-0000-000000000099', '36000000-0000-0000-0000-000000000099', '37000000-0000-0000-0000-000000000099', '38000000-0000-0000-0000-000000000099', 7, NULL, 72, 0, 1),
 (N'SECURITY', '33000000-0000-0000-0000-000000000100', N'09120000060', '34000000-0000-0000-0000-000000000100', '35000000-0000-0000-0000-000000000100', '36000000-0000-0000-0000-000000000100', '37000000-0000-0000-0000-000000000100', '38000000-0000-0000-0000-000000000100', 3, 48, 72, 0, 2);

DELETE FROM dbo.GiftCodes WHERE Id = '39000000-0000-0000-0000-000000000099';
DELETE d FROM dbo.VerificationDocuments d JOIN @Scenarios s ON s.ProfileId = d.UserVerificationProfileId;
DELETE k FROM dbo.OrderItemKycStates k JOIN @Scenarios s ON s.ItemId = k.OrderItemId;
DELETE FROM dbo.OrderItems WHERE Id IN (SELECT ItemId FROM @Scenarios);
DELETE FROM dbo.Orders WHERE Id IN (SELECT OrderId FROM @Scenarios);
DELETE FROM dbo.UserVerificationProfiles WHERE Id IN (SELECT ProfileId FROM @Scenarios);
DELETE r FROM dbo.KycPolicyDocumentRequirements r JOIN @Scenarios s ON s.VersionId = r.KycPolicyVersionId;
DELETE FROM dbo.KycPolicyVersions WHERE Id IN (SELECT VersionId FROM @Scenarios);
DELETE FROM dbo.KycPolicies WHERE Id IN (SELECT PolicyId FROM @Scenarios);
DELETE ur FROM dbo.UserRoles ur JOIN @Scenarios s ON s.CustomerId = ur.UserId;
DELETE FROM dbo.Users WHERE Id IN (SELECT CustomerId FROM @Scenarios);

INSERT dbo.Users (Id, FullName, Mobile, Email, PasswordHash, Status, VerificationStatus, IsMobileConfirmed, CreatedAt)
SELECT CustomerId, N'FIX09-3BB-' + Code, Mobile, LOWER(Code) + N'@fix09-3bb.test', @PasswordHash, 1, 0, 1, SYSUTCDATETIME()
FROM @Scenarios;
INSERT dbo.UserRoles (UserId, RoleId)
SELECT s.CustomerId, r.Id FROM @Scenarios s CROSS JOIN dbo.Roles r
WHERE r.Name = N'Customer';

INSERT dbo.KycPolicies (Id, Code, Name, IsActive, CreatedAt)
SELECT PolicyId, N'FIX09-3BB-' + Code, N'FIX09 3BB ' + Code, 1, SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.KycPolicyVersions (Id, KycPolicyId, Version, Status, CustomerTitle, CustomerInstructions, CustomerActionDeadlineHours, CreatedAt, PublishedAt)
SELECT VersionId, PolicyId, 1, 2, N'FIX09 3BB ' + Code, N'FIX09-3BB deterministic deadline scenario', DeadlineHours, SYSUTCDATETIME(), SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder, RedactionMode, RedactionInstructions)
SELECT NEWID(), VersionId, @DocA, 1, 10, RedactionMode, CASE WHEN RedactionMode = 2 THEN N'FIX09-3BB redact before upload' END FROM @Scenarios;

-- Pending is intentionally reserved for AwaitingReview.  The other rows begin
-- rejected so the Customer page permits the required replacement/upload actions.
INSERT dbo.UserVerificationProfiles (Id, UserId, FirstName, LastName, NationalCode, Status, CreatedAt)
SELECT ProfileId, CustomerId, N'FIX09', Code, RIGHT(N'0000000000' + RIGHT(Code, 1), 10), CASE WHEN Lifecycle = 4 THEN 0 ELSE 2 END, SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.Orders (Id, UserId, OrderNumber, Status, PaymentStatus, SubtotalAmount, FinalAmount, CurrencyType, CreatedAt, PaidAt)
SELECT OrderId, CustomerId, N'FIX09-3BB-' + Code, 2, 2, 6000, 6000, 2, SYSUTCDATETIME(), SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductTitle, Quantity, UnitPrice, TotalPrice, CurrencyType, DeliveryType, DeliveryStatus,
    RequiresVerification, KycRequirementMode, KycThresholdAmount, KycEvaluatedAmount, KycPolicyVersionId, KycCustomerActionDeadlineHours, CreatedAt)
SELECT ItemId, OrderId, @ProductId, N'FIX09-3BB-' + Code, 1, 6000, 6000, 2, DeliveryType, 1, 1, 2, 5000, 6000, VersionId, DeadlineHours, SYSUTCDATETIME() FROM @Scenarios;
INSERT dbo.OrderItemKycStates (Id, OrderItemId, Status, CustomerActionDeadlineAt, CreatedAt, UpdatedAt)
SELECT NEWID(), ItemId, Lifecycle, CASE WHEN DeadlineOffsetHours IS NULL THEN NULL ELSE DATEADD(hour, DeadlineOffsetHours, SYSUTCDATETIME()) END, SYSUTCDATETIME(), SYSUTCDATETIME() FROM @Scenarios;

-- A held exact allocation is never released simply because the item is expired.
INSERT dbo.GiftCodes (Id, ProductId, OrderItemId, EncryptedCode, MaskedCode, Status, EncryptionVersion, CodeHashFingerprint, ReservedByUserId, SoldAt, CreatedAt)
SELECT '39000000-0000-0000-0000-000000000099', @ProductId, ItemId, N'FIX09-3BB-INSTANT-HELD', N'****3BB', 2, 0, N'fix09-3bb-instant-held', CustomerId, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM @Scenarios WHERE Code = N'INSTANT';

PRINT N'FIX-09 Phase-3B-B isolated deadline browser fixtures are ready.';
