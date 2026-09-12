/* Whole rial/toman repair. SQL Server 2019+.
   DEFAULT: PREVIEW ONLY. No application data is changed with @Apply = 0.
   Before apply: take a full database backup; stop BOTH Web and API app pools/jobs.
   Then set @Apply=1 and @MaintenanceConfirmed=1 and execute the ENTIRE script.
   Never change paid/refunded/delivered orders or historical failed payment amounts.
   Any order with a bank authority (even a failed one), INITIALIZING attempt,
   wallet/coupon usage, delivery or KYC lifecycle is conservatively skipped.
   No schema migration; before/after money snapshots are retained in AuditLogs.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
DECLARE @Apply bit = 0;
DECLARE @MaintenanceConfirmed bit = 0;
DECLARE @ExpectedDatabase sysname = N'VitorizeDb';
IF DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Wrong database. Select VitorizeDb before running.', 1;
IF @@TRANCOUNT <> 0
    THROW 51001, 'Run in a new query window without an existing transaction.', 1;
IF @Apply = 1 AND @MaintenanceConfirmed = 0
    THROW 51002, 'Back up the database and stop Web/API/jobs before confirming maintenance.', 1;

DROP TABLE IF EXISTS #Products, #Variants, #CartItems, #Orders, #Items;

BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    SELECT Id, Title, BasePrice AS OldPrice, DiscountPrice AS OldDiscount,
        ROUND(BasePrice, 0) AS NewPrice,
        CASE WHEN ROUND(DiscountPrice, 0) > 0 THEN ROUND(DiscountPrice, 0) END AS NewDiscount
    INTO #Products
    FROM dbo.Products
    WHERE IsDeleted = 0 AND CurrencyType IN (1, 2)
      AND (BasePrice <> ROUND(BasePrice, 0) OR DiscountPrice <> ROUND(DiscountPrice, 0));

    SELECT v.Id, v.ProductId, v.Title, v.Price AS OldPrice, v.DiscountPrice AS OldDiscount,
        ROUND(v.Price, 0) AS NewPrice,
        CASE WHEN ROUND(v.DiscountPrice, 0) > 0 THEN ROUND(v.DiscountPrice, 0) END AS NewDiscount
    INTO #Variants
    FROM dbo.ProductVariants v JOIN dbo.Products p ON p.Id = v.ProductId
    WHERE p.IsDeleted = 0 AND p.CurrencyType IN (1, 2)
      AND (v.Price <> ROUND(v.Price, 0) OR v.DiscountPrice <> ROUND(v.DiscountPrice, 0));

    SELECT Id, UnitPrice AS OldPrice, ROUND(UnitPrice, 0) AS NewPrice
    INTO #CartItems FROM dbo.CartItems
    WHERE CurrencyType IN (1, 2) AND UnitPrice <> ROUND(UnitPrice, 0);

    -- Include unpaid orders with fractional lines even if their final total happens to be integral.
    SELECT o.Id, o.OrderNumber, o.SubtotalAmount AS OldSubtotal,
        o.DiscountAmount AS OldDiscount, o.VatTaxableAmount AS OldTaxable,
        o.VatAmount AS OldVat, o.FinalAmount AS OldFinal,
        CAST(NULL AS decimal(18,2)) AS NewSubtotal, CAST(NULL AS decimal(18,2)) AS NewDiscount,
        CAST(NULL AS decimal(18,2)) AS NewTaxable, CAST(NULL AS decimal(18,2)) AS NewVat,
        CAST(NULL AS decimal(18,2)) AS NewFinal,
        CAST(NULL AS nvarchar(200)) AS SkipReason
    INTO #Orders FROM dbo.Orders o
    WHERE o.Status = 1 AND o.PaymentStatus IN (1, 3, 4) AND o.CurrencyType IN (1, 2)
      AND (o.SubtotalAmount <> ROUND(o.SubtotalAmount, 0)
        OR o.DiscountAmount <> ROUND(o.DiscountAmount, 0)
        OR o.VatAmount <> ROUND(o.VatAmount, 0) OR o.FinalAmount <> ROUND(o.FinalAmount, 0)
        OR EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.OrderId = o.Id AND i.UnitPrice <> ROUND(i.UnitPrice, 0)));

    UPDATE t SET SkipReason = N'Bank attempt, paid/refunded history, or payment initializing: reconcile manually'
    FROM #Orders t
    WHERE EXISTS (SELECT 1 FROM dbo.Payments p WHERE p.OrderId=t.Id AND
        (p.Status IN (2,5) OR p.CallbackVerified=1
         OR NULLIF(LTRIM(RTRIM(p.Authority)), N'') IS NOT NULL
         OR NULLIF(LTRIM(RTRIM(p.ReferenceNumber)), N'') IS NOT NULL
         OR NULLIF(LTRIM(RTRIM(p.TransactionId)), N'') IS NOT NULL
         OR NULLIF(LTRIM(RTRIM(p.GatewayTrackingCode)), N'') IS NOT NULL
         OR p.VerifiedAt IS NOT NULL
         OR EXISTS (SELECT 1 FROM dbo.PaymentCallbacks c WHERE c.PaymentId=p.Id)
         OR (p.Status=1 AND ISNULL(p.ProviderStatusCode,N'')<>N'READY')))
      OR EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.Id=t.Id AND (o.PaidAt IS NOT NULL OR o.CompletedAt IS NOT NULL));

    UPDATE t SET SkipReason = COALESCE(SkipReason, N'Financial, delivery or KYC activity already exists: review manually')
    FROM #Orders t
    WHERE EXISTS (SELECT 1 FROM dbo.WalletTransactions w WHERE w.ReferenceId=t.Id)
       OR EXISTS (SELECT 1 FROM dbo.CouponUsages c WHERE c.OrderId=t.Id)
       OR EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.OrderId=t.Id AND
           (i.DeliveredAt IS NOT NULL OR i.DeliveryStatus<>1
            OR EXISTS (SELECT 1 FROM dbo.OrderItemDeliveries d WHERE d.OrderItemId=i.Id)
            OR EXISTS (SELECT 1 FROM dbo.OrderItemKycStates k WHERE k.OrderItemId=i.Id)));

    SELECT i.Id, i.OrderId, i.Quantity, i.UnitPrice AS OldUnitPrice, i.TotalPrice AS OldTotal,
        i.KycEvaluatedAmount AS OldKycEvaluatedAmount,
        ROUND(i.UnitPrice,0) AS NewUnitPrice,
        CAST(ROUND(i.UnitPrice,0)*i.Quantity AS decimal(18,2)) AS NewTotal
    INTO #Items FROM dbo.OrderItems i JOIN #Orders t ON t.Id=i.OrderId;

    UPDATE t SET NewSubtotal=s.Amount
    FROM #Orders t JOIN (SELECT OrderId, SUM(NewTotal) AS Amount FROM #Items GROUP BY OrderId) s ON s.OrderId=t.Id;
    UPDATE t SET NewDiscount=ROUND(CASE WHEN OldDiscount<0 THEN 0
        WHEN OldDiscount>NewSubtotal THEN NewSubtotal ELSE OldDiscount END,0)
    FROM #Orders t;
    UPDATE t SET NewTaxable=CASE WHEN o.VatEnabled=0 THEN 0
        WHEN o.VatCalculationMode=2 THEN t.NewSubtotal-t.NewDiscount ELSE t.NewSubtotal END
    FROM #Orders t JOIN dbo.Orders o ON o.Id=t.Id;
    UPDATE t SET NewVat=CASE WHEN o.VatEnabled=0 THEN 0 ELSE ROUND(t.NewTaxable*o.VatRatePercent/100,0) END
    FROM #Orders t JOIN dbo.Orders o ON o.Id=t.Id;
    UPDATE #Orders SET NewFinal=NewSubtotal-NewDiscount+NewVat;

    UPDATE t SET SkipReason=COALESCE(t.SkipReason,N'Invalid totals, unit price, currency or VAT snapshot: review manually')
    FROM #Orders t JOIN dbo.Orders o ON o.Id=t.Id
    WHERE t.NewSubtotal IS NULL OR t.NewSubtotal-t.NewDiscount<=0 OR t.NewFinal<=0
       OR o.VatRatePercent<0 OR o.VatRatePercent>100
       OR (o.VatEnabled=1 AND o.VatCalculationMode NOT IN (1,2))
       OR EXISTS (SELECT 1 FROM #Items i JOIN dbo.OrderItems source ON source.Id=i.Id
                  WHERE i.OrderId=t.Id AND (i.Quantity<=0 OR i.NewUnitPrice<=0 OR source.CurrencyType<>o.CurrencyType));

    -- Do not automatically change eligibility when rounding crosses an existing KYC threshold.
    UPDATE t SET SkipReason=COALESCE(t.SkipReason,N'KYC threshold crossing: review manually')
    FROM #Orders t WHERE EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.OrderId=t.Id
        AND i.KycThresholdAmount IS NOT NULL AND
        ((t.OldFinal<i.KycThresholdAmount AND t.NewFinal>=i.KycThresholdAmount)
         OR (t.OldFinal>=i.KycThresholdAmount AND t.NewFinal<i.KycThresholdAmount)));

    SELECT N'Product prices' AS ResultSet, * FROM #Products;
    SELECT N'Variant prices' AS ResultSet, * FROM #Variants;
    SELECT N'Unpaid order plan' AS ResultSet, * FROM #Orders ORDER BY OrderNumber;
    SELECT N'Cart items' AS ResultSet, COUNT(*) AS RowsToNormalize FROM #CartItems;
    IF @Apply=0
    BEGIN
        ROLLBACK;
        SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
        SET LOCK_TIMEOUT -1;
        PRINT 'PREVIEW ONLY. Nothing changed. Review SkipReason before applying.';
        RETURN;
    END;

    IF EXISTS (SELECT 1 FROM #Products WHERE NewPrice<=0 AND OldPrice>0)
       OR EXISTS (SELECT 1 FROM #Variants WHERE NewPrice<=0 AND OldPrice>0)
       OR EXISTS (SELECT 1 FROM #CartItems WHERE NewPrice<=0 AND OldPrice>0)
        THROW 51003, 'A positive price would round to zero. Correct that price manually first.', 1;

    DECLARE @AuditId uniqueidentifier=NEWID(), @Now datetime2=SYSUTCDATETIME();
    -- Snapshot BEFORE updates; no card numbers, tokens, personal data or document contents.
    INSERT dbo.AuditLogs(Id, UserId, ActionType, EntityName, EntityId, Data, CreatedAt)
    SELECT @AuditId, NULL, N'WholeCurrencyRepair', N'Money', N'20260911',
        (SELECT JSON_QUERY((SELECT * FROM #Products FOR JSON PATH)) AS Products,
                JSON_QUERY((SELECT * FROM #Variants FOR JSON PATH)) AS Variants,
                JSON_QUERY((SELECT * FROM #CartItems FOR JSON PATH)) AS CartItems,
                JSON_QUERY((SELECT * FROM #Orders WHERE SkipReason IS NULL FOR JSON PATH)) AS Orders,
                JSON_QUERY((SELECT i.* FROM #Items i JOIN #Orders t ON t.Id=i.OrderId WHERE t.SkipReason IS NULL FOR JSON PATH)) AS OrderItems,
                JSON_QUERY((SELECT p.Id,p.OrderId,p.Status,p.ProviderStatusCode,p.UpdatedAt
                    FROM dbo.Payments p JOIN #Orders t ON t.Id=p.OrderId
                    WHERE t.SkipReason IS NULL AND p.Status=1 AND p.ProviderStatusCode=N'READY' FOR JSON PATH)) AS ReadyPayments
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @Now;

    UPDATE p SET BasePrice=t.NewPrice, DiscountPrice=t.NewDiscount, UpdatedAt=@Now
    FROM dbo.Products p JOIN #Products t ON t.Id=p.Id;
    UPDATE v SET Price=t.NewPrice, DiscountPrice=t.NewDiscount, UpdatedAt=@Now
    FROM dbo.ProductVariants v JOIN #Variants t ON t.Id=v.Id;
    UPDATE c SET UnitPrice=t.NewPrice, UpdatedAt=@Now FROM dbo.CartItems c JOIN #CartItems t ON t.Id=c.Id;
    UPDATE i SET UnitPrice=t.NewUnitPrice, TotalPrice=t.NewTotal, KycEvaluatedAmount=o.NewFinal
    FROM dbo.OrderItems i JOIN #Items t ON t.Id=i.Id JOIN #Orders o ON o.Id=i.OrderId
    WHERE o.SkipReason IS NULL;
    UPDATE o SET SubtotalAmount=t.NewSubtotal, DiscountAmount=t.NewDiscount,
        VatTaxableAmount=t.NewTaxable, VatAmount=t.NewVat, FinalAmount=t.NewFinal, UpdatedAt=@Now
    FROM dbo.Orders o JOIN #Orders t ON t.Id=o.Id WHERE t.SkipReason IS NULL;
    -- Preserve every original payment amount; the next retry creates a fresh matching attempt.
    UPDATE p SET Status=3, ProviderStatusCode=N'REPRICED_WHOLE_CURRENCY', UpdatedAt=@Now
    FROM dbo.Payments p JOIN #Orders t ON t.Id=p.OrderId
    WHERE t.SkipReason IS NULL AND p.Status=1 AND p.ProviderStatusCode=N'READY';

    IF EXISTS (SELECT 1 FROM dbo.Orders o JOIN #Orders t ON t.Id=o.Id
        WHERE t.SkipReason IS NULL AND (o.FinalAmount<>ROUND(o.FinalAmount,0)
          OR o.FinalAmount<>o.SubtotalAmount-o.DiscountAmount+o.VatAmount
          OR o.SubtotalAmount<>(SELECT SUM(i.TotalPrice) FROM dbo.OrderItems i WHERE i.OrderId=o.Id)))
        THROW 51004, 'Reconciliation check failed. All changes rolled back.', 1;

    COMMIT;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    SET LOCK_TIMEOUT -1;
    SELECT @AuditId AS RepairAuditId,
        (SELECT COUNT(*) FROM #Products) AS ProductsChanged,
        (SELECT COUNT(*) FROM #Variants) AS VariantsChanged,
        (SELECT COUNT(*) FROM #Orders WHERE SkipReason IS NULL) AS OrdersChanged,
        (SELECT COUNT(*) FROM #Orders WHERE SkipReason IS NOT NULL) AS OrdersSkipped;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    SET LOCK_TIMEOUT -1;
    THROW;
END CATCH;
