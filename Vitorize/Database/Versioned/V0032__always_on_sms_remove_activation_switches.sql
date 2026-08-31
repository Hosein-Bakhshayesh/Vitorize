/*
  SMS delivery is always on when SMS.ir has the required technical configuration.
  This removes obsolete on/off switches only; no SMS history, templates, API key,
  sender line, or other customer data is changed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'SmsEnabled',
    N'Sms.IsEnabled',
    N'Sms.CustomSendEnabled',
    N'Sms.CustomTextEnabled',
    N'Sms.UseOutbox',
    N'Sms.RequireConfirmation',
    N'Sms.AllowImmediateSend',
    N'Sms.AllowRetryFailed'
);

COMMIT TRANSACTION;
