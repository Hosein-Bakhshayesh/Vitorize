SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- Dedicated, idempotent Phase-2F Customer-browser fixture.  It is applied only
-- to the disposable E2E database by Prepare-E2EDatabase.ps1.
DECLARE @OwnerId uniqueidentifier = '31000000-0000-0000-0000-000000000021';
DECLARE @P2GBrowserDesktopLight uniqueidentifier = '31000000-0000-0000-0000-000000000061';
DECLARE @P2GBrowserDesktopDark uniqueidentifier = '31000000-0000-0000-0000-000000000062';
DECLARE @P2GBrowserMobileLight uniqueidentifier = '31000000-0000-0000-0000-000000000063';
DECLARE @P2GBrowserMobileDark uniqueidentifier = '31000000-0000-0000-0000-000000000064';
DECLARE @P2GBrowserReject uniqueidentifier = '31000000-0000-0000-0000-000000000065';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000053';
DECLARE @PolicyId uniqueidentifier = '31000000-0000-0000-0000-000000000041';
DECLARE @V1 uniqueidentifier = '31000000-0000-0000-0000-000000000042';
DECLARE @V2 uniqueidentifier = '31000000-0000-0000-0000-000000000043';
DECLARE @DocA uniqueidentifier = '31000000-0000-0000-0000-000000000044';
DECLARE @DocB uniqueidentifier = '31000000-0000-0000-0000-000000000045';
DECLARE @Main uniqueidentifier = '32000000-0000-0000-0000-000000000001';
DECLARE @V2Order uniqueidentifier = '32000000-0000-0000-0000-000000000002';
DECLARE @Mixed uniqueidentifier = '32000000-0000-0000-0000-000000000003';
DECLARE @Payment uniqueidentifier = '32000000-0000-0000-0000-000000000004';
DECLARE @Profile uniqueidentifier = '32000000-0000-0000-0000-000000000005';
DECLARE @SupportTicket uniqueidentifier = '32000000-0000-0000-0000-000000000006';
DECLARE @HeldCanary nvarchar(200) = N'FIX09-P2F-HELD-CANARY-DO-NOT-RENDER';
DECLARE @StorageCanary nvarchar(300) = N'kyc-private:' + REPLACE(CONVERT(nvarchar(36), @OwnerId), N'-', N'') + N'/document-canary.jpg';

-- One clean, mobile-confirmed but unsatisfied customer is dedicated to each
-- responsive 2G browser project. Their orders are produced by the browser;
-- this fixture never seeds an order or KYC profile for them.
DECLARE @P2GCustomers TABLE (Id uniqueidentifier, FullName nvarchar(200), Mobile nvarchar(20), Email nvarchar(320));
INSERT @P2GCustomers VALUES
(@P2GBrowserDesktopLight, N'P2G Browser Desktop Light', N'09120000015', N'p2g-desktop-light@example.test'),
(@P2GBrowserDesktopDark, N'P2G Browser Desktop Dark', N'09120000016', N'p2g-desktop-dark@example.test'),
(@P2GBrowserMobileLight, N'P2G Browser Mobile Light', N'09120000017', N'p2g-mobile-light@example.test'),
(@P2GBrowserMobileDark, N'P2G Browser Mobile Dark', N'09120000018', N'p2g-mobile-dark@example.test'),
(@P2GBrowserReject, N'P2G Browser Reject', N'09120000019', N'p2g-reject@example.test');
DECLARE @P2GPasswordHash nvarchar(400) = N'$2a$11$oRlRYEDBoNTt6xcAxAEcmeoOi/Ketcai3BWjZBLeCjnLhrwxIWc2y';
INSERT dbo.Users (Id, FullName, Mobile, Email, PasswordHash, Status, VerificationStatus, IsMobileConfirmed, CreatedAt)
SELECT c.Id, c.FullName, c.Mobile, c.Email, @P2GPasswordHash, 1, 0, 1, SYSUTCDATETIME()
FROM @P2GCustomers c WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Id = c.Id);
UPDATE u SET FullName = c.FullName, Email = c.Email, PasswordHash = @P2GPasswordHash, Status = 1, VerificationStatus = 0, IsMobileConfirmed = 1, IsDeleted = 0
FROM dbo.Users u JOIN @P2GCustomers c ON c.Id = u.Id;
INSERT dbo.UserRoles (UserId, RoleId)
SELECT c.Id, r.Id FROM @P2GCustomers c CROSS JOIN dbo.Roles r
WHERE r.Name = N'Customer' AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = c.Id AND ur.RoleId = r.Id);
DELETE ur FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId JOIN @P2GCustomers c ON c.Id = ur.UserId
WHERE r.Name IN (N'Admin', N'SuperAdmin', N'Support');

