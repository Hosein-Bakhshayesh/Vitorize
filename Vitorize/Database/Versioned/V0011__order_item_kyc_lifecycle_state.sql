/*
  FIX-09 Phase 2A: mutable post-payment KYC lifecycle state, deliberately
  separate from the immutable KYC purchase snapshot on dbo.OrderItems.
  No rows are backfilled: absence means the item is not managed by this lifecycle.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderItemKycStates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItemKycStates
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_OrderItemKycStates_Id DEFAULT (newsequentialid())
            CONSTRAINT PK_OrderItemKycStates PRIMARY KEY,
        OrderItemId uniqueidentifier NOT NULL,
        Status tinyint NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_OrderItemKycStates_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_OrderItemKycStates_UpdatedAt DEFAULT (sysutcdatetime()),
        SatisfiedAt datetime2 NULL,
        SatisfiedByVerificationProfileId uniqueidentifier NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT UX_OrderItemKycStates_OrderItemId UNIQUE (OrderItemId),
        CONSTRAINT CK_OrderItemKycStates_Status CHECK (Status IN (1,2,3,4,5,6)),
        CONSTRAINT FK_OrderItemKycStates_OrderItems FOREIGN KEY (OrderItemId)
            REFERENCES dbo.OrderItems(Id),
        CONSTRAINT FK_OrderItemKycStates_SatisfiedByVerificationProfile FOREIGN KEY (SatisfiedByVerificationProfileId)
            REFERENCES dbo.UserVerificationProfiles(Id)
    );

    CREATE INDEX IX_OrderItemKycStates_SatisfiedByVerificationProfileId
        ON dbo.OrderItemKycStates(SatisfiedByVerificationProfileId)
        WHERE SatisfiedByVerificationProfileId IS NOT NULL;
END;

COMMIT TRANSACTION;
