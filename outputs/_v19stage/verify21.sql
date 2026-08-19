SET NOCOUNT ON;
DECLARE @DefTitle nvarchar(50) = N'پیش' + NCHAR(8204) + N'فرض';
SELECT p.Slug, p.DeliveryType, VariantCount = COUNT(v.Id),
       DefaultCount = SUM(CASE WHEN v.IsDefault = 1 THEN 1 ELSE 0 END),
       ImplicitCount = SUM(CASE WHEN v.Title = @DefTitle THEN 1 ELSE 0 END),
       Qty = SUM(ISNULL(v.StockQuantity,0)),
       Modes = STRING_AGG(CONVERT(varchar(4), v.StockMode), ',')
FROM dbo.Products p LEFT JOIN dbo.ProductVariants v ON v.ProductId = p.Id
WHERE p.Slug LIKE 'up-%' GROUP BY p.Slug, p.DeliveryType ORDER BY p.Slug;
SELECT HistoricalOrders = (SELECT COUNT(*) FROM dbo.Orders WHERE OrderNumber = 'VT-UP-1'),
       HistoricalItems   = (SELECT COUNT(*) FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.Id=oi.OrderId WHERE o.OrderNumber='VT-UP-1'),
       NullVariantItems  = (SELECT COUNT(*) FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.Id=oi.OrderId WHERE o.OrderNumber='VT-UP-1' AND oi.ProductVariantId IS NULL),
       GiftCodes         = (SELECT COUNT(*) FROM dbo.GiftCodes),
       DupDefaults       = (SELECT COUNT(*) FROM (SELECT ProductId FROM dbo.ProductVariants WHERE IsDefault=1 GROUP BY ProductId HAVING COUNT(*)>1) z),
       VariantlessNonInstant = (SELECT COUNT(*) FROM dbo.Products p WHERE p.DeliveryType<>1 AND NOT EXISTS(SELECT 1 FROM dbo.ProductVariants v WHERE v.ProductId=p.Id));
SELECT LedgerCount = COUNT(*), MaxVersion = MAX(ScriptVersion) FROM dbo.DatabaseScriptHistory;
