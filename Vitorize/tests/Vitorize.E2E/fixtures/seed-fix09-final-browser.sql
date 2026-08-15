SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- Isolated final-browser fixture.  The support item intentionally starts with
-- no support ticket, no delivery record and no TicketCreated notification.
DECLARE @CustomerId uniqueidentifier = '33000000-0000-0000-0000-000000000070';
DECLARE @ProfileId uniqueidentifier = '34000000-0000-0000-0000-000000000070';
DECLARE @OrderId uniqueidentifier = '34000000-0000-0000-0000-000000000071';
DECLARE @OrderItemId uniqueidentifier = '34000000-0000-0000-0000-000000000072';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000053';
DECLARE @PolicyId uniqueidentifier = '31000000-0000-0000-0000-000000000042';
DECLARE @PasswordHash nvarchar(400) = N'$2a$11$oRlRYEDBoNTt6xcAxAEcmeoOi/Ketcai3BWjZBLeCjnLhrwxIWc2y';

DELETE FROM dbo.Notifications WHERE UserId = @CustomerId;
DELETE d FROM dbo.OrderItemDeliveries d JOIN dbo.OrderItems i ON i.Id = d.OrderItemId WHERE i.OrderId = @OrderId;
DELETE FROM dbo.OrderItemKycStates WHERE OrderItemId = @OrderItemId;
UPDATE dbo.OrderItems SET SupportTicketId = NULL WHERE OrderId = @OrderId;
DELETE m FROM dbo.TicketMessages m JOIN dbo.Tickets t ON t.Id = m.TicketId WHERE t.OrderId = @OrderId;
DELETE FROM dbo.Tickets WHERE OrderId = @OrderId;
DELETE FROM dbo.OrderItems WHERE OrderId = @OrderId;
DELETE FROM dbo.Orders WHERE Id = @OrderId;
DELETE FROM dbo.VerificationDocuments WHERE UserVerificationProfileId = @ProfileId;
DELETE FROM dbo.UserVerificationProfiles WHERE Id = @ProfileId;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @CustomerId)
    INSERT dbo.Users (Id, FullName, Mobile, Email, PasswordHash, Status, VerificationStatus, IsMobileConfirmed, CreatedAt)
    VALUES (@CustomerId, N'FIX09 Final Support Customer', N'09120000070', N'fix09-final-support@example.test', @PasswordHash, 1, 0, 1, SYSUTCDATETIME());
UPDATE dbo.Users SET FullName = N'FIX09 Final Support Customer', Mobile = N'09120000070', Email = N'fix09-final-support@example.test',
    PasswordHash = @PasswordHash, Status = 1, VerificationStatus = 0, IsMobileConfirmed = 1, IsDeleted = 0
WHERE Id = @CustomerId;
INSERT dbo.UserRoles (UserId, RoleId)
SELECT @CustomerId, r.Id FROM dbo.Roles r
WHERE r.Name = N'Customer' AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = @CustomerId AND ur.RoleId = r.Id);
DELETE ur FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id = ur.RoleId
WHERE ur.UserId = @CustomerId AND r.Name IN (N'Admin', N'SuperAdmin', N'Support');

INSERT dbo.UserVerificationProfiles (Id, UserId, FirstName, LastName, NationalCode, Status, CreatedAt, SubmittedAt)
VALUES (@ProfileId, @CustomerId, N'FIX09', N'Support Customer', N'0070000000', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

INSERT dbo.Orders (Id, UserId, OrderNumber, Status, PaymentStatus, SubtotalAmount, FinalAmount, CurrencyType, CreatedAt, PaidAt)
VALUES (@OrderId, @CustomerId, N'FIX09-FINAL-SUPPORT', 2, 2, 6000, 6000, 2, SYSUTCDATETIME(), SYSUTCDATETIME());
INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductTitle, Quantity, UnitPrice, TotalPrice, CurrencyType, DeliveryType, DeliveryStatus,
    RequiresVerification, KycRequirementMode, KycThresholdAmount, KycEvaluatedAmount, KycPolicyVersionId, SupportTicketId, CreatedAt)
VALUES (@OrderItemId, @OrderId, @ProductId, N'FIX09 Fresh Support Required', 1, 6000, 6000, 2, 3, 1, 1, 2, 5000, 6000, @PolicyId, NULL, SYSUTCDATETIME());
INSERT dbo.OrderItemKycStates (Id, OrderItemId, Status, CreatedAt, UpdatedAt)
VALUES (NEWID(), @OrderItemId, 4, SYSUTCDATETIME(), SYSUTCDATETIME());

-- Finance is reset on every disposable QA database preparation.
DELETE FROM dbo.OrderItemKycFinanceResolutions WHERE OrderItemId = '32000000-0000-0000-0000-000000000104';
INSERT dbo.OrderItemKycFinanceResolutions (Id, OrderItemId, Status, CreatedAt)
VALUES (NEWID(), '32000000-0000-0000-0000-000000000104', 1, SYSUTCDATETIME());