DECLARE @OrderIds TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT @OrderIds VALUES (@Main), (@V2Order), (@Mixed), (@Payment);

DELETE d FROM dbo.OrderItemDeliveries d JOIN dbo.OrderItems i ON i.Id = d.OrderItemId JOIN @OrderIds o ON o.Id = i.OrderId;
DELETE s FROM dbo.OrderItemKycStates s JOIN dbo.OrderItems i ON i.Id = s.OrderItemId JOIN @OrderIds o ON o.Id = i.OrderId;
DELETE r FROM dbo.OrderItemKycFinanceResolutions r JOIN dbo.OrderItems i ON i.Id = r.OrderItemId JOIN @OrderIds o ON o.Id = i.OrderId;
DELETE v FROM dbo.GiftCodeReservations v JOIN dbo.GiftCodes g ON g.Id = v.GiftCodeId JOIN dbo.OrderItems i ON i.Id = g.OrderItemId JOIN @OrderIds o ON o.Id = i.OrderId;
DELETE g FROM dbo.GiftCodes g JOIN dbo.OrderItems i ON i.Id = g.OrderItemId JOIN @OrderIds o ON o.Id = i.OrderId;
DELETE h FROM dbo.OrderStatusHistories h JOIN @OrderIds o ON o.Id = h.OrderId;
DELETE FROM dbo.OrderItemInputValues WHERE OrderItemId IN (SELECT i.Id FROM dbo.OrderItems i JOIN @OrderIds o ON o.Id = i.OrderId);
UPDATE dbo.OrderItems SET SupportTicketId = NULL WHERE OrderId IN (SELECT Id FROM @OrderIds);
DELETE FROM dbo.Tickets WHERE Id = @SupportTicket;
DELETE FROM dbo.OrderItems WHERE OrderId IN (SELECT Id FROM @OrderIds);
DELETE FROM dbo.Orders WHERE Id IN (SELECT Id FROM @OrderIds);
DELETE FROM dbo.VerificationDocuments WHERE UserVerificationProfileId = @Profile;
DELETE FROM dbo.UserVerificationProfiles WHERE Id = @Profile;

-- Product is now on V2.  V1 purchased rows below retain their immutable V1 snapshot.
UPDATE dbo.Products SET RequiresVerification = 1, KycRequirementMode = 2, KycThresholdAmount = 5000, KycPolicyVersionId = @V2,
    DeliveryType = 1, BasePrice = 6000, IsActive = 1, IsDeleted = 0
WHERE Id = @ProductId;
-- The existing FIX-02 staged-input product becomes the mobile 2G regression
-- fixture: its checkout inputs remain mandatory even though KYC is now post-payment.
UPDATE dbo.Products SET RequiresVerification = 1, KycRequirementMode = 1, KycThresholdAmount = NULL, KycPolicyVersionId = @V2,
    IsActive = 1, IsDeleted = 0
