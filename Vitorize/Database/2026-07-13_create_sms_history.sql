-- Vitorize SMS history/audit storage (Database-First, idempotent)
-- Execution order:
--   1) Run this script.
--   2) Run 2026-07-13_seed_sms_settings.sql.
-- No EF Core migration is used.

SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SmsMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsMessages
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_SmsMessages_Id DEFAULT NEWSEQUENTIALID(),
        UserId uniqueidentifier NULL,
        Mobile nvarchar(20) NOT NULL,
        MaskedMobile nvarchar(20) NOT NULL,
        Purpose nvarchar(100) NOT NULL,
        SendType tinyint NOT NULL,
        TemplateKey nvarchar(100) NULL,
        TemplateId int NULL,
        PublicReference nvarchar(150) NULL,
        SafeMessagePreview nvarchar(1000) NULL,
        InternalNote nvarchar(500) NULL,
        Provider nvarchar(50) NOT NULL,
        ProviderMessageId nvarchar(200) NULL,
        ProviderErrorCode nvarchar(100) NULL,
        ProviderErrorMessage nvarchar(1000) NULL,
        DeliveryCost decimal(18,2) NULL,
        Status tinyint NOT NULL CONSTRAINT DF_SmsMessages_Status DEFAULT (0),
        RetryCount int NOT NULL CONSTRAINT DF_SmsMessages_RetryCount DEFAULT (0),
        MaxRetryCount int NOT NULL CONSTRAINT DF_SmsMessages_MaxRetryCount DEFAULT (5),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_SmsMessages_CreatedAt DEFAULT SYSUTCDATETIME(),
        LastAttemptAt datetime2(7) NULL,
        SentAt datetime2(7) NULL,
        FailedAt datetime2(7) NULL,
        NextRetryAt datetime2(7) NULL,
        CreatedByUserId uniqueidentifier NULL,
        RelatedEntityType nvarchar(100) NULL,
        RelatedEntityId uniqueidentifier NULL,
        RelatedEntityReference nvarchar(150) NULL,
        IdempotencyKey nvarchar(200) NOT NULL,
        CorrelationId uniqueidentifier NOT NULL CONSTRAINT DF_SmsMessages_CorrelationId DEFAULT NEWID(),
        OutboxMessageId uniqueidentifier NULL,
        CONSTRAINT PK_SmsMessages PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_SmsMessages_SendType CHECK (SendType IN (1,2,3)),
        CONSTRAINT CK_SmsMessages_Status CHECK (Status BETWEEN 0 AND 7),
        CONSTRAINT CK_SmsMessages_RetryCount CHECK (RetryCount >= 0 AND MaxRetryCount BETWEEN 1 AND 10),
        CONSTRAINT FK_SmsMessages_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_SmsMessages_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_SmsMessages_Outbox FOREIGN KEY (OutboxMessageId) REFERENCES dbo.OutboxMessages(Id)
    );
END;

IF COL_LENGTH(N'dbo.SmsMessages', N'InternalNote') IS NULL
    ALTER TABLE dbo.SmsMessages ADD InternalNote nvarchar(500) NULL;

IF OBJECT_ID(N'dbo.SmsMessageAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsMessageAttempts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_SmsMessageAttempts_Id DEFAULT NEWSEQUENTIALID(),
        SmsMessageId uniqueidentifier NOT NULL,
        AttemptNumber int NOT NULL,
        Status tinyint NOT NULL,
        ProviderMessageId nvarchar(200) NULL,
        ProviderErrorCode nvarchar(100) NULL,
        ProviderErrorMessage nvarchar(1000) NULL,
        DeliveryCost decimal(18,2) NULL,
        AttemptedAt datetime2(7) NOT NULL CONSTRAINT DF_SmsMessageAttempts_AttemptedAt DEFAULT SYSUTCDATETIME(),
        CompletedAt datetime2(7) NULL,
        CONSTRAINT PK_SmsMessageAttempts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_SmsMessageAttempts_Status CHECK (Status BETWEEN 0 AND 7),
        CONSTRAINT CK_SmsMessageAttempts_Number CHECK (AttemptNumber > 0),
        CONSTRAINT FK_SmsMessageAttempts_SmsMessage FOREIGN KEY (SmsMessageId)
            REFERENCES dbo.SmsMessages(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'UX_SmsMessages_IdempotencyKey')
    CREATE UNIQUE INDEX UX_SmsMessages_IdempotencyKey ON dbo.SmsMessages(IdempotencyKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'IX_SmsMessages_Status_CreatedAt')
    CREATE INDEX IX_SmsMessages_Status_CreatedAt ON dbo.SmsMessages(Status, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'IX_SmsMessages_SendType_CreatedAt')
    CREATE INDEX IX_SmsMessages_SendType_CreatedAt ON dbo.SmsMessages(SendType, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'IX_SmsMessages_Mobile')
    CREATE INDEX IX_SmsMessages_Mobile ON dbo.SmsMessages(Mobile, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'IX_SmsMessages_PublicReference')
    CREATE INDEX IX_SmsMessages_PublicReference ON dbo.SmsMessages(PublicReference) WHERE PublicReference IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessages') AND name = N'IX_SmsMessages_OutboxMessageId')
    CREATE INDEX IX_SmsMessages_OutboxMessageId ON dbo.SmsMessages(OutboxMessageId) WHERE OutboxMessageId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SmsMessageAttempts') AND name = N'UX_SmsMessageAttempts_Message_Attempt')
    CREATE UNIQUE INDEX UX_SmsMessageAttempts_Message_Attempt ON dbo.SmsMessageAttempts(SmsMessageId, AttemptNumber);

GO

-- Safeguarded retention procedure. It never removes active messages. @BeforeUtc
-- must be supplied explicitly by a privileged operator/job after reviewing retention policy.
CREATE OR ALTER PROCEDURE dbo.usp_PurgeSmsHistory
    @BeforeUtc datetime2(7),
    @BatchSize int = 1000
AS
BEGIN
    SET NOCOUNT ON;
    IF @BeforeUtc IS NULL OR @BatchSize NOT BETWEEN 1 AND 5000
        THROW 50001, 'A valid cutoff and batch size are required.', 1;

    DELETE TOP (@BatchSize)
    FROM dbo.SmsMessages
    WHERE CreatedAt < @BeforeUtc
      AND Status IN (2,3,5,6,7);

    SELECT @@ROWCOUNT AS DeletedRows;
END;

GO

PRINT 'SMS history schema is ready.';
