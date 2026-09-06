/*
  Asanak template delivery is no longer used. OTP codes are sent as application-owned
  plain text, so template identifiers must not remain configurable.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
    THROW 51037, N'dbo.Settings is required before V0037 can run.', 1;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'Sms.OtpTemplateId',
    N'Sms.LoginOtpTemplateId',
    N'Sms.RegisterOtpTemplateId',
    N'Sms.ForgotPasswordTemplateId'
);

COMMIT TRANSACTION;