WHERE Id = '31000000-0000-0000-0000-000000000036';
UPDATE dbo.KycPolicyVersions SET CustomerTitle = N'2F V1 Purchase Policy', CustomerInstructions = N'2F V1 purchase-time instructions.' WHERE Id = @V1;
UPDATE dbo.KycPolicyVersions SET CustomerTitle = N'2F V2 Purchase Policy', CustomerInstructions = N'2F V2 purchase-time instructions.' WHERE Id = @V2;
-- Phase 3A keeps V1 as the historical redaction fixture; the current V2
-- checkout policy remains unchanged for the Phase-2G direct-upload journey.
UPDATE dbo.KycPolicyDocumentRequirements
SET RedactionMode = CASE WHEN KycDocumentTypeId = @DocB THEN 2 ELSE 0 END,
    RedactionInstructions = CASE WHEN KycDocumentTypeId = @DocB THEN N'پیش از ارسال، اطلاعات غیرضروری را با مستطیل تیره بپوشانید.' ELSE NULL END
WHERE KycPolicyVersionId = @V1;
IF NOT EXISTS (SELECT 1 FROM dbo.KycPolicyDocumentRequirements WHERE KycPolicyVersionId = @V2 AND KycDocumentTypeId = @DocA)
    INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder) VALUES (NEWID(), @V2, @DocA, 1, 10);
IF NOT EXISTS (SELECT 1 FROM dbo.KycPolicyDocumentRequirements WHERE KycPolicyVersionId = @V2 AND KycDocumentTypeId = @DocB)
    INSERT dbo.KycPolicyDocumentRequirements (Id, KycPolicyVersionId, KycDocumentTypeId, IsRequired, SortOrder) VALUES (NEWID(), @V2, @DocB, 1, 20);

INSERT dbo.UserVerificationProfiles (Id, UserId, FirstName, LastName, NationalCode, BirthDate, Status, AdminNote, EncryptedPayload, CreatedAt)
VALUES (@Profile, @OwnerId, N'Browser', N'Customer', N'1234567890', '1990-01-01', 0, N'FIX09-P2F-INTERNAL-NOTE', NULL, SYSUTCDATETIME());
INSERT dbo.VerificationDocuments (Id, UserVerificationProfileId, DocumentType, KycDocumentTypeId, FilePath, Status, CreatedAt)
VALUES (NEWID(), @Profile, 1, @DocA, @StorageCanary, 1, SYSUTCDATETIME());

