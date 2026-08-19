SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderItemKycFinanceResolutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItemKycFinanceResolutions
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_OrderItemKycFinanceResolutions_Id DEFAULT NEWSEQUENTIALID(),
        OrderItemId uniqueidentifier NOT NULL,
        Status tinyint NOT NULL,
        Reason nvarchar(1000) NULL,
        ExternalReference nvarchar(200) NULL,
        ResolvedByUserId uniqueidentifier NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_OrderItemKycFinanceResolutions_CreatedAt DEFAULT SYSUTCDATETIME(),
        ResolvedAt datetime2(7) NULL,
        CONSTRAINT PK_OrderItemKycFinanceResolutions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OrderItemKycFinanceResolutions_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(Id),
        CONSTRAINT FK_OrderItemKycFinanceResolutions_ResolvedBy FOREIGN KEY (ResolvedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_OrderItemKycFinanceResolutions_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT UX_OrderItemKycFinanceResolutions_OrderItem UNIQUE (OrderItemId)
    );
    CREATE INDEX IX_OrderItemKycFinanceResolutions_Status_CreatedAt ON dbo.OrderItemKycFinanceResolutions(Status, CreatedAt);
END

COMMIT TRANSACTION;
