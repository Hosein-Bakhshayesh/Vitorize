/*
  Only OTP uses an Asanak template. Transactional notifications are sent as
  application-owned text through the SMS outbox, so notification template
  settings must not remain editable or affect delivery.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
    THROW 51036, N'dbo.Settings is required before V0036 can run.', 1;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN
(
    N'Sms.NotificationTemplateId',
    N'Sms.OrderPaidTemplateId',
    N'Sms.OrderCompletedTemplateId',
    N'Sms.OrderStatusChangedTemplateId',
    N'Sms.GiftCodeDeliveredTemplateId',
    N'Sms.TicketReplyTemplateId',
    N'Sms.VerificationApprovedTemplateId',
    N'Sms.VerificationRejectedTemplateId',
    N'Sms.WalletTopUpSuccessTemplateId'
);

COMMIT TRANSACTION;
