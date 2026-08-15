/*
  FIX-09 Phase 3B-A: persisted, versioned customer-action KYC deadlines.
  Historical rows intentionally remain NULL; expiry execution is not activated.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.KycPolicyVersions', N'CustomerActionDeadlineHours') IS NULL
    ALTER TABLE dbo.KycPolicyVersions ADD CustomerActionDeadlineHours int NULL;

IF COL_LENGTH(N'dbo.OrderItems', N'KycCustomerActionDeadlineHours') IS NULL
    ALTER TABLE dbo.OrderItems ADD KycCustomerActionDeadlineHours int NULL;

IF COL_LENGTH(N'dbo.OrderItemKycStates', N'CustomerActionDeadlineAt') IS NULL
    ALTER TABLE dbo.OrderItemKycStates ADD CustomerActionDeadlineAt datetime2 NULL;

GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.KycPolicyVersions')
      AND name = N'CK_KycPolicyVersions_CustomerActionDeadlineHours'
)
    ALTER TABLE dbo.KycPolicyVersions ADD CONSTRAINT CK_KycPolicyVersions_CustomerActionDeadlineHours
        CHECK (CustomerActionDeadlineHours IS NULL OR CustomerActionDeadlineHours > 0);

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.OrderItems')
      AND name = N'CK_OrderItems_KycCustomerActionDeadlineHours'
)
    ALTER TABLE dbo.OrderItems ADD CONSTRAINT CK_OrderItems_KycCustomerActionDeadlineHours
        CHECK (KycCustomerActionDeadlineHours IS NULL OR KycCustomerActionDeadlineHours > 0);

IF EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.OrderItemKycStates')
      AND name = N'CK_OrderItemKycStates_Status'
)
    ALTER TABLE dbo.OrderItemKycStates DROP CONSTRAINT CK_OrderItemKycStates_Status;

ALTER TABLE dbo.OrderItemKycStates ADD CONSTRAINT CK_OrderItemKycStates_Status
    CHECK (Status IN (1,2,3,4,5,6,7));

COMMIT TRANSACTION;
