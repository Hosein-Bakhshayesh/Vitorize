-- ============================================================================
-- Vitorize — OtpCodes.Purpose CHECK constraint for OTP-login (2026-07-13)
-- SCHEMA (constraint only). Idempotent — safe to run repeatedly. Non-destructive.
--
-- A new OTP purpose value was added: OtpPurpose.Login = 4 (alongside
-- MobileVerification=1, ForgotPassword=2, TwoFactorAuthentication=3).
--
-- The EF model defines NO CHECK constraint on OtpCodes.Purpose, so in a stock
-- database this script is a NO-OP. It exists defensively: if your production
-- database has a CHECK constraint that restricts Purpose to 1..3, this recreates
-- it to allow 1..4 so login OTPs can be stored. If no such constraint exists,
-- nothing is changed.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @constraintName sysname;

    -- Find any CHECK constraint defined on OtpCodes.Purpose.
    SELECT TOP (1) @constraintName = cc.name
    FROM sys.check_constraints cc
    INNER JOIN sys.columns c
        ON c.object_id = cc.parent_object_id
       AND c.column_id = cc.parent_column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.OtpCodes')
      AND c.name = N'Purpose';

    IF @constraintName IS NOT NULL
    BEGIN
        -- Only act if value 4 is not yet permitted (best-effort text probe).
        DECLARE @definition nvarchar(max);
        SELECT @definition = definition
        FROM sys.check_constraints
        WHERE name = @constraintName;

        IF @definition NOT LIKE N'%4%'
        BEGIN
            EXEC(N'ALTER TABLE dbo.OtpCodes DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';');

            ALTER TABLE dbo.OtpCodes WITH CHECK
                ADD CONSTRAINT CK_OtpCodes_Purpose CHECK ([Purpose] IN (1, 2, 3, 4));

            PRINT 'OtpCodes.Purpose CHECK constraint recreated to allow value 4 (Login).';
        END
        ELSE
            PRINT 'OtpCodes.Purpose CHECK constraint already allows value 4. No change.';
    END
    ELSE
        PRINT 'No CHECK constraint on OtpCodes.Purpose. No schema change required.';

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;
