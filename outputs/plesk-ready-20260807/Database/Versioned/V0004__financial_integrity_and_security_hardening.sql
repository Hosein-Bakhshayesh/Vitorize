/*
    Phase 2 financial-integrity and security hardening.
    Database-First / SQL Server / additive and idempotent for the canonical schema.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.PaymentCallbacks', N'U') IS NULL OR
       OBJECT_ID(N'dbo.WalletTransactions', N'U') IS NULL OR
       OBJECT_ID(N'dbo.UserVerificationProfiles', N'U') IS NULL OR
       OBJECT_ID(N'dbo.OrderItemDeliveries', N'U') IS NULL OR
       OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
       OR OBJECT_ID(N'dbo.OtpCodes', N'U') IS NULL
       OR OBJECT_ID(N'dbo.WalletTopUps', N'U') IS NULL
        THROW 51300, 'The canonical Vitorize baseline is required before V0004.', 1;

    IF EXISTS
    (
        SELECT Gateway, Authority
        FROM dbo.Payments
        WHERE Authority IS NOT NULL
        GROUP BY Gateway, Authority
        HAVING COUNT(*) > 1
    )
        THROW 51301, 'Duplicate payment Gateway/Authority values must be reconciled before V0004.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Payments')
          AND name = N'UX_Payments_Gateway_Authority'
    )
        CREATE UNIQUE INDEX UX_Payments_Gateway_Authority
            ON dbo.Payments(Gateway, Authority)
            WHERE Authority IS NOT NULL;

    IF EXISTS
    (
        SELECT Gateway, Authority FROM dbo.WalletTopUps WHERE Authority IS NOT NULL
        GROUP BY Gateway, Authority HAVING COUNT(*) > 1
    )
        THROW 51304, 'Duplicate wallet top-up Gateway/Authority values must be reconciled before V0004.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WalletTopUps') AND name = N'UX_WalletTopUps_Gateway_Authority')
        CREATE UNIQUE INDEX UX_WalletTopUps_Gateway_Authority
            ON dbo.WalletTopUps(Gateway, Authority) WHERE Authority IS NOT NULL;

    IF COL_LENGTH(N'dbo.PaymentCallbacks', N'CallbackKey') IS NULL
        ALTER TABLE dbo.PaymentCallbacks ADD CallbackKey char(64) COLLATE Latin1_General_100_BIN2 NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.PaymentCallbacks
        SET CallbackKey = LOWER(CONVERT(varchar(64), HASHBYTES(''SHA2_256'', CONVERT(varbinary(max), CallbackData)), 2))
        WHERE CallbackKey IS NULL;
        IF EXISTS
        (
            SELECT PaymentId, CallbackKey FROM dbo.PaymentCallbacks WHERE CallbackKey IS NOT NULL
            GROUP BY PaymentId, CallbackKey HAVING COUNT(*) > 1
        ) THROW 51302, ''Duplicate payment callback keys must be reconciled before V0004.'', 1;';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.PaymentCallbacks')
          AND name = N'UX_PaymentCallbacks_PaymentId_CallbackKey'
    )
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_PaymentCallbacks_PaymentId_CallbackKey
            ON dbo.PaymentCallbacks(PaymentId, CallbackKey) WHERE CallbackKey IS NOT NULL;';

    IF EXISTS
    (
        SELECT UserId, ReferenceType, ReferenceId, [Type]
        FROM dbo.WalletTransactions
        WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL
        GROUP BY UserId, ReferenceType, ReferenceId, [Type]
        HAVING COUNT(*) > 1
    )
        THROW 51303, 'Duplicate wallet reference transactions must be reconciled before V0004.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.WalletTransactions')
          AND name = N'UX_WalletTransactions_FinancialReference'
    )
        CREATE UNIQUE INDEX UX_WalletTransactions_FinancialReference
            ON dbo.WalletTransactions(UserId, ReferenceType, ReferenceId, [Type])
            WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL;

    ;WITH duplicateOtp AS
    (
        SELECT Id,
               ROW_NUMBER() OVER (PARTITION BY Mobile, Purpose ORDER BY CreatedAt DESC, Id DESC) AS RowNumber
        FROM dbo.OtpCodes
        WHERE Mobile IS NOT NULL AND ConsumedAt IS NULL
    )
    UPDATE otp
    SET ConsumedAt = SYSUTCDATETIME()
    FROM dbo.OtpCodes otp
    INNER JOIN duplicateOtp duplicateRow ON duplicateRow.Id = otp.Id
    WHERE duplicateRow.RowNumber > 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.OtpCodes')
          AND name = N'UX_OtpCodes_OneActive_Mobile_Purpose'
    )
        CREATE UNIQUE INDEX UX_OtpCodes_OneActive_Mobile_Purpose
            ON dbo.OtpCodes(Mobile, Purpose)
            WHERE Mobile IS NOT NULL AND ConsumedAt IS NULL;

    IF COL_LENGTH(N'dbo.UserVerificationProfiles', N'EncryptedPayload') IS NULL
        ALTER TABLE dbo.UserVerificationProfiles ADD EncryptedPayload nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.UserVerificationProfiles', N'EncryptionVersion') IS NULL
        ALTER TABLE dbo.UserVerificationProfiles ADD EncryptionVersion smallint NULL;

    IF COL_LENGTH(N'dbo.OrderItemDeliveries', N'ContentHash') IS NULL
        ALTER TABLE dbo.OrderItemDeliveries ADD ContentHash char(64) COLLATE Latin1_General_100_BIN2 NULL;
    IF COL_LENGTH(N'dbo.OrderItemDeliveries', N'EncryptionVersion') IS NULL
        ALTER TABLE dbo.OrderItemDeliveries ADD EncryptionVersion smallint NULL;
    IF COL_LENGTH(N'dbo.OrderItemDeliveries', N'ManualDeliveryItemKey') IS NULL
        ALTER TABLE dbo.OrderItemDeliveries ADD ManualDeliveryItemKey uniqueidentifier NULL;

    EXEC sys.sp_executesql N'UPDATE dbo.OrderItemDeliveries
        SET ManualDeliveryItemKey = OrderItemId
        WHERE DeliveryType IN (2, 3) AND ManualDeliveryItemKey IS NULL;';

    IF OBJECT_ID(N'dbo.CK_OrderItemDeliveries_ManualKey', N'C') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.OrderItemDeliveries WITH CHECK
            ADD CONSTRAINT CK_OrderItemDeliveries_ManualKey CHECK
            ((DeliveryType IN (2, 3) AND ManualDeliveryItemKey = OrderItemId) OR
             (DeliveryType NOT IN (2, 3) AND ManualDeliveryItemKey IS NULL));';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.OrderItemDeliveries')
          AND name = N'UX_OrderItemDeliveries_Manual_Item'
    )
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_OrderItemDeliveries_Manual_Item
            ON dbo.OrderItemDeliveries(ManualDeliveryItemKey) WHERE ManualDeliveryItemKey IS NOT NULL;';

    IF COL_LENGTH(N'dbo.OutboxMessages', N'LockedAt') IS NULL
        ALTER TABLE dbo.OutboxMessages ADD LockedAt datetime2(7) NULL;
    IF COL_LENGTH(N'dbo.OutboxMessages', N'LockId') IS NULL
        ALTER TABLE dbo.OutboxMessages ADD LockId uniqueidentifier NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.OutboxMessages')
          AND name = N'IX_OutboxMessages_Status_LockedAt'
    )
        EXEC sys.sp_executesql N'CREATE INDEX IX_OutboxMessages_Status_LockedAt
            ON dbo.OutboxMessages(Status, LockedAt, CreatedAt);';

    IF OBJECT_ID(N'dbo.PaymentRefunds', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PaymentRefunds
        (
            Id uniqueidentifier NOT NULL CONSTRAINT DF_PaymentRefunds_Id DEFAULT NEWSEQUENTIALID(),
            PaymentId uniqueidentifier NOT NULL,
            OrderId uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            Amount decimal(18,2) NOT NULL,
            Method tinyint NOT NULL,
            Status tinyint NOT NULL,
            Reason nvarchar(1000) NOT NULL,
            IdempotencyKey nvarchar(100) NOT NULL,
            RequestedByUserId uniqueidentifier NULL,
            RequestedAt datetime2(7) NOT NULL CONSTRAINT DF_PaymentRefunds_RequestedAt DEFAULT SYSUTCDATETIME(),
            CompletedAt datetime2(7) NULL,
            FailureReason nvarchar(1000) NULL,
            CONSTRAINT PK_PaymentRefunds PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_PaymentRefunds_Payments FOREIGN KEY (PaymentId) REFERENCES dbo.Payments(Id),
            CONSTRAINT FK_PaymentRefunds_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
            CONSTRAINT FK_PaymentRefunds_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_PaymentRefunds_RequestedBy FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users(Id),
            CONSTRAINT CK_PaymentRefunds_Amount CHECK (Amount > 0),
            CONSTRAINT CK_PaymentRefunds_Method CHECK (Method IN (1, 2)),
            CONSTRAINT CK_PaymentRefunds_Status CHECK (Status IN (1, 2, 3, 4))
        );
        CREATE UNIQUE INDEX UX_PaymentRefunds_Payment_Idempotency ON dbo.PaymentRefunds(PaymentId, IdempotencyKey);
        CREATE INDEX IX_PaymentRefunds_Status_RequestedAt ON dbo.PaymentRefunds(Status, RequestedAt);
    END;

    IF OBJECT_ID(N'dbo.FinancialAuditLogs', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FinancialAuditLogs
        (
            Id bigint IDENTITY(1,1) NOT NULL,
            EventType nvarchar(100) NOT NULL,
            EntityType nvarchar(100) NOT NULL,
            EntityId uniqueidentifier NOT NULL,
            UserId uniqueidentifier NULL,
            Amount decimal(18,2) NULL,
            CorrelationId uniqueidentifier NOT NULL,
            Detail nvarchar(2000) NULL,
            CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_FinancialAuditLogs_CreatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_FinancialAuditLogs PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_FinancialAuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
        );
        CREATE INDEX IX_FinancialAuditLogs_Entity ON dbo.FinancialAuditLogs(EntityType, EntityId, CreatedAt DESC);
        CREATE INDEX IX_FinancialAuditLogs_Event ON dbo.FinancialAuditLogs(EventType, CreatedAt DESC);
        CREATE INDEX IX_FinancialAuditLogs_CorrelationId ON dbo.FinancialAuditLogs(CorrelationId);
    END;

    IF OBJECT_ID(N'dbo.Settings', N'U') IS NOT NULL
    BEGIN
        DECLARE @Settings TABLE ([Key] nvarchar(200), [Value] nvarchar(max), GroupName nvarchar(100), ValueType nvarchar(50), Description nvarchar(500));
        INSERT @Settings VALUES
            (N'Security.OtpRetentionDays', N'7', N'Security', N'int', N'مدت نگهداری سوابق کد یکبار مصرف'),
            (N'Security.RefreshTokenRetentionDays', N'30', N'Security', N'int', N'مدت نگهداری توکن‌های منقضی یا لغوشده'),
            (N'Security.AuditRetentionDays', N'730', N'Security', N'int', N'مدت نگهداری رویدادهای ممیزی'),
            (N'Security.KycRejectedRetentionDays', N'90', N'Security', N'int', N'مدت نگهداری مدارک ردشده احراز هویت'),
            (N'Security.OutboxLockTimeoutMinutes', N'5', N'Security', N'int', N'زمان بازیابی پیام Outbox قفل‌شده');

        INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, Description)
        SELECT NEWID(), source.[Key], source.[Value], source.GroupName, source.ValueType, source.Description
        FROM @Settings source
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings target WHERE target.[Key] = source.[Key]);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER TRIGGER dbo.TR_FinancialAuditLogs_Immutable
ON dbo.FinancialAuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51310, 'Financial audit history is immutable.', 1;
END;
GO
