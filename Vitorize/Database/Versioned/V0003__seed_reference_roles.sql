/*
    Idempotent non-secret reference seed. Existing roles are preserved and no
    users, passwords or role assignments are created.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51020, 'dbo.Roles must exist before V0003 can run.', 1;

    DECLARE @Roles TABLE
    (
        Name nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(150) NOT NULL
    );

    INSERT @Roles (Name, DisplayName)
    VALUES
        (N'SuperAdmin', N'مدیر کل'),
        (N'Admin', N'مدیر فروشگاه'),
        (N'Support', N'پشتیبان'),
        (N'Customer', N'مشتری');

    INSERT dbo.Roles (Id, Name, DisplayName, CreatedAt)
    SELECT NEWID(), source.Name, source.DisplayName, SYSUTCDATETIME()
    FROM @Roles source
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Roles existing WHERE existing.Name = source.Name
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

