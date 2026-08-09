/*
    Corrective replacement for the historical 2026-07-07 constraint patch.
    It validates data first, executes atomically and leaves one trusted canonical
    constraint matching GiftCodeReservationStatus values 0..3.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.GiftCodeReservations', N'U') IS NULL OR
       COL_LENGTH(N'dbo.GiftCodeReservations', N'Status') IS NULL
        THROW 51010, 'dbo.GiftCodeReservations.Status is required before V0002 can run.', 1;

    IF EXISTS (SELECT 1 FROM dbo.GiftCodeReservations WHERE [Status] NOT BETWEEN 0 AND 3)
        THROW 51011, 'GiftCodeReservations contains Status values outside 0..3. Correct the data before deployment.', 1;

    DECLARE @ObjectId int = OBJECT_ID(N'dbo.GiftCodeReservations');
    DECLARE @StatusColumnId int = COLUMNPROPERTY(@ObjectId, N'Status', 'ColumnId');

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = @ObjectId
          AND cc.name <> N'CK_GiftCodeReservations_Status'
          AND
          (
              cc.parent_column_id = @StatusColumnId OR
              EXISTS
              (
                  SELECT 1
                  FROM sys.sql_expression_dependencies dependency
                  WHERE dependency.referencing_id = cc.object_id
                    AND dependency.referenced_id = @ObjectId
                    AND dependency.referenced_minor_id = @StatusColumnId
              )
          )
    )
        THROW 51012, 'A non-canonical CHECK constraint also targets GiftCodeReservations.Status. Review it before deployment.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = @ObjectId
          AND name = N'CK_GiftCodeReservations_Status'
    )
        ALTER TABLE dbo.GiftCodeReservations
            DROP CONSTRAINT CK_GiftCodeReservations_Status;

    ALTER TABLE dbo.GiftCodeReservations WITH CHECK
        ADD CONSTRAINT CK_GiftCodeReservations_Status
        CHECK ([Status] >= 0 AND [Status] <= 3);

    ALTER TABLE dbo.GiftCodeReservations
        CHECK CONSTRAINT CK_GiftCodeReservations_Status;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

