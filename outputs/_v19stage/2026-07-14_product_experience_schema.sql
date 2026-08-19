/* Vitorize product experience schema — SQL Server, Database-First, idempotent.
   Execute before deploying the matching API/Web binaries. No destructive statements. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.CartItems', 'InputFingerprint') IS NULL
BEGIN
    ALTER TABLE dbo.CartItems ADD InputFingerprint varchar(64) NOT NULL
        CONSTRAINT DF_CartItems_InputFingerprint DEFAULT ('NONE') WITH VALUES;
END;

IF OBJECT_ID('dbo.ProductFeatures', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductFeatures
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ProductFeatures_Id DEFAULT (newsequentialid()),
        ProductId uniqueidentifier NOT NULL,
        Title nvarchar(120) NOT NULL,
        Value nvarchar(500) NOT NULL,
        IconKey varchar(64) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ProductFeatures_SortOrder DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_ProductFeatures_IsActive DEFAULT (1),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ProductFeatures_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2(7) NULL,
        CONSTRAINT PK_ProductFeatures PRIMARY KEY (Id),
        CONSTRAINT FK_ProductFeatures_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ProductFeatures_Title_NotBlank CHECK (LEN(LTRIM(RTRIM(Title))) > 0),
        CONSTRAINT CK_ProductFeatures_Value_NotBlank CHECK (LEN(LTRIM(RTRIM(Value))) > 0)
    );
END;

IF OBJECT_ID('dbo.ProductInputFields', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductInputFields
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ProductInputFields_Id DEFAULT (newsequentialid()),
        ProductId uniqueidentifier NOT NULL,
        [Key] varchar(64) NOT NULL,
        Label nvarchar(120) NOT NULL,
        Description nvarchar(500) NULL,
        Placeholder nvarchar(200) NULL,
        FieldType tinyint NOT NULL,
        IsRequired bit NOT NULL CONSTRAINT DF_ProductInputFields_IsRequired DEFAULT (0),
        OptionsJson nvarchar(max) NULL,
        DefaultValue nvarchar(2000) NULL,
        MinLength int NULL,
        MaxLength int NULL,
        ValidationPattern nvarchar(200) NULL,
        ValidationMessage nvarchar(300) NULL,
        IsSensitive bit NOT NULL CONSTRAINT DF_ProductInputFields_IsSensitive DEFAULT (0),
        RequiresConfirmation bit NOT NULL CONSTRAINT DF_ProductInputFields_RequiresConfirmation DEFAULT (0),
        DisplayStage tinyint NOT NULL CONSTRAINT DF_ProductInputFields_DisplayStage DEFAULT (1),
        SortOrder int NOT NULL CONSTRAINT DF_ProductInputFields_SortOrder DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_ProductInputFields_IsActive DEFAULT (1),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ProductInputFields_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2(7) NULL,
        CONSTRAINT PK_ProductInputFields PRIMARY KEY (Id),
        CONSTRAINT FK_ProductInputFields_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ProductInputFields_Type CHECK (FieldType BETWEEN 1 AND 12),
        CONSTRAINT CK_ProductInputFields_Stage CHECK (DisplayStage IN (1,2)),
        CONSTRAINT CK_ProductInputFields_Length CHECK (MinLength IS NULL OR MinLength >= 0 AND (MaxLength IS NULL OR MaxLength >= MinLength))
    );
END;

IF OBJECT_ID('dbo.CartItemInputValues', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CartItemInputValues
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_CartItemInputValues_Id DEFAULT (newsequentialid()),
        CartItemId uniqueidentifier NOT NULL,
        ProductInputFieldId uniqueidentifier NULL,
        FieldKey varchar(64) NOT NULL,
        FieldLabel nvarchar(120) NOT NULL,
        FieldType tinyint NOT NULL,
        Value nvarchar(2000) NULL,
        EncryptedValue nvarchar(4000) NULL,
        IsSensitive bit NOT NULL CONSTRAINT DF_CartItemInputValues_IsSensitive DEFAULT (0),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_CartItemInputValues_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2(7) NULL,
        CONSTRAINT PK_CartItemInputValues PRIMARY KEY (Id),
        CONSTRAINT FK_CartItemInputValues_CartItems FOREIGN KEY (CartItemId) REFERENCES dbo.CartItems(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CartItemInputValues_ProductInputFields FOREIGN KEY (ProductInputFieldId) REFERENCES dbo.ProductInputFields(Id) ON DELETE SET NULL,
        CONSTRAINT CK_CartItemInputValues_SensitiveStorage CHECK ((IsSensitive = 0 AND EncryptedValue IS NULL) OR (IsSensitive = 1 AND Value IS NULL))
    );
END;

IF OBJECT_ID('dbo.OrderItemInputValues', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItemInputValues
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_OrderItemInputValues_Id DEFAULT (newsequentialid()),
        OrderItemId uniqueidentifier NOT NULL,
        ProductInputFieldId uniqueidentifier NULL,
        FieldKey varchar(64) NOT NULL,
        FieldLabel nvarchar(120) NOT NULL,
        FieldType tinyint NOT NULL,
        Value nvarchar(2000) NULL,
        EncryptedValue nvarchar(4000) NULL,
        IsSensitive bit NOT NULL CONSTRAINT DF_OrderItemInputValues_IsSensitive DEFAULT (0),
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_OrderItemInputValues_CreatedAt DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_OrderItemInputValues PRIMARY KEY (Id),
        CONSTRAINT FK_OrderItemInputValues_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItemInputValues_ProductInputFields FOREIGN KEY (ProductInputFieldId) REFERENCES dbo.ProductInputFields(Id) ON DELETE SET NULL,
        CONSTRAINT CK_OrderItemInputValues_SensitiveStorage CHECK ((IsSensitive = 0 AND EncryptedValue IS NULL) OR (IsSensitive = 1 AND Value IS NULL))
    );
END;

IF OBJECT_ID('dbo.FontAssets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FontAssets
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_FontAssets_Id DEFAULT (newsequentialid()),
        FamilyName nvarchar(100) NOT NULL,
        FilePath nvarchar(500) NULL,
        FileFormat varchar(10) NOT NULL,
        MimeType varchar(100) NULL,
        SizeBytes bigint NOT NULL CONSTRAINT DF_FontAssets_SizeBytes DEFAULT (0),
        IsBuiltIn bit NOT NULL CONSTRAINT DF_FontAssets_IsBuiltIn DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_FontAssets_IsActive DEFAULT (0),
        Scope tinyint NOT NULL CONSTRAINT DF_FontAssets_Scope DEFAULT (3),
        CreatedByUserId uniqueidentifier NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_FontAssets_CreatedAt DEFAULT (sysutcdatetime()),
        UpdatedAt datetime2(7) NULL,
        CONSTRAINT PK_FontAssets PRIMARY KEY (Id),
        CONSTRAINT FK_FontAssets_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL,
        CONSTRAINT CK_FontAssets_Format CHECK (FileFormat IN ('woff2','woff','ttf')),
        CONSTRAINT CK_FontAssets_Scope CHECK (Scope IN (1,2,3)),
        CONSTRAINT CK_FontAssets_Path CHECK ((IsBuiltIn = 1 AND FilePath IS NULL) OR (IsBuiltIn = 0 AND FilePath LIKE '/uploads/fonts/%'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProductFeatures') AND name = 'IX_ProductFeatures_Product_Order')
    CREATE INDEX IX_ProductFeatures_Product_Order ON dbo.ProductFeatures(ProductId, SortOrder, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProductInputFields') AND name = 'UX_ProductInputFields_Product_Key')
    CREATE UNIQUE INDEX UX_ProductInputFields_Product_Key ON dbo.ProductInputFields(ProductId, [Key]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProductInputFields') AND name = 'IX_ProductInputFields_Product_Order')
    CREATE INDEX IX_ProductInputFields_Product_Order ON dbo.ProductInputFields(ProductId, SortOrder, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CartItems') AND name = 'IX_CartItems_Identity')
    CREATE INDEX IX_CartItems_Identity ON dbo.CartItems(CartId, ProductId, ProductVariantId, InputFingerprint);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CartItemInputValues') AND name = 'UX_CartItemInputValues_Item_Key')
    CREATE UNIQUE INDEX UX_CartItemInputValues_Item_Key ON dbo.CartItemInputValues(CartItemId, FieldKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OrderItemInputValues') AND name = 'UX_OrderItemInputValues_Item_Key')
    CREATE UNIQUE INDEX UX_OrderItemInputValues_Item_Key ON dbo.OrderItemInputValues(OrderItemId, FieldKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.FontAssets') AND name = 'UX_FontAssets_FamilyName')
    CREATE UNIQUE INDEX UX_FontAssets_FamilyName ON dbo.FontAssets(FamilyName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.FontAssets') AND name = 'UX_FontAssets_OneActive')
    CREATE UNIQUE INDEX UX_FontAssets_OneActive ON dbo.FontAssets(IsActive) WHERE IsActive = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.FontAssets WHERE IsBuiltIn = 1)
    INSERT dbo.FontAssets (Id, FamilyName, FileFormat, MimeType, SizeBytes, IsBuiltIn, IsActive, Scope)
    VALUES (NEWID(), N'Vazirmatn', 'woff2', 'font/woff2', 0, 1, 1, 3);

COMMIT TRANSACTION;
/* Rollback note: first export OrderItemInputValues. Tables can be dropped in dependency order:
   OrderItemInputValues, CartItemInputValues, ProductInputFields, ProductFeatures, FontAssets.
   InputFingerprint may be retained safely; dropping it is destructive to cart identity history. */