INSERT dbo.Orders (Id, UserId, OrderNumber, Status, PaymentStatus, SubtotalAmount, FinalAmount, CurrencyType, CreatedAt, PaidAt)
VALUES
(@Main, @OwnerId, N'P2F-MAIN', 2, 2, 54000, 54000, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
(@V2Order, @OwnerId, N'P2F-V2', 2, 2, 6000, 6000, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
(@Mixed, @OwnerId, N'P2F-MIXED', 2, 2, 24000, 24000, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
(@Payment, @OwnerId, N'P2F-PAYMENT', 2, 2, 12000, 12000, 2, SYSUTCDATETIME(), SYSUTCDATETIME());
INSERT dbo.Tickets (Id, UserId, OrderId, Subject, Department, Priority, Status, IsFulfillmentTicket, CreatedAt)
VALUES (@SupportTicket, @OwnerId, @Main, N'P2F support fulfillment', 2, 1, 1, 1, SYSUTCDATETIME());

DECLARE @Items TABLE (Id uniqueidentifier PRIMARY KEY, OrderId uniqueidentifier, Title nvarchar(250), DeliveryType tinyint, DeliveryStatus tinyint,
    KycMode tinyint, PolicyId uniqueidentifier NULL, State tinyint NULL, Support bit NOT NULL);
INSERT @Items VALUES
('32000000-0000-0000-0000-000000000101', @Main, N'P2F Awaiting Submission', 1, 1, 2, @V1, 3, 0),
('32000000-0000-0000-0000-000000000102', @Main, N'P2F Awaiting Review', 1, 1, 2, @V1, 4, 0),
('32000000-0000-0000-0000-000000000103', @Main, N'P2F Rejected', 1, 1, 2, @V1, 5, 0),
('32000000-0000-0000-0000-000000000104', @Main, N'P2F Final Rejected', 1, 1, 2, @V1, 6, 0),
('32000000-0000-0000-0000-000000000105', @Main, N'P2F Instant Delivered', 1, 2, 2, @V1, 2, 0),
('32000000-0000-0000-0000-000000000106', @Main, N'P2F Manual Pending', 2, 1, 2, @V1, 2, 0),
('32000000-0000-0000-0000-000000000107', @Main, N'P2F Support Pending', 3, 1, 2, @V1, 2, 1),
('32000000-0000-0000-0000-000000000108', @Main, N'P2F Not Required', 2, 1, 0, NULL, 1, 0),
('32000000-0000-0000-0000-000000000109', @Main, N'P2F Legacy', 2, 1, 0, NULL, NULL, 0),
('32000000-0000-0000-0000-000000000110', @Main, N'P2F Manual Held', 2, 1, 2, @V1, 4, 0),
('32000000-0000-0000-0000-000000000111', @Main, N'P2F Support Held', 3, 1, 2, @V1, 4, 0),
-- Owned by the final browser-closure spec, which fulfils it. It used to spend 'P2F Manual Pending'
-- instead, and because it runs first that left the Phase-2F Admin spec asserting a delivery button
-- on an item that was already delivered. The title deliberately shares no substring with the other
-- rows so row filters stay unambiguous.
('32000000-0000-0000-0000-000000000112', @Main, N'P2F Closure Delivery', 2, 1, 2, @V1, 2, 0),
('32000000-0000-0000-0000-000000000201', @V2Order, N'P2F Instant Release Pending', 1, 1, 2, @V2, 2, 0),
('32000000-0000-0000-0000-000000000202', @V2Order, N'P2F V2 Awaiting Submission', 1, 1, 2, @V2, 3, 0),
('32000000-0000-0000-0000-000000000301', @Mixed, N'P2F Mixed Delivered Legacy', 1, 2, 0, NULL, NULL, 0),
('32000000-0000-0000-0000-000000000302', @Mixed, N'P2F Mixed Awaiting Submission', 1, 1, 2, @V1, 3, 0),
('32000000-0000-0000-0000-000000000303', @Mixed, N'P2F Mixed Awaiting Review', 1, 1, 2, @V1, 4, 0),
('32000000-0000-0000-0000-000000000304', @Mixed, N'P2F Mixed Manual Pending', 2, 1, 2, @V1, 2, 0),
('32000000-0000-0000-0000-000000000305', @Mixed, N'P2F Mixed Support Satisfied', 3, 1, 2, @V1, 2, 1),
('32000000-0000-0000-0000-000000000401', @Payment, N'P2F Payment Delivered', 1, 2, 0, NULL, NULL, 0),
('32000000-0000-0000-0000-000000000402', @Payment, N'P2F Payment Awaiting Submission', 1, 1, 2, @V1, 3, 0);

INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductTitle, Quantity, UnitPrice, TotalPrice, CurrencyType, DeliveryType, DeliveryStatus,
    RequiresVerification, KycRequirementMode, KycThresholdAmount, KycEvaluatedAmount, KycPolicyVersionId, SupportTicketId, CreatedAt, DeliveredAt)
SELECT Id, OrderId, @ProductId, Title, 1, 6000, 6000, 2, DeliveryType, DeliveryStatus,
    CASE WHEN KycMode = 0 THEN 0 ELSE 1 END, KycMode, CASE WHEN KycMode = 2 THEN 5000 ELSE NULL END, CASE WHEN KycMode = 0 THEN 0 ELSE 6000 END,
    PolicyId, CASE WHEN Support = 1 THEN @SupportTicket ELSE NULL END, SYSUTCDATETIME(), CASE WHEN DeliveryStatus = 2 THEN SYSUTCDATETIME() ELSE NULL END
FROM @Items;

INSERT dbo.OrderItemKycStates (Id, OrderItemId, Status, CreatedAt, UpdatedAt, SatisfiedAt)
SELECT NEWID(), Id, State, SYSUTCDATETIME(), SYSUTCDATETIME(), CASE WHEN State = 2 THEN SYSUTCDATETIME() ELSE NULL END FROM @Items WHERE State IS NOT NULL;

DECLARE @DeliveredCode uniqueidentifier = '32000000-0000-0000-0000-000000000501';
DECLARE @HeldCode uniqueidentifier = '32000000-0000-0000-0000-000000000502';
DECLARE @CheckoutAvailableCode uniqueidentifier = '32000000-0000-0000-0000-000000000503';
DECLARE @P2GCheckoutCode1 uniqueidentifier = '32000000-0000-0000-0000-000000000511';
DECLARE @P2GCheckoutCode2 uniqueidentifier = '32000000-0000-0000-0000-000000000512';
DECLARE @P2GCheckoutCode3 uniqueidentifier = '32000000-0000-0000-0000-000000000513';
DECLARE @P2GCheckoutCode4 uniqueidentifier = '32000000-0000-0000-0000-000000000514';
DECLARE @P2GCheckoutCode5 uniqueidentifier = '32000000-0000-0000-0000-000000000515';
-- A previous run may have allocated these pool codes and recorded a delivery against them, and
-- OrderItemDeliveries.GiftCodeId is enforced, so the dependent rows have to go first. Without this
-- the whole fixture aborts the second time it is applied to a database that has served a run.
DELETE d FROM dbo.OrderItemDeliveries d
WHERE d.GiftCodeId IN (@CheckoutAvailableCode, @P2GCheckoutCode1, @P2GCheckoutCode2, @P2GCheckoutCode3, @P2GCheckoutCode4, @P2GCheckoutCode5);
DELETE v FROM dbo.GiftCodeReservations v
WHERE v.GiftCodeId IN (@CheckoutAvailableCode, @P2GCheckoutCode1, @P2GCheckoutCode2, @P2GCheckoutCode3, @P2GCheckoutCode4, @P2GCheckoutCode5);
DELETE FROM dbo.GiftCodes WHERE Id IN (@CheckoutAvailableCode, @P2GCheckoutCode1, @P2GCheckoutCode2, @P2GCheckoutCode3, @P2GCheckoutCode4, @P2GCheckoutCode5);
INSERT dbo.GiftCodes (Id, ProductId, OrderItemId, EncryptedCode, MaskedCode, Status, EncryptionVersion, CodeHashFingerprint, ReservedByUserId, SoldAt, DeliveredAt, CreatedAt)
VALUES
(@DeliveredCode, @ProductId, '32000000-0000-0000-0000-000000000105', N'P2F-DELIVERED-CODE', N'****2F', 3, 0, N'fix09-p2f-delivered', @OwnerId, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME()),
(@HeldCode, @ProductId, '32000000-0000-0000-0000-000000000201', @HeldCanary, N'****HELD', 2, 0, N'fix09-p2f-held', @OwnerId, SYSUTCDATETIME(), NULL, SYSUTCDATETIME()),
(@CheckoutAvailableCode, @ProductId, NULL, N'P2F-CHECKOUT-AVAILABLE', N'****P2F', 0, 0, N'fix09-p2f-checkout', NULL, NULL, NULL, SYSUTCDATETIME()),
(@P2GCheckoutCode1, @ProductId, NULL, N'P2G-CHECKOUT-1', N'****2G1', 0, 0, N'fix09-p2g-checkout-1', NULL, NULL, NULL, SYSUTCDATETIME()),
(@P2GCheckoutCode2, @ProductId, NULL, N'P2G-CHECKOUT-2', N'****2G2', 0, 0, N'fix09-p2g-checkout-2', NULL, NULL, NULL, SYSUTCDATETIME()),
(@P2GCheckoutCode3, @ProductId, NULL, N'P2G-CHECKOUT-3', N'****2G3', 0, 0, N'fix09-p2g-checkout-3', NULL, NULL, NULL, SYSUTCDATETIME()),
(@P2GCheckoutCode4, @ProductId, NULL, N'P2G-CHECKOUT-4', N'****2G4', 0, 0, N'fix09-p2g-checkout-4', NULL, NULL, NULL, SYSUTCDATETIME()),
(@P2GCheckoutCode5, @ProductId, NULL, N'P2G-CHECKOUT-5', N'****2G5', 0, 0, N'fix09-p2g-checkout-5', NULL, NULL, NULL, SYSUTCDATETIME());
INSERT dbo.OrderItemDeliveries (Id, OrderItemId, DeliveryType, GiftCodeId, DeliveredContent, ContentHash, IsVisibleToCustomer, CreatedAt)
VALUES (NEWID(), '32000000-0000-0000-0000-000000000105', 1, @DeliveredCode, N'P2F-DELIVERED-CODE', REPLICATE(N'A', 64), 1, SYSUTCDATETIME());

-- This product is Instant, so its inventory is the gift-code pool, and several browser specs buy
-- from that one pool during a run. Seeding exactly as many codes as a single spec consumes leaves
-- the later specs facing an out-of-stock product and blaming the storefront for it, so keep real
-- head-room. These rows are identifiable by fingerprint and are rebuilt on every application.
DECLARE @PoolSpares int = 40;
DELETE v FROM dbo.GiftCodeReservations v JOIN dbo.GiftCodes g ON g.Id = v.GiftCodeId
WHERE g.CodeHashFingerprint LIKE N'fix09-pool-%';
DELETE d FROM dbo.OrderItemDeliveries d JOIN dbo.GiftCodes g ON g.Id = d.GiftCodeId
WHERE g.CodeHashFingerprint LIKE N'fix09-pool-%';
DELETE FROM dbo.GiftCodes WHERE CodeHashFingerprint LIKE N'fix09-pool-%';

-- Two pools, because the two purchase routes look the product up differently: the storefront picks
-- the default SKU and needs codes attached to it, while an API checkout that posts only a productId
-- needs product-level codes. The seeded product carries a SKU from when it was a Manual product, so
-- both routes are live and both have to find inventory. These rows are rebuilt on every application.
-- Turning the product Instant leaves behind the SKU it owned as a Manual product. Production keeps
-- the two consistent (an Instant product's SKU is always gift-code backed) and Checkout enforces it,
-- so the fixture has to uphold the same invariant instead of shipping a combination the application
-- would never create.
UPDATE v SET v.StockMode = 1
FROM dbo.ProductVariants v WHERE v.ProductId = @ProductId;

WITH Spares AS (
    SELECT TOP (@PoolSpares) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Ordinal FROM sys.all_objects
)
INSERT dbo.GiftCodes (Id, ProductId, ProductVariantId, OrderItemId, EncryptedCode, MaskedCode, Status,
    EncryptionVersion, CodeHashFingerprint, ReservedByUserId, SoldAt, DeliveredAt, CreatedAt)
SELECT NEWID(), @ProductId, NULL, NULL, N'P2G-CHECKOUT-P-' + CAST(Ordinal AS nvarchar(4)),
    N'****' + RIGHT(N'000' + CAST(Ordinal AS nvarchar(4)), 3), 0, 0,
    N'fix09-pool-p-' + CAST(Ordinal AS nvarchar(4)), NULL, NULL, NULL, SYSUTCDATETIME()
FROM Spares;
WITH Spares AS (
    SELECT TOP (@PoolSpares) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Ordinal FROM sys.all_objects
)
INSERT dbo.GiftCodes (Id, ProductId, ProductVariantId, OrderItemId, EncryptedCode, MaskedCode, Status,
    EncryptionVersion, CodeHashFingerprint, ReservedByUserId, SoldAt, DeliveredAt, CreatedAt)
SELECT NEWID(), @ProductId, v.Id, NULL, N'P2G-CHECKOUT-V-' + CAST(Ordinal AS nvarchar(4)),
    N'****' + RIGHT(N'000' + CAST(Ordinal AS nvarchar(4)), 3), 0, 0,
    N'fix09-pool-v-' + CAST(Ordinal AS nvarchar(4)), NULL, NULL, NULL, SYSUTCDATETIME()
FROM Spares
CROSS JOIN (SELECT TOP 1 Id FROM dbo.ProductVariants WHERE ProductId = @ProductId AND IsDefault = 1 AND IsActive = 1) v;
PRINT N'FIX-09 Phase-2F Customer browser fixture is ready.';
