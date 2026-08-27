/* Persist only the provider-masked PAN returned by Zarinpal Verify (never a full card number). */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Payments', N'MaskedCardPan') IS NULL
    ALTER TABLE dbo.Payments ADD MaskedCardPan nvarchar(32) NULL;

COMMIT TRANSACTION;
