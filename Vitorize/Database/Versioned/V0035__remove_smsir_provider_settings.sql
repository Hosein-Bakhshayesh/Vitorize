/*
  Asanak is the sole SMS transport. Remove legacy SMS.ir connection settings
  after V0034 has provisioned the Asanak credentials. SMS history and outbox
  records are intentionally preserved because they are operational audit data.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
    THROW 51035, N'dbo.Settings is required before V0035 can run.', 1;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'Sms.Provider',
    N'Sms.ApiKey',
    N'Sms.DefaultLineNumber',
    N'Sms.SenderName'
);

COMMIT TRANSACTION;
