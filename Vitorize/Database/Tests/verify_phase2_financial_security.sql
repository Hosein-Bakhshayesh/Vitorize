SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.PaymentRefunds', N'U') IS NULL THROW 51400, 'PaymentRefunds is missing.', 1;
IF OBJECT_ID(N'dbo.FinancialAuditLogs', N'U') IS NULL THROW 51401, 'FinancialAuditLogs is missing.', 1;
IF COL_LENGTH(N'dbo.PaymentCallbacks', N'CallbackKey') IS NULL THROW 51402, 'CallbackKey is missing.', 1;
IF COL_LENGTH(N'dbo.UserVerificationProfiles', N'EncryptedPayload') IS NULL THROW 51403, 'KYC protection column is missing.', 1;
IF COL_LENGTH(N'dbo.OrderItemDeliveries', N'EncryptionVersion') IS NULL THROW 51404, 'Delivery encryption marker is missing.', 1;
IF COL_LENGTH(N'dbo.OutboxMessages', N'LockedAt') IS NULL THROW 51405, 'Outbox lease column is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments') AND name = N'UX_Payments_Gateway_Authority' AND is_unique = 1)
    THROW 51406, 'Unique payment authority index is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PaymentCallbacks') AND name = N'UX_PaymentCallbacks_PaymentId_CallbackKey' AND is_unique = 1)
    THROW 51407, 'Unique callback index is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WalletTransactions') AND name = N'UX_WalletTransactions_FinancialReference' AND is_unique = 1)
    THROW 51408, 'Wallet idempotency index is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OtpCodes') AND name = N'UX_OtpCodes_OneActive_Mobile_Purpose' AND is_unique = 1)
    THROW 51409, 'OTP active-code uniqueness index is missing.', 1;
IF OBJECT_ID(N'dbo.TR_FinancialAuditLogs_Immutable', N'TR') IS NULL
    THROW 51410, 'Immutable financial audit trigger is missing.', 1;

SELECT N'Phase 2 financial/security schema verification passed.' AS Result;
