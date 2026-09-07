/* Quantity reservations prevent concurrent unpaid checkouts from overselling counted stock. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ManagedStockReservations', N'U') IS NULL
BEGIN
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
    CREATE INDEX IX_ManagedStockReservations_OrderId ON dbo.ManagedStockReservations(OrderId);
END;
