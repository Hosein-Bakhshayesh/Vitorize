/*
  Vitorize standalone production upgrade: V0037 -> V0039

  Run this file in SSMS after selecting the Vitorize production database.
  No USE statement, SQLCMD mode, external file, or parameter is required.

  Applies:
    V0038: reconciles paid, verified KYC order items that were historically
           left in AwaitingSubmission, only after validating their evidence.
    V0039: creates managed-stock payment reservations, preventing concurrent
           unpaid checkouts from selling more counted stock than is available.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
        THROW 51370, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

    IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
        THROW 51371, N'جدول تاریخچه مهاجرت وجود ندارد؛ این فایل فقط برای دیتابیس canonical نسخه V0037 قابل اجرا است.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0037'
          AND ScriptName = N'V0037__remove_asanak_otp_template_settings.sql'
          AND ScriptHash = 'dcbe240f2c3f2ad2bcf18a6d5e475e3e4e7ad51d9ebfc5b60b66a15b9feebc2e'
          AND Success = 1
    )
        THROW 51372, N'این دیتابیس در وضعیت canonical نسخه V0037 نیست. اسکریپت اجرا نشد و تغییری اعمال نشد.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0038'
          AND (ScriptName <> N'V0038__reconcile_verified_kyc_order_items.sql'
               OR ScriptHash <> '863ab9d5613c97ac3b605825dcdfda8178f47f2675a3541369524b675968b9c6'
               OR Success <> 1)
    )
        THROW 51373, N'رکورد V0038 در تاریخچهٔ دیتابیس با نسخهٔ مورد انتظار سازگار نیست. تغییری اعمال نشد.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.DatabaseScriptHistory
        WHERE ScriptVersion = N'V0039'
          AND (ScriptName <> N'V0039__managed_stock_payment_reservations.sql'
               OR ScriptHash <> '7e63f5a390573c21963a7922553f90bbcc4b9ddf280bbb47da041cd845dcbdb4'
               OR Success <> 1)
    )
        THROW 51374, N'رکورد V0039 در تاریخچهٔ دیتابیس با نسخهٔ مورد انتظار سازگار نیست. تغییری اعمال نشد.', 1;

    IF OBJECT_ID(N'dbo.ManagedStockReservations', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptVersion = N'V0039' AND Success = 1)
        THROW 51375, N'جدول ManagedStockReservations بدون ثبت V0039 وجود دارد. برای جلوگیری از تغییر نامطمئن، اسکریپت اجرا نشد.', 1;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptVersion = N'V0038' AND Success = 1)
    BEGIN
        IF OBJECT_ID(N'dbo.OrderItemKycStates', N'U') IS NULL
           OR OBJECT_ID(N'dbo.UserVerificationProfiles', N'U') IS NULL
           OR OBJECT_ID(N'dbo.VerificationDocuments', N'U') IS NULL
           OR OBJECT_ID(N'dbo.KycPolicyDocumentRequirements', N'U') IS NULL
            THROW 51376, N'جداول چرخهٔ احراز هویت برای اجرای V0038 وجود ندارند.', 1;

        DECLARE @now datetime2 = SYSUTCDATETIME();

        UPDATE state
        SET state.Status = 2,
            state.UpdatedAt = @now,
            state.CustomerActionDeadlineAt = NULL,
            state.SatisfiedAt = @now,
            state.SatisfiedByVerificationProfileId = profile.Id
        FROM dbo.OrderItemKycStates AS state
        JOIN dbo.OrderItems AS item ON item.Id = state.OrderItemId
        JOIN dbo.Orders AS [order] ON [order].Id = item.OrderId
        JOIN dbo.Users AS [user] ON [user].Id = [order].UserId
        JOIN dbo.UserVerificationProfiles AS profile ON profile.UserId = [user].Id
        WHERE state.Status = 3
          AND item.RequiresVerification = 1
          AND item.KycPolicyVersionId IS NOT NULL
          AND [order].PaymentStatus = 2
          AND [user].VerificationStatus = 1
          AND profile.Status = 1
          AND profile.SubmittedAt IS NOT NULL
          AND NULLIF(LTRIM(RTRIM(profile.EncryptedPayload)), N'') IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.KycPolicyDocumentRequirements AS requirement
              WHERE requirement.KycPolicyVersionId = item.KycPolicyVersionId
                AND requirement.IsRequired = 1
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.VerificationDocuments AS document
                    WHERE document.UserVerificationProfileId = profile.Id
                      AND document.KycDocumentTypeId = requirement.KycDocumentTypeId
                      AND document.Status IN (0, 1)
                      AND NULLIF(LTRIM(RTRIM(document.FilePath)), N'') IS NOT NULL
                )
          );

        INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
        VALUES
        (
            N'V0038__reconcile_verified_kyc_order_items.sql',
            N'V0038',
            '863ab9d5613c97ac3b605825dcdfda8178f47f2675a3541369524b675968b9c6',
            N'Production',
            1,
            N'Canonical deployment chain'
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptVersion = N'V0039' AND Success = 1)
    BEGIN
        IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
           OR OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
           OR OBJECT_ID(N'dbo.ProductVariants', N'U') IS NULL
            THROW 51377, N'جداول سفارش و واریانت محصول برای اجرای V0039 وجود ندارند.', 1;

        CREATE TABLE dbo.ManagedStockReservations
        (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagedStockReservations PRIMARY KEY,
            OrderId uniqueidentifier NOT NULL,
            OrderItemId uniqueidentifier NOT NULL,
            ProductVariantId uniqueidentifier NOT NULL,
            Quantity int NOT NULL,
            Status tinyint NOT NULL,
            ReservedAt datetime2 NOT NULL,
            ExpiresAt datetime2 NOT NULL,
            ReleasedAt datetime2 NULL,
            ConsumedAt datetime2 NULL,
            CONSTRAINT CK_ManagedStockReservations_Quantity CHECK (Quantity > 0),
            CONSTRAINT CK_ManagedStockReservations_Status CHECK (Status IN (1,2,3,4)),
            CONSTRAINT FK_ManagedStockReservations_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
            CONSTRAINT FK_ManagedStockReservations_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(Id),
            CONSTRAINT FK_ManagedStockReservations_ProductVariants FOREIGN KEY (ProductVariantId) REFERENCES dbo.ProductVariants(Id),
            CONSTRAINT UX_ManagedStockReservations_OrderItemId UNIQUE (OrderItemId)
        );

        CREATE INDEX IX_ManagedStockReservations_Variant_Status_ExpiresAt
            ON dbo.ManagedStockReservations(ProductVariantId, Status, ExpiresAt);

        CREATE INDEX IX_ManagedStockReservations_OrderId
            ON dbo.ManagedStockReservations(OrderId);

        INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
        VALUES
        (
            N'V0039__managed_stock_payment_reservations.sql',
            N'V0039',
            '7e63f5a390573c21963a7922553f90bbcc4b9ddf280bbb47da041cd845dcbdb4',
            N'Production',
            1,
            N'Canonical deployment chain'
        );
    END;

    COMMIT TRANSACTION;

    SELECT
        N'ارتقای Vitorize از V0037 به V0039 با موفقیت انجام شد.' AS Result,
        DB_NAME() AS DatabaseName,
        SYSUTCDATETIME() AS CompletedAtUtc;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
