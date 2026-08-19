/* ============================================================================
   Vitorize — schema fix (Database-First, no EF migrations)
   Date: 2026-07-07

   Issue:
     The CHECK constraint CK_GiftCodeReservations_Status enforces
         [Status] >= 1 AND [Status] <= 4
     but the application enum GiftCodeReservationStatus is:
         Released = 0, Active = 1, Sold = 2, Expired = 3
     Releasing a reservation writes Status = 0 (Released), which violates the
     constraint and throws a SqlException, breaking reservation release /
     release-expired flows.

   Fix:
     Replace the constraint so it matches the enum's valid range (0..3).

   Safe to run once. Idempotent.
   ============================================================================ */

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_GiftCodeReservations_Status')
    ALTER TABLE dbo.GiftCodeReservations DROP CONSTRAINT CK_GiftCodeReservations_Status;
GO

ALTER TABLE dbo.GiftCodeReservations WITH CHECK
    ADD CONSTRAINT CK_GiftCodeReservations_Status
    CHECK ([Status] >= 0 AND [Status] <= 3);
GO
