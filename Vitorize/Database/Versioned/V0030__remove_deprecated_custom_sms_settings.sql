/*
  The custom-SMS permissions are no longer configurable: custom text and
  sending are always available to authorized administrators.  Their rows were
  part of the immutable 2026-07-13 seed, therefore retirement happens only in
  this forward migration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE FROM dbo.Settings
WHERE [Key] IN (N'Sms.CustomSendEnabled', N'Sms.CustomTextEnabled');

COMMIT TRANSACTION;
