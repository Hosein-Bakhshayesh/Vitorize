/*=============================================================================
  VITORIZE DATABASE - FRESH INSTALL (FullBootstrap)

  1. Create an EMPTY database on your SQL Server.
  2. Select that database in SSMS (the database dropdown).
  3. Open this file and press Execute.

  This script builds the complete Vitorize schema and seeds the system data the
  application needs on first start. It runs against WHICHEVER DATABASE IS
  CURRENTLY SELECTED - it never names, creates or alters a database.

  DO NOT run this against an existing Vitorize installation. A guard below stops
  the script if Vitorize tables are already present.

  Requires: SQL Server 2019 or later. Plain SSMS execution - no SQLCMD mode,
  no PowerShell, no DACPAC, no parameters.

  Contains no passwords, connection strings or application keys. Those live only
  in the application's appsettings.Production.json on the server.
=============================================================================*/
GO

SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/*----------------------------------------------------------------------------
  Fresh-install guard. SET NOEXEC ON suppresses every later batch in this
  session without dropping or altering anything.
----------------------------------------------------------------------------*/
IF EXISTS (SELECT 1 FROM sys.tables WHERE name IN (N'Settings', N'Users', N'Orders', N'DatabaseScriptHistory'))
BEGIN
    RAISERROR(N'Vitorize FullBootstrap is for a fresh EMPTY database only. This database already contains Vitorize tables - nothing has been changed. Use the versioned upgrade process instead.', 16, 1);
    SET NOEXEC ON;
END
GO

PRINT N'Vitorize fresh install starting...';
GO
GO
PRINT N'Creating Schema [AdminVitorize]...';


GO
CREATE SCHEMA [AdminVitorize]
    AUTHORIZATION [dbo];


GO
PRINT N'Creating Table [dbo].[AuditLogs]...';


GO
CREATE TABLE [dbo].[AuditLogs] (
    [Id]         UNIQUEIDENTIFIER NOT NULL,
    [UserId]     UNIQUEIDENTIFIER NULL,
    [ActionType] NVARCHAR (100)   NOT NULL,
    [EntityName] NVARCHAR (100)   NOT NULL,
    [EntityId]   NVARCHAR (100)   NULL,
    [Data]       NVARCHAR (MAX)   NULL,
    [IpAddress]  NVARCHAR (50)    NULL,
    [UserAgent]  NVARCHAR (1000)  NULL,
    [CreatedAt]  DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[AuditLogs].[IX_AuditLogs_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId]
    ON [dbo].[AuditLogs]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[AuditLogs].[IX_AuditLogs_Entity]...';


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Entity]
    ON [dbo].[AuditLogs]([EntityName] ASC, [EntityId] ASC);


GO
PRINT N'Creating Table [dbo].[Banners]...';


GO
CREATE TABLE [dbo].[Banners] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Title]           NVARCHAR (200)   NOT NULL,
    [ImagePath]       NVARCHAR (500)   NOT NULL,
    [MobileImagePath] NVARCHAR (500)   NULL,
    [LinkUrl]         NVARCHAR (500)   NULL,
    [Position]        NVARCHAR (100)   NOT NULL,
    [SortOrder]       INT              NOT NULL,
    [IsActive]        BIT              NOT NULL,
    [StartsAt]        DATETIME2 (7)    NULL,
    [EndsAt]          DATETIME2 (7)    NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    [AltText]         NVARCHAR (250)   NULL,
    [MobileAltText]   NVARCHAR (250)   NULL,
    CONSTRAINT [PK_Banners] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Banners].[IX_Banners_Position]...';


GO
CREATE NONCLUSTERED INDEX [IX_Banners_Position]
    ON [dbo].[Banners]([Position] ASC);


GO
PRINT N'Creating Table [dbo].[BlogPosts]...';


GO
CREATE TABLE [dbo].[BlogPosts] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [Title]             NVARCHAR (300)   NOT NULL,
    [Slug]              NVARCHAR (300)   NOT NULL,
    [Summary]           NVARCHAR (1000)  NULL,
    [ContentHtml]       NVARCHAR (MAX)   NOT NULL,
    [CoverImagePath]    NVARCHAR (500)   NULL,
    [SeoTitle]          NVARCHAR (250)   NULL,
    [SeoDescription]    NVARCHAR (500)   NULL,
    [IsPublished]       BIT              NOT NULL,
    [PublishedAt]       DATETIME2 (7)    NULL,
    [CreatedAt]         DATETIME2 (7)    NOT NULL,
    [UpdatedAt]         DATETIME2 (7)    NULL,
    [FocusKeyword]      NVARCHAR (200)   NULL,
    [CoverImageAltText] NVARCHAR (250)   NULL,
    CONSTRAINT [PK_BlogPosts] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[BlogPosts].[UX_BlogPosts_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_BlogPosts_Slug]
    ON [dbo].[BlogPosts]([Slug] ASC);


GO
PRINT N'Creating Table [dbo].[Brands]...';


GO
CREATE TABLE [dbo].[Brands] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [Title]          NVARCHAR (150)   NOT NULL,
    [Slug]           NVARCHAR (200)   NOT NULL,
    [ImagePath]      NVARCHAR (500)   NULL,
    [IsActive]       BIT              NOT NULL,
    [CreatedAt]      DATETIME2 (7)    NOT NULL,
    [Description]    NVARCHAR (2000)  NULL,
    [SeoTitle]       NVARCHAR (250)   NULL,
    [SeoDescription] NVARCHAR (500)   NULL,
    [FocusKeyword]   NVARCHAR (200)   NULL,
    [ImageAltText]   NVARCHAR (250)   NULL,
    [UpdatedAt]      DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Brands] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Brands].[UX_Brands_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Brands_Slug]
    ON [dbo].[Brands]([Slug] ASC);


GO
PRINT N'Creating Table [dbo].[CartItemInputValues]...';


GO
CREATE TABLE [dbo].[CartItemInputValues] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [CartItemId]          UNIQUEIDENTIFIER NOT NULL,
    [ProductInputFieldId] UNIQUEIDENTIFIER NULL,
    [FieldKey]            VARCHAR (64)     NOT NULL,
    [FieldLabel]          NVARCHAR (120)   NOT NULL,
    [FieldType]           TINYINT          NOT NULL,
    [Value]               NVARCHAR (2000)  NULL,
    [EncryptedValue]      NVARCHAR (4000)  NULL,
    [IsSensitive]         BIT              NOT NULL,
    [CreatedAt]           DATETIME2 (7)    NOT NULL,
    [UpdatedAt]           DATETIME2 (7)    NULL,
    CONSTRAINT [PK_CartItemInputValues] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[CartItemInputValues].[UX_CartItemInputValues_Item_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_CartItemInputValues_Item_Key]
    ON [dbo].[CartItemInputValues]([CartItemId] ASC, [FieldKey] ASC);


GO
PRINT N'Creating Table [dbo].[CartItems]...';


GO
CREATE TABLE [dbo].[CartItems] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [CartId]           UNIQUEIDENTIFIER NOT NULL,
    [ProductId]        UNIQUEIDENTIFIER NOT NULL,
    [ProductVariantId] UNIQUEIDENTIFIER NULL,
    [Quantity]         INT              NOT NULL,
    [UnitPrice]        DECIMAL (18, 2)  NOT NULL,
    [CreatedAt]        DATETIME2 (7)    NOT NULL,
    [UpdatedAt]        DATETIME2 (7)    NULL,
    [InputFingerprint] VARCHAR (64)     NOT NULL,
    [CurrencyType]     TINYINT          NOT NULL,
    CONSTRAINT [PK_CartItems] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[CartItems].[IX_CartItems_Identity]...';


GO
CREATE NONCLUSTERED INDEX [IX_CartItems_Identity]
    ON [dbo].[CartItems]([CartId] ASC, [ProductId] ASC, [ProductVariantId] ASC, [InputFingerprint] ASC);


GO
PRINT N'Creating Index [dbo].[CartItems].[IX_CartItems_CartId]...';


GO
CREATE NONCLUSTERED INDEX [IX_CartItems_CartId]
    ON [dbo].[CartItems]([CartId] ASC);


GO
PRINT N'Creating Table [dbo].[Carts]...';


GO
CREATE TABLE [dbo].[Carts] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [CreatedAt]      DATETIME2 (7)    NOT NULL,
    [UpdatedAt]      DATETIME2 (7)    NULL,
    [GuestTokenHash] VARCHAR (64)     NULL,
    [LastActivityAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Carts] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Carts].[UX_Carts_GuestTokenHash]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Carts_GuestTokenHash]
    ON [dbo].[Carts]([GuestTokenHash] ASC) WHERE ([GuestTokenHash] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Carts].[IX_Carts_GuestLastActivityAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_Carts_GuestLastActivityAt]
    ON [dbo].[Carts]([LastActivityAt] ASC) WHERE ([GuestTokenHash] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Carts].[UX_Carts_UserId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Carts_UserId]
    ON [dbo].[Carts]([UserId] ASC) WHERE ([UserId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[Categories]...';


GO
CREATE TABLE [dbo].[Categories] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [ParentId]       UNIQUEIDENTIFIER NULL,
    [Title]          NVARCHAR (200)   NOT NULL,
    [Slug]           NVARCHAR (250)   NOT NULL,
    [Description]    NVARCHAR (MAX)   NULL,
    [ImagePath]      NVARCHAR (500)   NULL,
    [Icon]           NVARCHAR (100)   NULL,
    [SortOrder]      INT              NOT NULL,
    [IsActive]       BIT              NOT NULL,
    [SeoTitle]       NVARCHAR (250)   NULL,
    [SeoDescription] NVARCHAR (500)   NULL,
    [CreatedAt]      DATETIME2 (7)    NOT NULL,
    [UpdatedAt]      DATETIME2 (7)    NULL,
    [IsDeleted]      BIT              NOT NULL,
    [DeletedAt]      DATETIME2 (7)    NULL,
    [FocusKeyword]   NVARCHAR (200)   NULL,
    [ImageAltText]   NVARCHAR (250)   NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Categories].[UX_Categories_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Categories_Slug]
    ON [dbo].[Categories]([Slug] ASC);


GO
PRINT N'Creating Index [dbo].[Categories].[IX_Categories_ParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Categories_ParentId]
    ON [dbo].[Categories]([ParentId] ASC);


GO
PRINT N'Creating Table [dbo].[Coupons]...';


GO
CREATE TABLE [dbo].[Coupons] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Code]            NVARCHAR (100)   NOT NULL,
    [Title]           NVARCHAR (200)   NOT NULL,
    [DiscountType]    TINYINT          NOT NULL,
    [DiscountValue]   DECIMAL (18, 2)  NOT NULL,
    [MaxUsageCount]   INT              NULL,
    [UsedCount]       INT              NOT NULL,
    [MaxUsagePerUser] INT              NULL,
    [MinOrderAmount]  DECIMAL (18, 2)  NULL,
    [StartsAt]        DATETIME2 (7)    NULL,
    [EndsAt]          DATETIME2 (7)    NULL,
    [IsActive]        BIT              NOT NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Coupons] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Coupons].[UX_Coupons_Code]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Coupons_Code]
    ON [dbo].[Coupons]([Code] ASC);


GO
PRINT N'Creating Table [dbo].[CouponUsages]...';


GO
CREATE TABLE [dbo].[CouponUsages] (
    [Id]       UNIQUEIDENTIFIER NOT NULL,
    [CouponId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]   UNIQUEIDENTIFIER NOT NULL,
    [OrderId]  UNIQUEIDENTIFIER NOT NULL,
    [UsedAt]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_CouponUsages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[CouponUsages].[UX_CouponUsages_OrderId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_CouponUsages_OrderId]
    ON [dbo].[CouponUsages]([OrderId] ASC);


GO
PRINT N'Creating Index [dbo].[CouponUsages].[IX_CouponUsages_CouponId]...';


GO
CREATE NONCLUSTERED INDEX [IX_CouponUsages_CouponId]
    ON [dbo].[CouponUsages]([CouponId] ASC);


GO
PRINT N'Creating Index [dbo].[CouponUsages].[IX_CouponUsages_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_CouponUsages_UserId]
    ON [dbo].[CouponUsages]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[DatabaseScriptHistory]...';


GO
CREATE TABLE [dbo].[DatabaseScriptHistory] (
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [ScriptName]    NVARCHAR (260)  NOT NULL,
    [ScriptVersion] NVARCHAR (50)   NOT NULL,
    [ScriptHash]    CHAR (64)       COLLATE Latin1_General_100_BIN2 NOT NULL,
    [AppliedAt]     DATETIME2 (7)   NOT NULL,
    [AppliedBy]     NVARCHAR (128)  NOT NULL,
    [Environment]   NVARCHAR (50)   NOT NULL,
    [Success]       BIT             NOT NULL,
    [Notes]         NVARCHAR (1000) NULL,
    CONSTRAINT [PK_DatabaseScriptHistory] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[DatabaseScriptHistory].[UX_DatabaseScriptHistory_ScriptName]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DatabaseScriptHistory_ScriptName]
    ON [dbo].[DatabaseScriptHistory]([ScriptName] ASC);


GO
PRINT N'Creating Index [dbo].[DatabaseScriptHistory].[UX_DatabaseScriptHistory_ScriptVersion]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DatabaseScriptHistory_ScriptVersion]
    ON [dbo].[DatabaseScriptHistory]([ScriptVersion] ASC);


GO
PRINT N'Creating Table [dbo].[ErrorLogs]...';


GO
CREATE TABLE [dbo].[ErrorLogs] (
    [Id]         UNIQUEIDENTIFIER NOT NULL,
    [Message]    NVARCHAR (MAX)   NOT NULL,
    [StackTrace] NVARCHAR (MAX)   NULL,
    [Source]     NVARCHAR (300)   NULL,
    [CreatedAt]  DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_ErrorLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Table [dbo].[FAQs]...';


GO
CREATE TABLE [dbo].[FAQs] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [Question]  NVARCHAR (500)   NOT NULL,
    [Answer]    NVARCHAR (MAX)   NOT NULL,
    [SortOrder] INT              NOT NULL,
    [IsActive]  BIT              NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_FAQs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[FAQs].[IX_FAQs_Product_Active_Sort]...';


GO
CREATE NONCLUSTERED INDEX [IX_FAQs_Product_Active_Sort]
    ON [dbo].[FAQs]([ProductId] ASC, [IsActive] ASC, [SortOrder] ASC);


GO
PRINT N'Creating Table [dbo].[FinancialAuditLogs]...';


GO
CREATE TABLE [dbo].[FinancialAuditLogs] (
    [Id]            BIGINT           IDENTITY (1, 1) NOT NULL,
    [EventType]     NVARCHAR (100)   NOT NULL,
    [EntityType]    NVARCHAR (100)   NOT NULL,
    [EntityId]      UNIQUEIDENTIFIER NOT NULL,
    [UserId]        UNIQUEIDENTIFIER NULL,
    [Amount]        DECIMAL (18, 2)  NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [Detail]        NVARCHAR (2000)  NULL,
    [CreatedAt]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_FinancialAuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[FinancialAuditLogs].[IX_FinancialAuditLogs_CorrelationId]...';


GO
CREATE NONCLUSTERED INDEX [IX_FinancialAuditLogs_CorrelationId]
    ON [dbo].[FinancialAuditLogs]([CorrelationId] ASC);


GO
PRINT N'Creating Index [dbo].[FinancialAuditLogs].[IX_FinancialAuditLogs_Entity]...';


GO
CREATE NONCLUSTERED INDEX [IX_FinancialAuditLogs_Entity]
    ON [dbo].[FinancialAuditLogs]([EntityType] ASC, [EntityId] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[FinancialAuditLogs].[IX_FinancialAuditLogs_Event]...';


GO
CREATE NONCLUSTERED INDEX [IX_FinancialAuditLogs_Event]
    ON [dbo].[FinancialAuditLogs]([EventType] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Table [dbo].[FontAssets]...';


GO
CREATE TABLE [dbo].[FontAssets] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [FamilyName]      NVARCHAR (100)   NOT NULL,
    [FilePath]        NVARCHAR (500)   NULL,
    [FileFormat]      VARCHAR (10)     NOT NULL,
    [MimeType]        VARCHAR (100)    NULL,
    [SizeBytes]       BIGINT           NOT NULL,
    [IsBuiltIn]       BIT              NOT NULL,
    [IsActive]        BIT              NOT NULL,
    [Scope]           TINYINT          NOT NULL,
    [CreatedByUserId] UNIQUEIDENTIFIER NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    [UpdatedAt]       DATETIME2 (7)    NULL,
    CONSTRAINT [PK_FontAssets] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[FontAssets].[UX_FontAssets_FamilyName]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_FontAssets_FamilyName]
    ON [dbo].[FontAssets]([FamilyName] ASC);


GO
PRINT N'Creating Index [dbo].[FontAssets].[UX_FontAssets_OneActive]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_FontAssets_OneActive]
    ON [dbo].[FontAssets]([IsActive] ASC) WHERE ([IsActive]=(1));


GO
PRINT N'Creating Table [dbo].[GiftCodeBatches]...';


GO
CREATE TABLE [dbo].[GiftCodeBatches] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [ProductId]         UNIQUEIDENTIFIER NULL,
    [ProductVariantId]  UNIQUEIDENTIFIER NULL,
    [BatchTitle]        NVARCHAR (200)   NOT NULL,
    [SourceName]        NVARCHAR (200)   NULL,
    [PurchasePrice]     DECIMAL (18, 2)  NULL,
    [Notes]             NVARCHAR (1000)  NULL,
    [ImportedByAdminId] UNIQUEIDENTIFIER NULL,
    [ImportedAt]        DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_GiftCodeBatches] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[GiftCodeBatches].[IX_GiftCodeBatches_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodeBatches_ProductId]
    ON [dbo].[GiftCodeBatches]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[GiftCodeBatches].[IX_GiftCodeBatches_ProductVariantId]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodeBatches_ProductVariantId]
    ON [dbo].[GiftCodeBatches]([ProductVariantId] ASC);


GO
PRINT N'Creating Table [dbo].[GiftCodeReservations]...';


GO
CREATE TABLE [dbo].[GiftCodeReservations] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [UserId]           UNIQUEIDENTIFIER NOT NULL,
    [OrderId]          UNIQUEIDENTIFIER NULL,
    [OrderItemId]      UNIQUEIDENTIFIER NULL,
    [ProductId]        UNIQUEIDENTIFIER NOT NULL,
    [ProductVariantId] UNIQUEIDENTIFIER NULL,
    [GiftCodeId]       UNIQUEIDENTIFIER NOT NULL,
    [Status]           TINYINT          NOT NULL,
    [ReservedAt]       DATETIME2 (7)    NOT NULL,
    [ExpiresAt]        DATETIME2 (7)    NOT NULL,
    [SoldAt]           DATETIME2 (7)    NULL,
    [ReleasedAt]       DATETIME2 (7)    NULL,
    CONSTRAINT [PK_GiftCodeReservations] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[GiftCodeReservations].[UX_GiftCodeReservations_Active_GiftCode]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_GiftCodeReservations_Active_GiftCode]
    ON [dbo].[GiftCodeReservations]([GiftCodeId] ASC) WHERE ([Status]=(1));


GO
PRINT N'Creating Index [dbo].[GiftCodeReservations].[IX_GiftCodeReservations_OrderItemId]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodeReservations_OrderItemId]
    ON [dbo].[GiftCodeReservations]([OrderItemId] ASC) WHERE ([OrderItemId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[GiftCodeReservations].[IX_GiftCodeReservations_UserId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodeReservations_UserId_Status]
    ON [dbo].[GiftCodeReservations]([UserId] ASC, [Status] ASC, [ReservedAt] ASC);


GO
PRINT N'Creating Table [dbo].[GiftCodes]...';


GO
CREATE TABLE [dbo].[GiftCodes] (
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [BatchId]              UNIQUEIDENTIFIER NULL,
    [ProductId]            UNIQUEIDENTIFIER NOT NULL,
    [ProductVariantId]     UNIQUEIDENTIFIER NULL,
    [EncryptedCode]        NVARCHAR (MAX)   NOT NULL,
    [MaskedCode]           NVARCHAR (100)   NULL,
    [SerialNumber]         NVARCHAR (200)   NULL,
    [ExtraData]            NVARCHAR (1000)  NULL,
    [Status]               TINYINT          NOT NULL,
    [ReservedByUserId]     UNIQUEIDENTIFIER NULL,
    [ReservationExpiresAt] DATETIME2 (7)    NULL,
    [EncryptionVersion]    INT              NOT NULL,
    [CodeHashFingerprint]  NVARCHAR (500)   NULL,
    [ReservedAt]           DATETIME2 (7)    NULL,
    [SoldAt]               DATETIME2 (7)    NULL,
    [DeliveredAt]          DATETIME2 (7)    NULL,
    [ExpiresAt]            DATETIME2 (7)    NULL,
    [OrderItemId]          UNIQUEIDENTIFIER NULL,
    [CreatedAt]            DATETIME2 (7)    NOT NULL,
    [UpdatedAt]            DATETIME2 (7)    NULL,
    CONSTRAINT [PK_GiftCodes] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_Available_ProductVariant]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_Available_ProductVariant]
    ON [dbo].[GiftCodes]([ProductId] ASC, [ProductVariantId] ASC, [Status] ASC, [CreatedAt] ASC)
    INCLUDE([Id], [ExpiresAt]) WHERE ([Status]=(0));


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_Status]
    ON [dbo].[GiftCodes]([Status] ASC);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[UX_GiftCodes_CodeHashFingerprint]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_GiftCodes_CodeHashFingerprint]
    ON [dbo].[GiftCodes]([CodeHashFingerprint] ASC) WHERE ([CodeHashFingerprint] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_ProductVariant_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_ProductVariant_Status]
    ON [dbo].[GiftCodes]([ProductVariantId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_ReservationExpiresAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_ReservationExpiresAt]
    ON [dbo].[GiftCodes]([Status] ASC, [ReservationExpiresAt] ASC) WHERE ([Status]=(1));


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_Product_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_Product_Status]
    ON [dbo].[GiftCodes]([ProductId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_OrderItemId]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_OrderItemId]
    ON [dbo].[GiftCodes]([OrderItemId] ASC);


GO
PRINT N'Creating Table [dbo].[IdempotencyKeys]...';


GO
CREATE TABLE [dbo].[IdempotencyKeys] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NULL,
    [Key]          NVARCHAR (200)   NOT NULL,
    [RequestHash]  NVARCHAR (500)   NULL,
    [ResponseJson] NVARCHAR (MAX)   NULL,
    [StatusCode]   INT              NULL,
    [CreatedAt]    DATETIME2 (7)    NOT NULL,
    [ExpiresAt]    DATETIME2 (7)    NOT NULL,
    [Status]       TINYINT          NOT NULL,
    [CompletedAt]  DATETIME2 (7)    NULL,
    [FailedAt]     DATETIME2 (7)    NULL,
    [ErrorMessage] NVARCHAR (1000)  NULL,
    CONSTRAINT [PK_IdempotencyKeys] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[IdempotencyKeys].[IX_IdempotencyKeys_UserId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_IdempotencyKeys_UserId_CreatedAt]
    ON [dbo].[IdempotencyKeys]([UserId] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[IdempotencyKeys].[UX_IdempotencyKeys_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_IdempotencyKeys_Key]
    ON [dbo].[IdempotencyKeys]([Key] ASC);


GO
PRINT N'Creating Table [dbo].[KycDocumentTypes]...';


GO
CREATE TABLE [dbo].[KycDocumentTypes] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [Code]              NVARCHAR (100)   NOT NULL,
    [Title]             NVARCHAR (250)   NOT NULL,
    [Description]       NVARCHAR (1000)  NULL,
    [IsActive]          BIT              NOT NULL,
    [AllowedExtensions] NVARCHAR (250)   NOT NULL,
    [MaxFileSizeBytes]  BIGINT           NOT NULL,
    [SortOrder]         INT              NOT NULL,
    [CreatedAt]         DATETIME2 (7)    NOT NULL,
    [UpdatedAt]         DATETIME2 (7)    NULL,
    CONSTRAINT [PK_KycDocumentTypes] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_KycDocumentTypes_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);


GO
PRINT N'Creating Table [dbo].[KycPolicies]...';


GO
CREATE TABLE [dbo].[KycPolicies] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [Code]      NVARCHAR (100)   NOT NULL,
    [Name]      NVARCHAR (250)   NOT NULL,
    [IsActive]  BIT              NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_KycPolicies] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_KycPolicies_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);


GO
PRINT N'Creating Table [dbo].[KycPolicyDocumentRequirements]...';


GO
CREATE TABLE [dbo].[KycPolicyDocumentRequirements] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [KycPolicyVersionId]    UNIQUEIDENTIFIER NOT NULL,
    [KycDocumentTypeId]     UNIQUEIDENTIFIER NOT NULL,
    [IsRequired]            BIT              NOT NULL,
    [SortOrder]             INT              NOT NULL,
    [Instructions]          NVARCHAR (1000)  NULL,
    [RedactionMode]         TINYINT          NOT NULL,
    [RedactionInstructions] NVARCHAR (1000)  NULL,
    CONSTRAINT [PK_KycPolicyDocumentRequirements] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_KycPolicyDocumentRequirements_Version_Document] UNIQUE NONCLUSTERED ([KycPolicyVersionId] ASC, [KycDocumentTypeId] ASC)
);


GO
PRINT N'Creating Table [dbo].[KycPolicyVersions]...';


GO
CREATE TABLE [dbo].[KycPolicyVersions] (
    [Id]                          UNIQUEIDENTIFIER NOT NULL,
    [KycPolicyId]                 UNIQUEIDENTIFIER NOT NULL,
    [Version]                     INT              NOT NULL,
    [Status]                      TINYINT          NOT NULL,
    [CustomerTitle]               NVARCHAR (250)   NOT NULL,
    [CustomerInstructions]        NVARCHAR (MAX)   NULL,
    [CreatedAt]                   DATETIME2 (7)    NOT NULL,
    [PublishedAt]                 DATETIME2 (7)    NULL,
    [CustomerActionDeadlineHours] INT              NULL,
    CONSTRAINT [PK_KycPolicyVersions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_KycPolicyVersions_Policy_Version] UNIQUE NONCLUSTERED ([KycPolicyId] ASC, [Version] ASC)
);


GO
PRINT N'Creating Table [dbo].[LegacyRedirects]...';


GO
CREATE TABLE [dbo].[LegacyRedirects] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [SourcePath]      NVARCHAR (750)   NOT NULL,
    [DestinationPath] NVARCHAR (1000)  NULL,
    [StatusCode]      SMALLINT         NOT NULL,
    [IsActive]        BIT              NOT NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    [UpdatedAt]       DATETIME2 (7)    NULL,
    CONSTRAINT [PK_LegacyRedirects] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[LegacyRedirects].[UX_LegacyRedirects_SourcePath]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_LegacyRedirects_SourcePath]
    ON [dbo].[LegacyRedirects]([SourcePath] ASC);


GO
PRINT N'Creating Index [dbo].[LegacyRedirects].[IX_LegacyRedirects_Active_Source]...';


GO
CREATE NONCLUSTERED INDEX [IX_LegacyRedirects_Active_Source]
    ON [dbo].[LegacyRedirects]([IsActive] ASC, [SourcePath] ASC)
    INCLUDE([DestinationPath], [StatusCode], [UpdatedAt]);


GO
PRINT N'Creating Table [dbo].[NotificationBroadcasts]...';


GO
CREATE TABLE [dbo].[NotificationBroadcasts] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Title]           NVARCHAR (250)   NOT NULL,
    [Message]         NVARCHAR (MAX)   NOT NULL,
    [AudienceType]    TINYINT          NOT NULL,
    [RecipientCount]  INT              NOT NULL,
    [Status]          TINYINT          NOT NULL,
    [ActionUrl]       NVARCHAR (500)   NULL,
    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    [SentAt]          DATETIME2 (7)    NULL,
    CONSTRAINT [PK_NotificationBroadcasts] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[NotificationBroadcasts].[IX_NotificationBroadcasts_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_NotificationBroadcasts_CreatedAt]
    ON [dbo].[NotificationBroadcasts]([CreatedAt] DESC);


GO
PRINT N'Creating Table [dbo].[Notifications]...';


GO
CREATE TABLE [dbo].[Notifications] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [UserId]      UNIQUEIDENTIFIER NOT NULL,
    [Title]       NVARCHAR (250)   NOT NULL,
    [Message]     NVARCHAR (MAX)   NOT NULL,
    [Type]        TINYINT          NOT NULL,
    [IsRead]      BIT              NOT NULL,
    [CreatedAt]   DATETIME2 (7)    NOT NULL,
    [ReadAt]      DATETIME2 (7)    NULL,
    [BroadcastId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Notifications].[IX_Notifications_UserId_IsRead]...';


GO
CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_IsRead]
    ON [dbo].[Notifications]([UserId] ASC, [IsRead] ASC);


GO
PRINT N'Creating Index [dbo].[Notifications].[IX_Notifications_BroadcastId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Notifications_BroadcastId]
    ON [dbo].[Notifications]([BroadcastId] ASC) WHERE ([BroadcastId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Notifications].[UX_Notifications_Broadcast_User]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Notifications_Broadcast_User]
    ON [dbo].[Notifications]([BroadcastId] ASC, [UserId] ASC) WHERE ([BroadcastId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[OrderItemDeliveries]...';


GO
CREATE TABLE [dbo].[OrderItemDeliveries] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [OrderItemId]           UNIQUEIDENTIFIER NOT NULL,
    [DeliveryType]          TINYINT          NOT NULL,
    [GiftCodeId]            UNIQUEIDENTIFIER NULL,
    [DeliveredContent]      NVARCHAR (MAX)   NULL,
    [IsVisibleToCustomer]   BIT              NOT NULL,
    [DeliveredByUserId]     UNIQUEIDENTIFIER NULL,
    [CreatedAt]             DATETIME2 (7)    NOT NULL,
    [ContentHash]           CHAR (64)        COLLATE Latin1_General_100_BIN2 NULL,
    [EncryptionVersion]     SMALLINT         NULL,
    [ManualDeliveryItemKey] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_OrderItemDeliveries] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderItemDeliveries].[UX_OrderItemDeliveries_Manual_Item]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_OrderItemDeliveries_Manual_Item]
    ON [dbo].[OrderItemDeliveries]([ManualDeliveryItemKey] ASC) WHERE ([ManualDeliveryItemKey] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[OrderItemDeliveries].[IX_OrderItemDeliveries_GiftCodeId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItemDeliveries_GiftCodeId]
    ON [dbo].[OrderItemDeliveries]([GiftCodeId] ASC);


GO
PRINT N'Creating Index [dbo].[OrderItemDeliveries].[IX_OrderItemDeliveries_OrderItemId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItemDeliveries_OrderItemId]
    ON [dbo].[OrderItemDeliveries]([OrderItemId] ASC);


GO
PRINT N'Creating Table [dbo].[OrderItemInputValues]...';


GO
CREATE TABLE [dbo].[OrderItemInputValues] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [OrderItemId]         UNIQUEIDENTIFIER NOT NULL,
    [ProductInputFieldId] UNIQUEIDENTIFIER NULL,
    [FieldKey]            VARCHAR (64)     NOT NULL,
    [FieldLabel]          NVARCHAR (120)   NOT NULL,
    [FieldType]           TINYINT          NOT NULL,
    [Value]               NVARCHAR (2000)  NULL,
    [EncryptedValue]      NVARCHAR (4000)  NULL,
    [IsSensitive]         BIT              NOT NULL,
    [CreatedAt]           DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrderItemInputValues] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderItemInputValues].[UX_OrderItemInputValues_Item_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_OrderItemInputValues_Item_Key]
    ON [dbo].[OrderItemInputValues]([OrderItemId] ASC, [FieldKey] ASC);


GO
PRINT N'Creating Table [dbo].[OrderItemKycFinanceResolutions]...';


GO
CREATE TABLE [dbo].[OrderItemKycFinanceResolutions] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrderItemId]       UNIQUEIDENTIFIER NOT NULL,
    [Status]            TINYINT          NOT NULL,
    [Reason]            NVARCHAR (1000)  NULL,
    [ExternalReference] NVARCHAR (200)   NULL,
    [ResolvedByUserId]  UNIQUEIDENTIFIER NULL,
    [CreatedAt]         DATETIME2 (7)    NOT NULL,
    [ResolvedAt]        DATETIME2 (7)    NULL,
    CONSTRAINT [PK_OrderItemKycFinanceResolutions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_OrderItemKycFinanceResolutions_OrderItem] UNIQUE NONCLUSTERED ([OrderItemId] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderItemKycFinanceResolutions].[IX_OrderItemKycFinanceResolutions_Status_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItemKycFinanceResolutions_Status_CreatedAt]
    ON [dbo].[OrderItemKycFinanceResolutions]([Status] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Table [dbo].[OrderItemKycStates]...';


GO
CREATE TABLE [dbo].[OrderItemKycStates] (
    [Id]                               UNIQUEIDENTIFIER NOT NULL,
    [OrderItemId]                      UNIQUEIDENTIFIER NOT NULL,
    [Status]                           TINYINT          NOT NULL,
    [CreatedAt]                        DATETIME2 (7)    NOT NULL,
    [UpdatedAt]                        DATETIME2 (7)    NOT NULL,
    [SatisfiedAt]                      DATETIME2 (7)    NULL,
    [SatisfiedByVerificationProfileId] UNIQUEIDENTIFIER NULL,
    [RowVersion]                       ROWVERSION       NOT NULL,
    [CustomerActionDeadlineAt]         DATETIME2 (7)    NULL,
    CONSTRAINT [PK_OrderItemKycStates] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_OrderItemKycStates_OrderItemId] UNIQUE NONCLUSTERED ([OrderItemId] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderItemKycStates].[IX_OrderItemKycStates_SatisfiedByVerificationProfileId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItemKycStates_SatisfiedByVerificationProfileId]
    ON [dbo].[OrderItemKycStates]([SatisfiedByVerificationProfileId] ASC) WHERE ([SatisfiedByVerificationProfileId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[OrderItems]...';


GO
CREATE TABLE [dbo].[OrderItems] (
    [Id]                             UNIQUEIDENTIFIER NOT NULL,
    [OrderId]                        UNIQUEIDENTIFIER NOT NULL,
    [ProductId]                      UNIQUEIDENTIFIER NOT NULL,
    [ProductVariantId]               UNIQUEIDENTIFIER NULL,
    [ProductTitle]                   NVARCHAR (250)   NOT NULL,
    [VariantTitle]                   NVARCHAR (200)   NULL,
    [Quantity]                       INT              NOT NULL,
    [UnitPrice]                      DECIMAL (18, 2)  NOT NULL,
    [TotalPrice]                     DECIMAL (18, 2)  NOT NULL,
    [DeliveryType]                   TINYINT          NOT NULL,
    [DeliveryStatus]                 TINYINT          NOT NULL,
    [RequiresVerification]           BIT              NOT NULL,
    [SupportTicketId]                UNIQUEIDENTIFIER NULL,
    [CreatedAt]                      DATETIME2 (7)    NOT NULL,
    [DeliveredAt]                    DATETIME2 (7)    NULL,
    [CurrencyType]                   TINYINT          NOT NULL,
    [KycRequirementMode]             TINYINT          NOT NULL,
    [KycThresholdAmount]             DECIMAL (18, 2)  NULL,
    [KycEvaluatedAmount]             DECIMAL (18, 2)  NOT NULL,
    [KycPolicyVersionId]             UNIQUEIDENTIFIER NULL,
    [KycCustomerActionDeadlineHours] INT              NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderItems].[IX_OrderItems_OrderId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItems_OrderId]
    ON [dbo].[OrderItems]([OrderId] ASC);


GO
PRINT N'Creating Index [dbo].[OrderItems].[IX_OrderItems_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItems_ProductId]
    ON [dbo].[OrderItems]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[OrderItems].[IX_OrderItems_KycPolicyVersionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItems_KycPolicyVersionId]
    ON [dbo].[OrderItems]([KycPolicyVersionId] ASC) WHERE ([KycPolicyVersionId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[Orders]...';


GO
CREATE TABLE [dbo].[Orders] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [UserId]             UNIQUEIDENTIFIER NOT NULL,
    [OrderNumber]        NVARCHAR (50)    NOT NULL,
    [Status]             TINYINT          NOT NULL,
    [PaymentStatus]      TINYINT          NOT NULL,
    [SubtotalAmount]     DECIMAL (18, 2)  NOT NULL,
    [DiscountAmount]     DECIMAL (18, 2)  NOT NULL,
    [FinalAmount]        DECIMAL (18, 2)  NOT NULL,
    [CouponId]           UNIQUEIDENTIFIER NULL,
    [Description]        NVARCHAR (1000)  NULL,
    [AdminNote]          NVARCHAR (2000)  NULL,
    [CreatedAt]          DATETIME2 (7)    NOT NULL,
    [PaidAt]             DATETIME2 (7)    NULL,
    [CompletedAt]        DATETIME2 (7)    NULL,
    [UpdatedAt]          DATETIME2 (7)    NULL,
    [CurrencyType]       TINYINT          NOT NULL,
    [VatEnabled]         BIT              NOT NULL,
    [VatRatePercent]     DECIMAL (5, 2)   NOT NULL,
    [VatCalculationMode] TINYINT          NOT NULL,
    [VatAmount]          DECIMAL (18, 2)  NOT NULL,
    [VatTaxableAmount]   DECIMAL (18, 2)  NOT NULL,
    [HiddenByCustomerAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Orders].[IX_Orders_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Orders_UserId]
    ON [dbo].[Orders]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[Orders].[IX_Orders_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Orders_Status]
    ON [dbo].[Orders]([Status] ASC);


GO
PRINT N'Creating Index [dbo].[Orders].[UX_Orders_OrderNumber]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Orders_OrderNumber]
    ON [dbo].[Orders]([OrderNumber] ASC);


GO
PRINT N'Creating Index [dbo].[Orders].[IX_Orders_UserId_HiddenByCustomerAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_Orders_UserId_HiddenByCustomerAt]
    ON [dbo].[Orders]([UserId] ASC, [HiddenByCustomerAt] ASC)
    INCLUDE([CreatedAt]);


GO
PRINT N'Creating Table [dbo].[OrderStatusHistories]...';


GO
CREATE TABLE [dbo].[OrderStatusHistories] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [OrderId]         UNIQUEIDENTIFIER NOT NULL,
    [FromStatus]      TINYINT          NULL,
    [ToStatus]        TINYINT          NOT NULL,
    [ChangedByUserId] UNIQUEIDENTIFIER NULL,
    [Note]            NVARCHAR (1000)  NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OrderStatusHistories].[IX_OrderStatusHistories_OrderId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderStatusHistories_OrderId]
    ON [dbo].[OrderStatusHistories]([OrderId] ASC);


GO
PRINT N'Creating Table [dbo].[OtpCodes]...';


GO
CREATE TABLE [dbo].[OtpCodes] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NULL,
    [Mobile]       NVARCHAR (20)    NULL,
    [Email]        NVARCHAR (256)   NULL,
    [CodeHash]     NVARCHAR (500)   NOT NULL,
    [Purpose]      TINYINT          NOT NULL,
    [AttemptCount] INT              NOT NULL,
    [MaxAttempt]   INT              NOT NULL,
    [ExpiresAt]    DATETIME2 (7)    NOT NULL,
    [ConsumedAt]   DATETIME2 (7)    NULL,
    [IpAddress]    NVARCHAR (50)    NULL,
    [UserAgent]    NVARCHAR (1000)  NULL,
    [CreatedAt]    DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OtpCodes] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OtpCodes].[IX_OtpCodes_Email_Purpose_ExpiresAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_OtpCodes_Email_Purpose_ExpiresAt]
    ON [dbo].[OtpCodes]([Email] ASC, [Purpose] ASC, [ExpiresAt] ASC) WHERE ([Email] IS NOT NULL AND [ConsumedAt] IS NULL);


GO
PRINT N'Creating Index [dbo].[OtpCodes].[IX_OtpCodes_Mobile_Purpose_ExpiresAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_OtpCodes_Mobile_Purpose_ExpiresAt]
    ON [dbo].[OtpCodes]([Mobile] ASC, [Purpose] ASC, [ExpiresAt] ASC) WHERE ([Mobile] IS NOT NULL AND [ConsumedAt] IS NULL);


GO
PRINT N'Creating Index [dbo].[OtpCodes].[UX_OtpCodes_OneActive_Mobile_Purpose]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_OtpCodes_OneActive_Mobile_Purpose]
    ON [dbo].[OtpCodes]([Mobile] ASC, [Purpose] ASC) WHERE ([Mobile] IS NOT NULL AND [ConsumedAt] IS NULL);


GO
PRINT N'Creating Table [dbo].[OutboxMessages]...';


GO
CREATE TABLE [dbo].[OutboxMessages] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [AggregateId]   UNIQUEIDENTIFIER NULL,
    [AggregateType] NVARCHAR (200)   NULL,
    [MessageType]   NVARCHAR (300)   NOT NULL,
    [Payload]       NVARCHAR (MAX)   NOT NULL,
    [Status]        TINYINT          NOT NULL,
    [RetryCount]    INT              NOT NULL,
    [ErrorMessage]  NVARCHAR (2000)  NULL,
    [CreatedAt]     DATETIME2 (7)    NOT NULL,
    [ProcessedAt]   DATETIME2 (7)    NULL,
    [LockedAt]      DATETIME2 (7)    NULL,
    [LockId]        UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[OutboxMessages].[IX_OutboxMessages_Status_LockedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_OutboxMessages_Status_LockedAt]
    ON [dbo].[OutboxMessages]([Status] ASC, [LockedAt] ASC, [CreatedAt] ASC);


GO
PRINT N'Creating Index [dbo].[OutboxMessages].[IX_OutboxMessages_Status_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_OutboxMessages_Status_CreatedAt]
    ON [dbo].[OutboxMessages]([Status] ASC, [CreatedAt] ASC)
    INCLUDE([RetryCount]);


GO
PRINT N'Creating Table [dbo].[Pages]...';


GO
CREATE TABLE [dbo].[Pages] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [Title]          NVARCHAR (250)   NOT NULL,
    [Slug]           NVARCHAR (250)   NOT NULL,
    [ContentHtml]    NVARCHAR (MAX)   NOT NULL,
    [SeoTitle]       NVARCHAR (250)   NULL,
    [SeoDescription] NVARCHAR (500)   NULL,
    [IsPublished]    BIT              NOT NULL,
    [CreatedAt]      DATETIME2 (7)    NOT NULL,
    [UpdatedAt]      DATETIME2 (7)    NULL,
    [FocusKeyword]   NVARCHAR (200)   NULL,
    [IsSystem]       BIT              NOT NULL,
    CONSTRAINT [PK_Pages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Pages].[UX_Pages_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Pages_Slug]
    ON [dbo].[Pages]([Slug] ASC);


GO
PRINT N'Creating Table [dbo].[PaymentCallbacks]...';


GO
CREATE TABLE [dbo].[PaymentCallbacks] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [PaymentId]    UNIQUEIDENTIFIER NOT NULL,
    [CallbackData] NVARCHAR (MAX)   NOT NULL,
    [CreatedAt]    DATETIME2 (7)    NOT NULL,
    [CallbackKey]  CHAR (64)        COLLATE Latin1_General_100_BIN2 NULL,
    CONSTRAINT [PK_PaymentCallbacks] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[PaymentCallbacks].[UX_PaymentCallbacks_PaymentId_CallbackKey]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentCallbacks_PaymentId_CallbackKey]
    ON [dbo].[PaymentCallbacks]([PaymentId] ASC, [CallbackKey] ASC) WHERE ([CallbackKey] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[PaymentCallbacks].[IX_PaymentCallbacks_PaymentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_PaymentCallbacks_PaymentId]
    ON [dbo].[PaymentCallbacks]([PaymentId] ASC);


GO
PRINT N'Creating Table [dbo].[PaymentRefunds]...';


GO
CREATE TABLE [dbo].[PaymentRefunds] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [PaymentId]         UNIQUEIDENTIFIER NOT NULL,
    [OrderId]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [Amount]            DECIMAL (18, 2)  NOT NULL,
    [Method]            TINYINT          NOT NULL,
    [Status]            TINYINT          NOT NULL,
    [Reason]            NVARCHAR (1000)  NOT NULL,
    [IdempotencyKey]    NVARCHAR (100)   NOT NULL,
    [RequestedByUserId] UNIQUEIDENTIFIER NULL,
    [RequestedAt]       DATETIME2 (7)    NOT NULL,
    [CompletedAt]       DATETIME2 (7)    NULL,
    [FailureReason]     NVARCHAR (1000)  NULL,
    CONSTRAINT [PK_PaymentRefunds] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[PaymentRefunds].[IX_PaymentRefunds_Status_RequestedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_PaymentRefunds_Status_RequestedAt]
    ON [dbo].[PaymentRefunds]([Status] ASC, [RequestedAt] ASC);


GO
PRINT N'Creating Index [dbo].[PaymentRefunds].[UX_PaymentRefunds_Payment_Idempotency]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentRefunds_Payment_Idempotency]
    ON [dbo].[PaymentRefunds]([PaymentId] ASC, [IdempotencyKey] ASC);


GO
PRINT N'Creating Table [dbo].[Payments]...';


GO
CREATE TABLE [dbo].[Payments] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [OrderId]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]              UNIQUEIDENTIFIER NOT NULL,
    [Amount]              DECIMAL (18, 2)  NOT NULL,
    [Gateway]             NVARCHAR (100)   NOT NULL,
    [Authority]           NVARCHAR (300)   NULL,
    [GatewayTrackingCode] NVARCHAR (300)   NULL,
    [IdempotencyKey]      NVARCHAR (200)   NULL,
    [TransactionId]       NVARCHAR (200)   NULL,
    [ReferenceNumber]     NVARCHAR (200)   NULL,
    [Status]              TINYINT          NOT NULL,
    [ProviderStatusCode]  NVARCHAR (100)   NULL,
    [CallbackVerified]    BIT              NOT NULL,
    [RequestedAt]         DATETIME2 (7)    NOT NULL,
    [VerifiedAt]          DATETIME2 (7)    NULL,
    [UpdatedAt]           DATETIME2 (7)    NULL,
    [RawRequestData]      NVARCHAR (MAX)   NULL,
    [RawResponseData]     NVARCHAR (MAX)   NULL,
    [ErrorMessage]        NVARCHAR (1000)  NULL,
    [CurrencyType]        TINYINT          NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_Authority]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_Authority]
    ON [dbo].[Payments]([Authority] ASC) WHERE ([Authority] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_OrderId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_OrderId]
    ON [dbo].[Payments]([OrderId] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_OrderId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_OrderId_Status]
    ON [dbo].[Payments]([OrderId] ASC, [Status] ASC, [RequestedAt] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_TransactionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_TransactionId]
    ON [dbo].[Payments]([TransactionId] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_UserId]
    ON [dbo].[Payments]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[UX_Payments_Gateway_Authority]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payments_Gateway_Authority]
    ON [dbo].[Payments]([Gateway] ASC, [Authority] ASC) WHERE ([Authority] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Payments].[UX_Payments_IdempotencyKey]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payments_IdempotencyKey]
    ON [dbo].[Payments]([IdempotencyKey] ASC) WHERE ([IdempotencyKey] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[ProductCategories]...';


GO
CREATE TABLE [dbo].[ProductCategories] (
    [ProductId]  UNIQUEIDENTIFIER NOT NULL,
    [CategoryId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt]  DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_ProductCategories] PRIMARY KEY CLUSTERED ([ProductId] ASC, [CategoryId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductCategories].[IX_ProductCategories_CategoryId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductCategories_CategoryId]
    ON [dbo].[ProductCategories]([CategoryId] ASC)
    INCLUDE([ProductId]);


GO
PRINT N'Creating Table [dbo].[ProductFeatures]...';


GO
CREATE TABLE [dbo].[ProductFeatures] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [Title]     NVARCHAR (120)   NOT NULL,
    [Value]     NVARCHAR (500)   NOT NULL,
    [IconKey]   VARCHAR (64)     NULL,
    [SortOrder] INT              NOT NULL,
    [IsActive]  BIT              NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_ProductFeatures] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductFeatures].[IX_ProductFeatures_Product_Order]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductFeatures_Product_Order]
    ON [dbo].[ProductFeatures]([ProductId] ASC, [SortOrder] ASC, [Id] ASC);


GO
PRINT N'Creating Table [dbo].[ProductImages]...';


GO
CREATE TABLE [dbo].[ProductImages] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [ImagePath] NVARCHAR (500)   NOT NULL,
    [AltText]   NVARCHAR (250)   NULL,
    [SortOrder] INT              NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductImages].[IX_ProductImages_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductImages_ProductId]
    ON [dbo].[ProductImages]([ProductId] ASC);


GO
PRINT N'Creating Table [dbo].[ProductInputFields]...';


GO
CREATE TABLE [dbo].[ProductInputFields] (
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [ProductId]            UNIQUEIDENTIFIER NOT NULL,
    [Key]                  VARCHAR (64)     NOT NULL,
    [Label]                NVARCHAR (120)   NOT NULL,
    [Description]          NVARCHAR (500)   NULL,
    [Placeholder]          NVARCHAR (200)   NULL,
    [FieldType]            TINYINT          NOT NULL,
    [IsRequired]           BIT              NOT NULL,
    [OptionsJson]          NVARCHAR (MAX)   NULL,
    [DefaultValue]         NVARCHAR (2000)  NULL,
    [MinLength]            INT              NULL,
    [MaxLength]            INT              NULL,
    [ValidationPattern]    NVARCHAR (200)   NULL,
    [ValidationMessage]    NVARCHAR (300)   NULL,
    [IsSensitive]          BIT              NOT NULL,
    [RequiresConfirmation] BIT              NOT NULL,
    [DisplayStage]         TINYINT          NOT NULL,
    [SortOrder]            INT              NOT NULL,
    [IsActive]             BIT              NOT NULL,
    [CreatedAt]            DATETIME2 (7)    NOT NULL,
    [UpdatedAt]            DATETIME2 (7)    NULL,
    CONSTRAINT [PK_ProductInputFields] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductInputFields].[UX_ProductInputFields_Product_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductInputFields_Product_Key]
    ON [dbo].[ProductInputFields]([ProductId] ASC, [Key] ASC);


GO
PRINT N'Creating Index [dbo].[ProductInputFields].[IX_ProductInputFields_Product_Order]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductInputFields_Product_Order]
    ON [dbo].[ProductInputFields]([ProductId] ASC, [SortOrder] ASC, [Id] ASC);


GO
PRINT N'Creating Table [dbo].[ProductReviews]...';


GO
CREATE TABLE [dbo].[ProductReviews] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [ProductId]       UNIQUEIDENTIFIER NOT NULL,
    [UserId]          UNIQUEIDENTIFIER NOT NULL,
    [ParentId]        UNIQUEIDENTIFIER NULL,
    [Title]           NVARCHAR (200)   NULL,
    [Comment]         NVARCHAR (2000)  NOT NULL,
    [Rating]          TINYINT          NOT NULL,
    [IsApproved]      BIT              NOT NULL,
    [IsRejected]      BIT              NOT NULL,
    [RejectionReason] NVARCHAR (500)   NULL,
    [IsBuyer]         BIT              NOT NULL,
    [LikeCount]       INT              NOT NULL,
    [DislikeCount]    INT              NOT NULL,
    [CreatedAt]       DATETIME2 (7)    NOT NULL,
    [UpdatedAt]       DATETIME2 (7)    NULL,
    [IsDeleted]       BIT              NOT NULL,
    [DeletedAt]       DATETIME2 (7)    NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_IsApproved]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_IsApproved]
    ON [dbo].[ProductReviews]([IsApproved] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_ParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_ParentId]
    ON [dbo].[ProductReviews]([ParentId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_ProductId]
    ON [dbo].[ProductReviews]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_UserId]
    ON [dbo].[ProductReviews]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[ProductReviewVotes]...';


GO
CREATE TABLE [dbo].[ProductReviewVotes] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ReviewId]  UNIQUEIDENTIFIER NOT NULL,
    [UserId]    UNIQUEIDENTIFIER NOT NULL,
    [VoteType]  TINYINT          NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ProductReviewVotes_Review_User] UNIQUE NONCLUSTERED ([ReviewId] ASC, [UserId] ASC)
);


GO
PRINT N'Creating Table [dbo].[Products]...';


GO
CREATE TABLE [dbo].[Products] (
    [Id]                     UNIQUEIDENTIFIER NOT NULL,
    [CategoryId]             UNIQUEIDENTIFIER NOT NULL,
    [BrandId]                UNIQUEIDENTIFIER NULL,
    [Title]                  NVARCHAR (250)   NOT NULL,
    [Slug]                   NVARCHAR (300)   NOT NULL,
    [ShortDescription]       NVARCHAR (1000)  NULL,
    [FullDescription]        NVARCHAR (MAX)   NULL,
    [ProductType]            TINYINT          NOT NULL,
    [DeliveryType]           TINYINT          NOT NULL,
    [BasePrice]              DECIMAL (18, 2)  NOT NULL,
    [DiscountPrice]          DECIMAL (18, 2)  NULL,
    [CurrencyType]           TINYINT          NOT NULL,
    [RequiresVerification]   BIT              NOT NULL,
    [RequiresSupportMessage] BIT              NOT NULL,
    [MinOrderQuantity]       INT              NOT NULL,
    [MaxOrderQuantity]       INT              NULL,
    [IsFeatured]             BIT              NOT NULL,
    [IsActive]               BIT              NOT NULL,
    [SeoTitle]               NVARCHAR (250)   NULL,
    [SeoDescription]         NVARCHAR (500)   NULL,
    [ThumbnailImagePath]     NVARCHAR (500)   NULL,
    [SortOrder]              INT              NOT NULL,
    [CreatedAt]              DATETIME2 (7)    NOT NULL,
    [UpdatedAt]              DATETIME2 (7)    NULL,
    [IsDeleted]              BIT              NOT NULL,
    [DeletedAt]              DATETIME2 (7)    NULL,
    [FocusKeyword]           NVARCHAR (200)   NULL,
    [ThumbnailAltText]       NVARCHAR (250)   NULL,
    [KycRequirementMode]     TINYINT          NOT NULL,
    [KycThresholdAmount]     DECIMAL (18, 2)  NULL,
    [KycPolicyVersionId]     UNIQUEIDENTIFIER NULL,
    [ForceOutOfStock]        BIT              NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_BrandId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_BrandId]
    ON [dbo].[Products]([BrandId] ASC);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_ForceOutOfStock]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_ForceOutOfStock]
    ON [dbo].[Products]([ForceOutOfStock] ASC) WHERE ([ForceOutOfStock]=(1));


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_CategoryId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_CategoryId]
    ON [dbo].[Products]([CategoryId] ASC);


GO
PRINT N'Creating Index [dbo].[Products].[UX_Products_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Slug]
    ON [dbo].[Products]([Slug] ASC);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_KycPolicyVersionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_KycPolicyVersionId]
    ON [dbo].[Products]([KycPolicyVersionId] ASC) WHERE ([KycPolicyVersionId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[ProductTagMappings]...';


GO
CREATE TABLE [dbo].[ProductTagMappings] (
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [TagId]     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ProductTagMappings] PRIMARY KEY CLUSTERED ([ProductId] ASC, [TagId] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductTagMappings].[IX_ProductTagMappings_TagId_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductTagMappings_TagId_ProductId]
    ON [dbo].[ProductTagMappings]([TagId] ASC, [ProductId] ASC);


GO
PRINT N'Creating Table [dbo].[ProductTags]...';


GO
CREATE TABLE [dbo].[ProductTags] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [Title]     NVARCHAR (100)   NOT NULL,
    [Slug]      NVARCHAR (150)   NOT NULL,
    [Aliases]   NVARCHAR (1000)  NULL,
    [IsActive]  BIT              NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_ProductTags] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductTags].[UX_ProductTags_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductTags_Slug]
    ON [dbo].[ProductTags]([Slug] ASC);


GO
PRINT N'Creating Index [dbo].[ProductTags].[UX_ProductTags_Title]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductTags_Title]
    ON [dbo].[ProductTags]([Title] ASC);


GO
PRINT N'Creating Table [dbo].[ProductVariants]...';


GO
CREATE TABLE [dbo].[ProductVariants] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [ProductId]     UNIQUEIDENTIFIER NOT NULL,
    [Title]         NVARCHAR (200)   NOT NULL,
    [Sku]           NVARCHAR (100)   NULL,
    [Price]         DECIMAL (18, 2)  NOT NULL,
    [DiscountPrice] DECIMAL (18, 2)  NULL,
    [Value]         NVARCHAR (100)   NULL,
    [StockMode]     TINYINT          NOT NULL,
    [IsDefault]     BIT              NOT NULL,
    [IsActive]      BIT              NOT NULL,
    [SortOrder]     INT              NOT NULL,
    [CreatedAt]     DATETIME2 (7)    NOT NULL,
    [UpdatedAt]     DATETIME2 (7)    NULL,
    [StockQuantity] INT              NOT NULL,
    CONSTRAINT [PK_ProductVariants] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[ProductVariants].[UX_ProductVariants_Sku]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductVariants_Sku]
    ON [dbo].[ProductVariants]([Sku] ASC) WHERE ([Sku] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[ProductVariants].[IX_ProductVariants_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductVariants_ProductId]
    ON [dbo].[ProductVariants]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductVariants].[IX_ProductVariants_StockMode_StockQuantity]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductVariants_StockMode_StockQuantity]
    ON [dbo].[ProductVariants]([StockMode] ASC, [StockQuantity] ASC)
    INCLUDE([ProductId], [IsActive]);


GO
PRINT N'Creating Table [dbo].[Roles]...';


GO
CREATE TABLE [dbo].[Roles] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [Name]        NVARCHAR (100)   NOT NULL,
    [DisplayName] NVARCHAR (150)   NOT NULL,
    [CreatedAt]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Roles].[UX_Roles_Name]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Roles_Name]
    ON [dbo].[Roles]([Name] ASC);


GO
PRINT N'Creating Table [dbo].[SecurityLogs]...';


GO
CREATE TABLE [dbo].[SecurityLogs] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NULL,
    [EventType]    NVARCHAR (100)   NOT NULL,
    [Description]  NVARCHAR (1000)  NULL,
    [IpAddress]    NVARCHAR (50)    NULL,
    [UserAgent]    NVARCHAR (1000)  NULL,
    [IsSuccessful] BIT              NOT NULL,
    [CreatedAt]    DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_SecurityLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[SecurityLogs].[IX_SecurityLogs_UserId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SecurityLogs_UserId_CreatedAt]
    ON [dbo].[SecurityLogs]([UserId] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[SecurityLogs].[IX_SecurityLogs_EventType_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SecurityLogs_EventType_CreatedAt]
    ON [dbo].[SecurityLogs]([EventType] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Table [dbo].[Settings]...';


GO
CREATE TABLE [dbo].[Settings] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [Key]         NVARCHAR (200)   NOT NULL,
    [Value]       NVARCHAR (MAX)   NULL,
    [GroupName]   NVARCHAR (100)   NULL,
    [ValueType]   NVARCHAR (50)    NULL,
    [Description] NVARCHAR (500)   NULL,
    [UpdatedAt]   DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Settings] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Settings].[UX_Settings_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Settings_Key]
    ON [dbo].[Settings]([Key] ASC);


GO
PRINT N'Creating Table [dbo].[SmsMessageAttempts]...';


GO
CREATE TABLE [dbo].[SmsMessageAttempts] (
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [SmsMessageId]         UNIQUEIDENTIFIER NOT NULL,
    [AttemptNumber]        INT              NOT NULL,
    [Status]               TINYINT          NOT NULL,
    [ProviderMessageId]    NVARCHAR (200)   NULL,
    [ProviderErrorCode]    NVARCHAR (100)   NULL,
    [ProviderErrorMessage] NVARCHAR (1000)  NULL,
    [DeliveryCost]         DECIMAL (18, 2)  NULL,
    [AttemptedAt]          DATETIME2 (7)    NOT NULL,
    [CompletedAt]          DATETIME2 (7)    NULL,
    CONSTRAINT [PK_SmsMessageAttempts] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[SmsMessageAttempts].[UX_SmsMessageAttempts_Message_Attempt]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SmsMessageAttempts_Message_Attempt]
    ON [dbo].[SmsMessageAttempts]([SmsMessageId] ASC, [AttemptNumber] ASC);


GO
PRINT N'Creating Table [dbo].[SmsMessages]...';


GO
CREATE TABLE [dbo].[SmsMessages] (
    [Id]                     UNIQUEIDENTIFIER NOT NULL,
    [UserId]                 UNIQUEIDENTIFIER NULL,
    [Mobile]                 NVARCHAR (20)    NOT NULL,
    [MaskedMobile]           NVARCHAR (20)    NOT NULL,
    [Purpose]                NVARCHAR (100)   NOT NULL,
    [SendType]               TINYINT          NOT NULL,
    [TemplateKey]            NVARCHAR (100)   NULL,
    [TemplateId]             INT              NULL,
    [PublicReference]        NVARCHAR (150)   NULL,
    [SafeMessagePreview]     NVARCHAR (1000)  NULL,
    [InternalNote]           NVARCHAR (500)   NULL,
    [Provider]               NVARCHAR (50)    NOT NULL,
    [ProviderMessageId]      NVARCHAR (200)   NULL,
    [ProviderErrorCode]      NVARCHAR (100)   NULL,
    [ProviderErrorMessage]   NVARCHAR (1000)  NULL,
    [DeliveryCost]           DECIMAL (18, 2)  NULL,
    [Status]                 TINYINT          NOT NULL,
    [RetryCount]             INT              NOT NULL,
    [MaxRetryCount]          INT              NOT NULL,
    [CreatedAt]              DATETIME2 (7)    NOT NULL,
    [LastAttemptAt]          DATETIME2 (7)    NULL,
    [SentAt]                 DATETIME2 (7)    NULL,
    [FailedAt]               DATETIME2 (7)    NULL,
    [NextRetryAt]            DATETIME2 (7)    NULL,
    [CreatedByUserId]        UNIQUEIDENTIFIER NULL,
    [RelatedEntityType]      NVARCHAR (100)   NULL,
    [RelatedEntityId]        UNIQUEIDENTIFIER NULL,
    [RelatedEntityReference] NVARCHAR (150)   NULL,
    [IdempotencyKey]         NVARCHAR (200)   NOT NULL,
    [CorrelationId]          UNIQUEIDENTIFIER NOT NULL,
    [OutboxMessageId]        UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_SmsMessages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_PublicReference]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_PublicReference]
    ON [dbo].[SmsMessages]([PublicReference] ASC) WHERE ([PublicReference] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_OutboxMessageId]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_OutboxMessageId]
    ON [dbo].[SmsMessages]([OutboxMessageId] ASC) WHERE ([OutboxMessageId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[UX_SmsMessages_IdempotencyKey]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SmsMessages_IdempotencyKey]
    ON [dbo].[SmsMessages]([IdempotencyKey] ASC);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_SendType_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_SendType_CreatedAt]
    ON [dbo].[SmsMessages]([SendType] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_Mobile]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_Mobile]
    ON [dbo].[SmsMessages]([Mobile] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_Status_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_Status_CreatedAt]
    ON [dbo].[SmsMessages]([Status] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Table [dbo].[TicketMessages]...';


GO
CREATE TABLE [dbo].[TicketMessages] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [TicketId]       UNIQUEIDENTIFIER NOT NULL,
    [SenderUserId]   UNIQUEIDENTIFIER NOT NULL,
    [Message]        NVARCHAR (MAX)   NOT NULL,
    [AttachmentPath] NVARCHAR (500)   NULL,
    [IsInternalNote] BIT              NOT NULL,
    [CreatedAt]      DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_TicketMessages] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[TicketMessages].[IX_TicketMessages_TicketId]...';


GO
CREATE NONCLUSTERED INDEX [IX_TicketMessages_TicketId]
    ON [dbo].[TicketMessages]([TicketId] ASC);


GO
PRINT N'Creating Table [dbo].[Tickets]...';


GO
CREATE TABLE [dbo].[Tickets] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [UserId]              UNIQUEIDENTIFIER NOT NULL,
    [OrderId]             UNIQUEIDENTIFIER NULL,
    [Subject]             NVARCHAR (250)   NOT NULL,
    [Department]          TINYINT          NOT NULL,
    [Priority]            TINYINT          NOT NULL,
    [Status]              TINYINT          NOT NULL,
    [CreatedAt]           DATETIME2 (7)    NOT NULL,
    [UpdatedAt]           DATETIME2 (7)    NULL,
    [ClosedAt]            DATETIME2 (7)    NULL,
    [IsFulfillmentTicket] BIT              NOT NULL,
    CONSTRAINT [PK_Tickets] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Tickets].[UX_Tickets_OneFulfillmentPerOrder]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tickets_OneFulfillmentPerOrder]
    ON [dbo].[Tickets]([OrderId] ASC) WHERE ([IsFulfillmentTicket]=(1) AND [OrderId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Tickets].[IX_Tickets_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Tickets_UserId]
    ON [dbo].[Tickets]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[Tickets].[IX_Tickets_OrderId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Tickets_OrderId]
    ON [dbo].[Tickets]([OrderId] ASC);


GO
PRINT N'Creating Table [dbo].[UserAddresses]...';


GO
CREATE TABLE [dbo].[UserAddresses] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Title]        NVARCHAR (100)   NOT NULL,
    [ReceiverName] NVARCHAR (150)   NOT NULL,
    [PhoneNumber]  NVARCHAR (20)    NOT NULL,
    [Province]     NVARCHAR (100)   NULL,
    [City]         NVARCHAR (100)   NULL,
    [AddressLine]  NVARCHAR (1000)  NOT NULL,
    [PostalCode]   NVARCHAR (20)    NULL,
    [IsDefault]    BIT              NOT NULL,
    [CreatedAt]    DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_UserAddresses] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserAddresses].[IX_UserAddresses_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_UserAddresses_UserId]
    ON [dbo].[UserAddresses]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[UserRefreshTokens]...';


GO
CREATE TABLE [dbo].[UserRefreshTokens] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [UserId]              UNIQUEIDENTIFIER NOT NULL,
    [TokenHash]           NVARCHAR (500)   NOT NULL,
    [JwtId]               NVARCHAR (200)   NULL,
    [DeviceId]            NVARCHAR (200)   NULL,
    [IpAddress]           NVARCHAR (50)    NULL,
    [UserAgent]           NVARCHAR (1000)  NULL,
    [ExpiresAt]           DATETIME2 (7)    NOT NULL,
    [RevokedAt]           DATETIME2 (7)    NULL,
    [RevocationReason]    NVARCHAR (500)   NULL,
    [ReplacedByTokenHash] NVARCHAR (500)   NULL,
    [CreatedAt]           DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_UserRefreshTokens] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserRefreshTokens].[IX_UserRefreshTokens_UserId_ExpiresAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_UserRefreshTokens_UserId_ExpiresAt]
    ON [dbo].[UserRefreshTokens]([UserId] ASC, [ExpiresAt] ASC);


GO
PRINT N'Creating Index [dbo].[UserRefreshTokens].[UX_UserRefreshTokens_TokenHash]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserRefreshTokens_TokenHash]
    ON [dbo].[UserRefreshTokens]([TokenHash] ASC);


GO
PRINT N'Creating Table [dbo].[UserRoles]...';


GO
CREATE TABLE [dbo].[UserRoles] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [RoleId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC)
);


GO
PRINT N'Creating Table [dbo].[Users]...';


GO
CREATE TABLE [dbo].[Users] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [FullName]           NVARCHAR (200)   NOT NULL,
    [Mobile]             NVARCHAR (20)    NOT NULL,
    [Email]              NVARCHAR (256)   NULL,
    [PasswordHash]       NVARCHAR (MAX)   NOT NULL,
    [NationalCode]       NVARCHAR (20)    NULL,
    [AvatarPath]         NVARCHAR (500)   NULL,
    [Status]             TINYINT          NOT NULL,
    [VerificationStatus] TINYINT          NOT NULL,
    [IsMobileConfirmed]  BIT              NOT NULL,
    [IsEmailConfirmed]   BIT              NOT NULL,
    [LastLoginAt]        DATETIME2 (7)    NULL,
    [CreatedAt]          DATETIME2 (7)    NOT NULL,
    [UpdatedAt]          DATETIME2 (7)    NULL,
    [IsDeleted]          BIT              NOT NULL,
    [DeletedAt]          DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Users].[UX_Users_Mobile]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Mobile]
    ON [dbo].[Users]([Mobile] ASC);


GO
PRINT N'Creating Index [dbo].[Users].[UX_Users_Email]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_Email]
    ON [dbo].[Users]([Email] ASC) WHERE ([Email] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[UserVerificationProfiles]...';


GO
CREATE TABLE [dbo].[UserVerificationProfiles] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [FirstName]         NVARCHAR (100)   NOT NULL,
    [LastName]          NVARCHAR (100)   NOT NULL,
    [NationalCode]      NVARCHAR (20)    NOT NULL,
    [BirthDate]         DATE             NULL,
    [BankCardNumber]    NVARCHAR (30)    NULL,
    [ShabaNumber]       NVARCHAR (50)    NULL,
    [Address]           NVARCHAR (1000)  NULL,
    [PostalCode]        NVARCHAR (20)    NULL,
    [Status]            TINYINT          NOT NULL,
    [AdminNote]         NVARCHAR (1000)  NULL,
    [SubmittedAt]       DATETIME2 (7)    NULL,
    [ReviewedAt]        DATETIME2 (7)    NULL,
    [ReviewedByAdminId] UNIQUEIDENTIFIER NULL,
    [CreatedAt]         DATETIME2 (7)    NOT NULL,
    [UpdatedAt]         DATETIME2 (7)    NULL,
    [EncryptedPayload]  NVARCHAR (MAX)   NULL,
    [EncryptionVersion] SMALLINT         NULL,
    CONSTRAINT [PK_UserVerificationProfiles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[UserVerificationProfiles].[UX_UserVerificationProfiles_UserId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserVerificationProfiles_UserId]
    ON [dbo].[UserVerificationProfiles]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[VerificationDocuments]...';


GO
CREATE TABLE [dbo].[VerificationDocuments] (
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [UserVerificationProfileId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentType]              TINYINT          NOT NULL,
    [FilePath]                  NVARCHAR (500)   NOT NULL,
    [Status]                    TINYINT          NOT NULL,
    [AdminNote]                 NVARCHAR (1000)  NULL,
    [CreatedAt]                 DATETIME2 (7)    NOT NULL,
    [ReviewedAt]                DATETIME2 (7)    NULL,
    [ReviewedByAdminId]         UNIQUEIDENTIFIER NULL,
    [KycDocumentTypeId]         UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_VerificationDocuments] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[VerificationDocuments].[IX_VerificationDocuments_ProfileId]...';


GO
CREATE NONCLUSTERED INDEX [IX_VerificationDocuments_ProfileId]
    ON [dbo].[VerificationDocuments]([UserVerificationProfileId] ASC);


GO
PRINT N'Creating Index [dbo].[VerificationDocuments].[IX_VerificationDocuments_KycDocumentTypeId]...';


GO
CREATE NONCLUSTERED INDEX [IX_VerificationDocuments_KycDocumentTypeId]
    ON [dbo].[VerificationDocuments]([KycDocumentTypeId] ASC) WHERE ([KycDocumentTypeId] IS NOT NULL);


GO
PRINT N'Creating Table [dbo].[Wallets]...';


GO
CREATE TABLE [dbo].[Wallets] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [UserId]    UNIQUEIDENTIFIER NOT NULL,
    [Balance]   DECIMAL (18, 2)  NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    [UpdatedAt] DATETIME2 (7)    NULL,
    CONSTRAINT [PK_Wallets] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Wallets].[UX_Wallets_UserId]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Wallets_UserId]
    ON [dbo].[Wallets]([UserId] ASC);


GO
PRINT N'Creating Table [dbo].[WalletTopUps]...';


GO
CREATE TABLE [dbo].[WalletTopUps] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [UserId]          UNIQUEIDENTIFIER NOT NULL,
    [Amount]          DECIMAL (18, 2)  NOT NULL,
    [Gateway]         NVARCHAR (100)   NOT NULL,
    [Authority]       NVARCHAR (300)   NULL,
    [ReferenceNumber] NVARCHAR (200)   NULL,
    [Status]          TINYINT          NOT NULL,
    [ErrorMessage]    NVARCHAR (1000)  NULL,
    [RawResponseData] NVARCHAR (MAX)   NULL,
    [RequestedAt]     DATETIME2 (7)    NOT NULL,
    [VerifiedAt]      DATETIME2 (7)    NULL,
    [UpdatedAt]       DATETIME2 (7)    NULL,
    CONSTRAINT [PK_WalletTopUps] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[WalletTopUps].[IX_WalletTopUps_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WalletTopUps_UserId]
    ON [dbo].[WalletTopUps]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[WalletTopUps].[UX_WalletTopUps_Gateway_Authority]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_WalletTopUps_Gateway_Authority]
    ON [dbo].[WalletTopUps]([Gateway] ASC, [Authority] ASC) WHERE ([Authority] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[WalletTopUps].[IX_WalletTopUps_Authority]...';


GO
CREATE NONCLUSTERED INDEX [IX_WalletTopUps_Authority]
    ON [dbo].[WalletTopUps]([Authority] ASC);


GO
PRINT N'Creating Table [dbo].[WalletTransactions]...';


GO
CREATE TABLE [dbo].[WalletTransactions] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [WalletId]      UNIQUEIDENTIFIER NOT NULL,
    [UserId]        UNIQUEIDENTIFIER NOT NULL,
    [Type]          TINYINT          NOT NULL,
    [Amount]        DECIMAL (18, 2)  NOT NULL,
    [BalanceAfter]  DECIMAL (18, 2)  NOT NULL,
    [ReferenceType] TINYINT          NULL,
    [ReferenceId]   UNIQUEIDENTIFIER NULL,
    [Description]   NVARCHAR (1000)  NULL,
    [CreatedAt]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_WalletTransactions] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[WalletTransactions].[UX_WalletTransactions_FinancialReference]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_WalletTransactions_FinancialReference]
    ON [dbo].[WalletTransactions]([UserId] ASC, [ReferenceType] ASC, [ReferenceId] ASC, [Type] ASC) WHERE ([ReferenceType] IS NOT NULL AND [ReferenceId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[WalletTransactions].[IX_WalletTransactions_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WalletTransactions_UserId]
    ON [dbo].[WalletTransactions]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[WalletTransactions].[IX_WalletTransactions_WalletId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WalletTransactions_WalletId]
    ON [dbo].[WalletTransactions]([WalletId] ASC);


GO
PRINT N'Creating Table [dbo].[WishList]...';


GO
CREATE TABLE [dbo].[WishList] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [UserId]    UNIQUEIDENTIFIER NOT NULL,
    [ProductId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 (7)    NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_WishList_User_Product] UNIQUE NONCLUSTERED ([UserId] ASC, [ProductId] ASC)
);


GO
PRINT N'Creating Index [dbo].[WishList].[IX_WishList_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WishList_ProductId]
    ON [dbo].[WishList]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[WishList].[IX_WishList_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WishList_UserId]
    ON [dbo].[WishList]([UserId] ASC);


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[AuditLogs]...';


GO
ALTER TABLE [dbo].[AuditLogs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[AuditLogs]...';


GO
ALTER TABLE [dbo].[AuditLogs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Banners]...';


GO
ALTER TABLE [dbo].[Banners]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Banners]...';


GO
ALTER TABLE [dbo].[Banners]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Banners]...';


GO
ALTER TABLE [dbo].[Banners]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Banners]...';


GO
ALTER TABLE [dbo].[Banners]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[BlogPosts]...';


GO
ALTER TABLE [dbo].[BlogPosts]
    ADD DEFAULT ((0)) FOR [IsPublished];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[BlogPosts]...';


GO
ALTER TABLE [dbo].[BlogPosts]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[BlogPosts]...';


GO
ALTER TABLE [dbo].[BlogPosts]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Brands]...';


GO
ALTER TABLE [dbo].[Brands]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Brands]...';


GO
ALTER TABLE [dbo].[Brands]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Brands]...';


GO
ALTER TABLE [dbo].[Brands]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItemInputValues_IsSensitive]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [DF_CartItemInputValues_IsSensitive] DEFAULT ((0)) FOR [IsSensitive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItemInputValues_Id]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [DF_CartItemInputValues_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItemInputValues_CreatedAt]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [DF_CartItemInputValues_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [DF_CartItems_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CartItems]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItems_InputFingerprint]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [DF_CartItems_InputFingerprint] DEFAULT ('NONE') FOR [InputFingerprint];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CartItems]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CartItems]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD DEFAULT ((1)) FOR [Quantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Carts]...';


GO
ALTER TABLE [dbo].[Carts]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Carts]...';


GO
ALTER TABLE [dbo].[Carts]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT ((0)) FOR [UsedCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CouponUsages]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CouponUsages]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD DEFAULT (sysutcdatetime()) FOR [UsedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DatabaseScriptHistory_Success]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory]
    ADD CONSTRAINT [DF_DatabaseScriptHistory_Success] DEFAULT ((1)) FOR [Success];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DatabaseScriptHistory_AppliedBy]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory]
    ADD CONSTRAINT [DF_DatabaseScriptHistory_AppliedBy] DEFAULT (original_login()) FOR [AppliedBy];


GO
PRINT N'Creating Default Constraint [dbo].[DF_DatabaseScriptHistory_AppliedAt]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory]
    ADD CONSTRAINT [DF_DatabaseScriptHistory_AppliedAt] DEFAULT (sysutcdatetime()) FOR [AppliedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ErrorLogs]...';


GO
ALTER TABLE [dbo].[ErrorLogs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ErrorLogs]...';


GO
ALTER TABLE [dbo].[ErrorLogs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FinancialAuditLogs_CreatedAt]...';


GO
ALTER TABLE [dbo].[FinancialAuditLogs]
    ADD CONSTRAINT [DF_FinancialAuditLogs_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_SizeBytes]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_SizeBytes] DEFAULT ((0)) FOR [SizeBytes];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_Scope]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_Scope] DEFAULT ((3)) FOR [Scope];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_Id]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_IsBuiltIn]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_IsBuiltIn] DEFAULT ((0)) FOR [IsBuiltIn];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_IsActive]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_IsActive] DEFAULT ((0)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_FontAssets_CreatedAt]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [DF_FontAssets_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeBatches]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD DEFAULT (sysutcdatetime()) FOR [ImportedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeBatches]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT ((1)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT (sysutcdatetime()) FOR [ReservedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT ((1)) FOR [EncryptionVersion];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[IdempotencyKeys]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_IdempotencyKeys_Status]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD CONSTRAINT [DF_IdempotencyKeys_Status] DEFAULT ((1)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[IdempotencyKeys]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycDocumentTypes_AllowedExtensions]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [DF_KycDocumentTypes_AllowedExtensions] DEFAULT (N'jpg,jpeg,png,webp') FOR [AllowedExtensions];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycDocumentTypes_MaxFileSizeBytes]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [DF_KycDocumentTypes_MaxFileSizeBytes] DEFAULT ((5242880)) FOR [MaxFileSizeBytes];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycDocumentTypes_CreatedAt]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [DF_KycDocumentTypes_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycDocumentTypes_IsActive]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [DF_KycDocumentTypes_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycDocumentTypes_SortOrder]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [DF_KycDocumentTypes_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicies_IsActive]...';


GO
ALTER TABLE [dbo].[KycPolicies]
    ADD CONSTRAINT [DF_KycPolicies_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicies_CreatedAt]...';


GO
ALTER TABLE [dbo].[KycPolicies]
    ADD CONSTRAINT [DF_KycPolicies_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicyDocumentRequirements_IsRequired]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [DF_KycPolicyDocumentRequirements_IsRequired] DEFAULT ((1)) FOR [IsRequired];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicyDocumentRequirements_RedactionMode]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [DF_KycPolicyDocumentRequirements_RedactionMode] DEFAULT ((0)) FOR [RedactionMode];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicyDocumentRequirements_SortOrder]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [DF_KycPolicyDocumentRequirements_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicyVersions_CreatedAt]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions]
    ADD CONSTRAINT [DF_KycPolicyVersions_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_KycPolicyVersions_Status]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions]
    ADD CONSTRAINT [DF_KycPolicyVersions_Status] DEFAULT ((1)) FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LegacyRedirects_StatusCode]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [DF_LegacyRedirects_StatusCode] DEFAULT ((301)) FOR [StatusCode];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LegacyRedirects_CreatedAt]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [DF_LegacyRedirects_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LegacyRedirects_Id]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [DF_LegacyRedirects_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_LegacyRedirects_IsActive]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [DF_LegacyRedirects_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_NotificationBroadcasts_Status]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [DF_NotificationBroadcasts_Status] DEFAULT ((1)) FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_NotificationBroadcasts_Id]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [DF_NotificationBroadcasts_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_NotificationBroadcasts_RecipientCount]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [DF_NotificationBroadcasts_RecipientCount] DEFAULT ((0)) FOR [RecipientCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_NotificationBroadcasts_CreatedAt]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [DF_NotificationBroadcasts_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Notifications]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Notifications]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD DEFAULT ((0)) FOR [Type];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Notifications]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Notifications]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD DEFAULT ((0)) FOR [IsRead];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT ((0)) FOR [DeliveryType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT ((1)) FOR [IsVisibleToCustomer];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemInputValues_CreatedAt]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [DF_OrderItemInputValues_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemInputValues_IsSensitive]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [DF_OrderItemInputValues_IsSensitive] DEFAULT ((0)) FOR [IsSensitive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemInputValues_Id]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [DF_OrderItemInputValues_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemKycFinanceResolutions_Id]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions]
    ADD CONSTRAINT [DF_OrderItemKycFinanceResolutions_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemKycFinanceResolutions_CreatedAt]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions]
    ADD CONSTRAINT [DF_OrderItemKycFinanceResolutions_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemKycStates_CreatedAt]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [DF_OrderItemKycStates_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemKycStates_Id]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [DF_OrderItemKycStates_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItemKycStates_UpdatedAt]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [DF_OrderItemKycStates_UpdatedAt] DEFAULT (sysutcdatetime()) FOR [UpdatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItems_KycRequirementMode]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [DF_OrderItems_KycRequirementMode] DEFAULT ((0)) FOR [KycRequirementMode];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [DeliveryType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [RequiresVerification];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [TotalPrice];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [UnitPrice];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItems_KycEvaluatedAmount]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [DF_OrderItems_KycEvaluatedAmount] DEFAULT ((0)) FOR [KycEvaluatedAmount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [DeliveryStatus];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [DF_OrderItems_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((1)) FOR [Quantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [SubtotalAmount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatRatePercent]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatRatePercent] DEFAULT ((0)) FOR [VatRatePercent];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [PaymentStatus];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [DiscountAmount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatEnabled]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatEnabled] DEFAULT ((0)) FOR [VatEnabled];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatCalculationMode]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatCalculationMode] DEFAULT ((1)) FOR [VatCalculationMode];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_CurrencyType]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [FinalAmount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatTaxableAmount]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatTaxableAmount] DEFAULT ((0)) FOR [VatTaxableAmount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatAmount]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatAmount] DEFAULT ((0)) FOR [VatAmount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderStatusHistories]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderStatusHistories]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OtpCodes]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OtpCodes]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD DEFAULT ((0)) FOR [AttemptCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OtpCodes]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OtpCodes]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD DEFAULT ((5)) FOR [MaxAttempt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT ((0)) FOR [RetryCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Pages]...';


GO
ALTER TABLE [dbo].[Pages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Pages]...';


GO
ALTER TABLE [dbo].[Pages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Pages_IsSystem]...';


GO
ALTER TABLE [dbo].[Pages]
    ADD CONSTRAINT [DF_Pages_IsSystem] DEFAULT ((0)) FOR [IsSystem];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Pages]...';


GO
ALTER TABLE [dbo].[Pages]
    ADD DEFAULT ((1)) FOR [IsPublished];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[PaymentCallbacks]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[PaymentCallbacks]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_PaymentRefunds_Id]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [DF_PaymentRefunds_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_PaymentRefunds_RequestedAt]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [DF_PaymentRefunds_RequestedAt] DEFAULT (sysutcdatetime()) FOR [RequestedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT (sysutcdatetime()) FOR [RequestedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Payments_CurrencyType]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD CONSTRAINT [DF_Payments_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT ((0)) FOR [CallbackVerified];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductCategories_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductCategories]
    ADD CONSTRAINT [DF_ProductCategories_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductFeatures_SortOrder]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [DF_ProductFeatures_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductFeatures_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [DF_ProductFeatures_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductFeatures_IsActive]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [DF_ProductFeatures_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductFeatures_Id]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [DF_ProductFeatures_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductImages]...';


GO
ALTER TABLE [dbo].[ProductImages]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductImages]...';


GO
ALTER TABLE [dbo].[ProductImages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductImages]...';


GO
ALTER TABLE [dbo].[ProductImages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_Id]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_IsSensitive]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_IsSensitive] DEFAULT ((0)) FOR [IsSensitive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_SortOrder]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_IsRequired]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_IsRequired] DEFAULT ((0)) FOR [IsRequired];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_DisplayStage]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_DisplayStage] DEFAULT ((1)) FOR [DisplayStage];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_IsActive]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_RequiresConfirmation]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_RequiresConfirmation] DEFAULT ((0)) FOR [RequiresConfirmation];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [LikeCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsApproved];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsBuyer];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [DislikeCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsRejected];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviewVotes]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviewVotes]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((1)) FOR [MinOrderQuantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [DeliveryType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Products_ForceOutOfStock]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [DF_Products_ForceOutOfStock] DEFAULT ((0)) FOR [ForceOutOfStock];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [RequiresSupportMessage];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Products_KycRequirementMode]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [DF_Products_KycRequirementMode] DEFAULT ((0)) FOR [KycRequirementMode];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [IsFeatured];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [RequiresVerification];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [ProductType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [BasePrice];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductTags_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD CONSTRAINT [DF_ProductTags_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductTags]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductTags_IsActive]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD CONSTRAINT [DF_ProductTags_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((0)) FOR [StockMode];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductVariants_StockQuantity]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD CONSTRAINT [DF_ProductVariants_StockQuantity] DEFAULT ((0)) FOR [StockQuantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((0)) FOR [Price];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((0)) FOR [IsDefault];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Roles]...';


GO
ALTER TABLE [dbo].[Roles]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Roles]...';


GO
ALTER TABLE [dbo].[Roles]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[SecurityLogs]...';


GO
ALTER TABLE [dbo].[SecurityLogs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[SecurityLogs]...';


GO
ALTER TABLE [dbo].[SecurityLogs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Settings]...';


GO
ALTER TABLE [dbo].[Settings]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessageAttempts_AttemptedAt]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [DF_SmsMessageAttempts_AttemptedAt] DEFAULT (sysutcdatetime()) FOR [AttemptedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessageAttempts_Id]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [DF_SmsMessageAttempts_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_CorrelationId]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_CorrelationId] DEFAULT (newid()) FOR [CorrelationId];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_RetryCount]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_RetryCount] DEFAULT ((0)) FOR [RetryCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_Id]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_Status]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_Status] DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_MaxRetryCount]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_MaxRetryCount] DEFAULT ((5)) FOR [MaxRetryCount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_CreatedAt]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT ((0)) FOR [IsInternalNote];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Tickets_IsFulfillmentTicket]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [DF_Tickets_IsFulfillmentTicket] DEFAULT ((0)) FOR [IsFulfillmentTicket];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((1)) FOR [Priority];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((0)) FOR [Department];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserAddresses]...';


GO
ALTER TABLE [dbo].[UserAddresses]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserAddresses]...';


GO
ALTER TABLE [dbo].[UserAddresses]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserAddresses]...';


GO
ALTER TABLE [dbo].[UserAddresses]
    ADD DEFAULT ((0)) FOR [IsDefault];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserRefreshTokens]...';


GO
ALTER TABLE [dbo].[UserRefreshTokens]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserRefreshTokens]...';


GO
ALTER TABLE [dbo].[UserRefreshTokens]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [IsEmailConfirmed];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [VerificationStatus];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [IsMobileConfirmed];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[VerificationDocuments]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[VerificationDocuments]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[VerificationDocuments]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Wallets]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Wallets]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD DEFAULT ((0)) FOR [Balance];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Wallets]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[WalletTransactions]...';


GO
ALTER TABLE [dbo].[WalletTransactions]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[WalletTransactions]...';


GO
ALTER TABLE [dbo].[WalletTransactions]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[WishList]...';


GO
ALTER TABLE [dbo].[WishList]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[WishList]...';


GO
ALTER TABLE [dbo].[WishList]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Foreign Key [dbo].[FK_AuditLogs_Users]...';


GO
ALTER TABLE [dbo].[AuditLogs] WITH NOCHECK
    ADD CONSTRAINT [FK_AuditLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItemInputValues_CartItems]...';


GO
ALTER TABLE [dbo].[CartItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [FK_CartItemInputValues_CartItems] FOREIGN KEY ([CartItemId]) REFERENCES [dbo].[CartItems] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItemInputValues_ProductInputFields]...';


GO
ALTER TABLE [dbo].[CartItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [FK_CartItemInputValues_ProductInputFields] FOREIGN KEY ([ProductInputFieldId]) REFERENCES [dbo].[ProductInputFields] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_Carts]...';


GO
ALTER TABLE [dbo].[CartItems] WITH NOCHECK
    ADD CONSTRAINT [FK_CartItems_Carts] FOREIGN KEY ([CartId]) REFERENCES [dbo].[Carts] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_Products]...';


GO
ALTER TABLE [dbo].[CartItems] WITH NOCHECK
    ADD CONSTRAINT [FK_CartItems_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_ProductVariants]...';


GO
ALTER TABLE [dbo].[CartItems] WITH NOCHECK
    ADD CONSTRAINT [FK_CartItems_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Carts_Users]...';


GO
ALTER TABLE [dbo].[Carts] WITH NOCHECK
    ADD CONSTRAINT [FK_Carts_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Categories_Parent]...';


GO
ALTER TABLE [dbo].[Categories] WITH NOCHECK
    ADD CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Categories] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Users]...';


GO
ALTER TABLE [dbo].[CouponUsages] WITH NOCHECK
    ADD CONSTRAINT [FK_CouponUsages_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Coupons]...';


GO
ALTER TABLE [dbo].[CouponUsages] WITH NOCHECK
    ADD CONSTRAINT [FK_CouponUsages_Coupons] FOREIGN KEY ([CouponId]) REFERENCES [dbo].[Coupons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Orders]...';


GO
ALTER TABLE [dbo].[CouponUsages] WITH NOCHECK
    ADD CONSTRAINT [FK_CouponUsages_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_FAQs_Products_ProductId]...';


GO
ALTER TABLE [dbo].[FAQs] WITH NOCHECK
    ADD CONSTRAINT [FK_FAQs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_FinancialAuditLogs_Users]...';


GO
ALTER TABLE [dbo].[FinancialAuditLogs] WITH NOCHECK
    ADD CONSTRAINT [FK_FinancialAuditLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_FontAssets_CreatedByUser]...';


GO
ALTER TABLE [dbo].[FontAssets] WITH NOCHECK
    ADD CONSTRAINT [FK_FontAssets_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_Products]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeBatches_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_ImportedByAdmin]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeBatches_ImportedByAdmin] FOREIGN KEY ([ImportedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeBatches_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Users]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Orders]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Products]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_GiftCodes] FOREIGN KEY ([GiftCodeId]) REFERENCES [dbo].[GiftCodes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_OrderItems]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodeReservations_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodes_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_Products]...';


GO
ALTER TABLE [dbo].[GiftCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodes_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_OrderItems]...';


GO
ALTER TABLE [dbo].[GiftCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodes_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_ReservedByUser]...';


GO
ALTER TABLE [dbo].[GiftCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodes_ReservedByUser] FOREIGN KEY ([ReservedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_GiftCodeBatches]...';


GO
ALTER TABLE [dbo].[GiftCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_GiftCodes_GiftCodeBatches] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[GiftCodeBatches] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_IdempotencyKeys_Users]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys] WITH NOCHECK
    ADD CONSTRAINT [FK_IdempotencyKeys_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyDocumentRequirements_KycDocumentTypes]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH NOCHECK
    ADD CONSTRAINT [FK_KycPolicyDocumentRequirements_KycDocumentTypes] FOREIGN KEY ([KycDocumentTypeId]) REFERENCES [dbo].[KycDocumentTypes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyDocumentRequirements_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH NOCHECK
    ADD CONSTRAINT [FK_KycPolicyDocumentRequirements_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyVersions_KycPolicies]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions] WITH NOCHECK
    ADD CONSTRAINT [FK_KycPolicyVersions_KycPolicies] FOREIGN KEY ([KycPolicyId]) REFERENCES [dbo].[KycPolicies] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_NotificationBroadcasts_Users]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts] WITH NOCHECK
    ADD CONSTRAINT [FK_NotificationBroadcasts_Users] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Notifications_Users]...';


GO
ALTER TABLE [dbo].[Notifications] WITH NOCHECK
    ADD CONSTRAINT [FK_Notifications_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Notifications_NotificationBroadcasts]...';


GO
ALTER TABLE [dbo].[Notifications] WITH NOCHECK
    ADD CONSTRAINT [FK_Notifications_NotificationBroadcasts] FOREIGN KEY ([BroadcastId]) REFERENCES [dbo].[NotificationBroadcasts] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_GiftCodes]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemDeliveries_GiftCodes] FOREIGN KEY ([GiftCodeId]) REFERENCES [dbo].[GiftCodes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_DeliveredByUser]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemDeliveries_DeliveredByUser] FOREIGN KEY ([DeliveredByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemDeliveries_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemInputValues_ProductInputFields]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemInputValues_ProductInputFields] FOREIGN KEY ([ProductInputFieldId]) REFERENCES [dbo].[ProductInputFields] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemInputValues_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemInputValues_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycFinanceResolutions_ResolvedBy]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemKycFinanceResolutions_ResolvedBy] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycFinanceResolutions_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemKycFinanceResolutions_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycStates_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemKycStates_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycStates_SatisfiedByVerificationProfile]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItemKycStates_SatisfiedByVerificationProfile] FOREIGN KEY ([SatisfiedByVerificationProfileId]) REFERENCES [dbo].[UserVerificationProfiles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Orders]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_ProductVariants]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItems_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Products]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItems_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Tickets]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItems_Tickets] FOREIGN KEY ([SupportTicketId]) REFERENCES [dbo].[Tickets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderItems_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Orders_Coupons]...';


GO
ALTER TABLE [dbo].[Orders] WITH NOCHECK
    ADD CONSTRAINT [FK_Orders_Coupons] FOREIGN KEY ([CouponId]) REFERENCES [dbo].[Coupons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Orders_Users]...';


GO
ALTER TABLE [dbo].[Orders] WITH NOCHECK
    ADD CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderStatusHistories_Orders]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderStatusHistories_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderStatusHistories_ChangedByUser]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories] WITH NOCHECK
    ADD CONSTRAINT [FK_OrderStatusHistories_ChangedByUser] FOREIGN KEY ([ChangedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OtpCodes_Users]...';


GO
ALTER TABLE [dbo].[OtpCodes] WITH NOCHECK
    ADD CONSTRAINT [FK_OtpCodes_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentCallbacks_Payments]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks] WITH NOCHECK
    ADD CONSTRAINT [FK_PaymentCallbacks_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Users]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [FK_PaymentRefunds_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_RequestedBy]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [FK_PaymentRefunds_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Payments]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [FK_PaymentRefunds_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Orders]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [FK_PaymentRefunds_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Payments_Users]...';


GO
ALTER TABLE [dbo].[Payments] WITH NOCHECK
    ADD CONSTRAINT [FK_Payments_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Payments_Orders]...';


GO
ALTER TABLE [dbo].[Payments] WITH NOCHECK
    ADD CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductCategories_Products]...';


GO
ALTER TABLE [dbo].[ProductCategories] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductCategories_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductCategories_Categories]...';


GO
ALTER TABLE [dbo].[ProductCategories] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductCategories_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductFeatures_Products]...';


GO
ALTER TABLE [dbo].[ProductFeatures] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductFeatures_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductImages_Products]...';


GO
ALTER TABLE [dbo].[ProductImages] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductImages_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductInputFields_Products]...';


GO
ALTER TABLE [dbo].[ProductInputFields] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductInputFields_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_Product]...';


GO
ALTER TABLE [dbo].[ProductReviews] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductReviews_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_Parent]...';


GO
ALTER TABLE [dbo].[ProductReviews] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductReviews_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[ProductReviews] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_User]...';


GO
ALTER TABLE [dbo].[ProductReviews] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductReviews_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviewVotes_Review]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductReviewVotes_Review] FOREIGN KEY ([ReviewId]) REFERENCES [dbo].[ProductReviews] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviewVotes_User]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductReviewVotes_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[Products] WITH NOCHECK
    ADD CONSTRAINT [FK_Products_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_Brands]...';


GO
ALTER TABLE [dbo].[Products] WITH NOCHECK
    ADD CONSTRAINT [FK_Products_Brands] FOREIGN KEY ([BrandId]) REFERENCES [dbo].[Brands] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_Categories]...';


GO
ALTER TABLE [dbo].[Products] WITH NOCHECK
    ADD CONSTRAINT [FK_Products_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductTagMappings_ProductTags]...';


GO
ALTER TABLE [dbo].[ProductTagMappings] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductTagMappings_ProductTags] FOREIGN KEY ([TagId]) REFERENCES [dbo].[ProductTags] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductTagMappings_Products]...';


GO
ALTER TABLE [dbo].[ProductTagMappings] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductTagMappings_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductVariants_Products]...';


GO
ALTER TABLE [dbo].[ProductVariants] WITH NOCHECK
    ADD CONSTRAINT [FK_ProductVariants_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SecurityLogs_Users]...';


GO
ALTER TABLE [dbo].[SecurityLogs] WITH NOCHECK
    ADD CONSTRAINT [FK_SecurityLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessageAttempts_SmsMessage]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts] WITH NOCHECK
    ADD CONSTRAINT [FK_SmsMessageAttempts_SmsMessage] FOREIGN KEY ([SmsMessageId]) REFERENCES [dbo].[SmsMessages] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_CreatedByUser]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [FK_SmsMessages_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_Outbox]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [FK_SmsMessages_Outbox] FOREIGN KEY ([OutboxMessageId]) REFERENCES [dbo].[OutboxMessages] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_User]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [FK_SmsMessages_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TicketMessages_Tickets]...';


GO
ALTER TABLE [dbo].[TicketMessages] WITH NOCHECK
    ADD CONSTRAINT [FK_TicketMessages_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TicketMessages_Users]...';


GO
ALTER TABLE [dbo].[TicketMessages] WITH NOCHECK
    ADD CONSTRAINT [FK_TicketMessages_Users] FOREIGN KEY ([SenderUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Tickets_Orders]...';


GO
ALTER TABLE [dbo].[Tickets] WITH NOCHECK
    ADD CONSTRAINT [FK_Tickets_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Tickets_Users]...';


GO
ALTER TABLE [dbo].[Tickets] WITH NOCHECK
    ADD CONSTRAINT [FK_Tickets_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserAddresses_Users]...';


GO
ALTER TABLE [dbo].[UserAddresses] WITH NOCHECK
    ADD CONSTRAINT [FK_UserAddresses_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRefreshTokens_Users]...';


GO
ALTER TABLE [dbo].[UserRefreshTokens] WITH NOCHECK
    ADD CONSTRAINT [FK_UserRefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Roles]...';


GO
ALTER TABLE [dbo].[UserRoles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Users]...';


GO
ALTER TABLE [dbo].[UserRoles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserVerificationProfiles_Users]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserVerificationProfiles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserVerificationProfiles_ReviewedByAdmin]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles] WITH NOCHECK
    ADD CONSTRAINT [FK_UserVerificationProfiles_ReviewedByAdmin] FOREIGN KEY ([ReviewedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[VerificationDocuments] WITH NOCHECK
    ADD CONSTRAINT [FK_VerificationDocuments_UserVerificationProfiles] FOREIGN KEY ([UserVerificationProfileId]) REFERENCES [dbo].[UserVerificationProfiles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_KycDocumentTypes]...';


GO
ALTER TABLE [dbo].[VerificationDocuments] WITH NOCHECK
    ADD CONSTRAINT [FK_VerificationDocuments_KycDocumentTypes] FOREIGN KEY ([KycDocumentTypeId]) REFERENCES [dbo].[KycDocumentTypes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_ReviewedByAdmin]...';


GO
ALTER TABLE [dbo].[VerificationDocuments] WITH NOCHECK
    ADD CONSTRAINT [FK_VerificationDocuments_ReviewedByAdmin] FOREIGN KEY ([ReviewedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Wallets_Users]...';


GO
ALTER TABLE [dbo].[Wallets] WITH NOCHECK
    ADD CONSTRAINT [FK_Wallets_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTopUps_User]...';


GO
ALTER TABLE [dbo].[WalletTopUps] WITH NOCHECK
    ADD CONSTRAINT [FK_WalletTopUps_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTransactions_Wallets]...';


GO
ALTER TABLE [dbo].[WalletTransactions] WITH NOCHECK
    ADD CONSTRAINT [FK_WalletTransactions_Wallets] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTransactions_Users]...';


GO
ALTER TABLE [dbo].[WalletTransactions] WITH NOCHECK
    ADD CONSTRAINT [FK_WalletTransactions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WishList_User]...';


GO
ALTER TABLE [dbo].[WishList] WITH NOCHECK
    ADD CONSTRAINT [FK_WishList_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WishList_Product]...';


GO
ALTER TABLE [dbo].[WishList] WITH NOCHECK
    ADD CONSTRAINT [FK_WishList_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_CartItemInputValues_SensitiveStorage]...';


GO
ALTER TABLE [dbo].[CartItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [CK_CartItemInputValues_SensitiveStorage] CHECK ([IsSensitive]=(0) AND [EncryptedValue] IS NULL OR [IsSensitive]=(1) AND [Value] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_CartItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[CartItems] WITH NOCHECK
    ADD CONSTRAINT [CK_CartItems_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Carts_ExactlyOneOwner]...';


GO
ALTER TABLE [dbo].[Carts] WITH NOCHECK
    ADD CONSTRAINT [CK_Carts_ExactlyOneOwner] CHECK ([UserId] IS NOT NULL AND [GuestTokenHash] IS NULL OR [UserId] IS NULL AND [GuestTokenHash] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_DatabaseScriptHistory_Hash]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory] WITH NOCHECK
    ADD CONSTRAINT [CK_DatabaseScriptHistory_Hash] CHECK (len([ScriptHash])=(64) AND NOT [ScriptHash] like '%[^0-9a-f]%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_DatabaseScriptHistory_Success]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory] WITH NOCHECK
    ADD CONSTRAINT [CK_DatabaseScriptHistory_Success] CHECK ([Success]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Format]...';


GO
ALTER TABLE [dbo].[FontAssets] WITH NOCHECK
    ADD CONSTRAINT [CK_FontAssets_Format] CHECK ([FileFormat]='ttf' OR [FileFormat]='woff' OR [FileFormat]='woff2');


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Path]...';


GO
ALTER TABLE [dbo].[FontAssets] WITH NOCHECK
    ADD CONSTRAINT [CK_FontAssets_Path] CHECK ([IsBuiltIn]=(1) AND [FilePath] IS NULL OR [IsBuiltIn]=(0) AND [FilePath] like '/uploads/fonts/%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Scope]...';


GO
ALTER TABLE [dbo].[FontAssets] WITH NOCHECK
    ADD CONSTRAINT [CK_FontAssets_Scope] CHECK ([Scope]=(3) OR [Scope]=(2) OR [Scope]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_GiftCodeReservations_Status]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations] WITH NOCHECK
    ADD CONSTRAINT [CK_GiftCodeReservations_Status] CHECK ([Status]>=(0) AND [Status]<=(3));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycDocumentTypes_MaxFileSizeBytes]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes] WITH NOCHECK
    ADD CONSTRAINT [CK_KycDocumentTypes_MaxFileSizeBytes] CHECK ([MaxFileSizeBytes]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyDocumentRequirements_RedactionMode]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH NOCHECK
    ADD CONSTRAINT [CK_KycPolicyDocumentRequirements_RedactionMode] CHECK ([RedactionMode]>=(0) AND [RedactionMode]<=(2));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyVersions_CustomerActionDeadlineHours]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions] WITH NOCHECK
    ADD CONSTRAINT [CK_KycPolicyVersions_CustomerActionDeadlineHours] CHECK ([CustomerActionDeadlineHours] IS NULL OR [CustomerActionDeadlineHours]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyVersions_Status]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions] WITH NOCHECK
    ADD CONSTRAINT [CK_KycPolicyVersions_Status] CHECK ([Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_Destination]...';


GO
ALTER TABLE [dbo].[LegacyRedirects] WITH NOCHECK
    ADD CONSTRAINT [CK_LegacyRedirects_Destination] CHECK (([StatusCode]=(308) OR [StatusCode]=(301)) AND [DestinationPath] IS NOT NULL AND left([DestinationPath],(1))=N'/' OR [StatusCode]=(410) AND [DestinationPath] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_SourcePath]...';


GO
ALTER TABLE [dbo].[LegacyRedirects] WITH NOCHECK
    ADD CONSTRAINT [CK_LegacyRedirects_SourcePath] CHECK (left([SourcePath],(1))=N'/' AND NOT [SourcePath] like N'%?%' AND NOT [SourcePath] like N'%#%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_StatusCode]...';


GO
ALTER TABLE [dbo].[LegacyRedirects] WITH NOCHECK
    ADD CONSTRAINT [CK_LegacyRedirects_StatusCode] CHECK ([StatusCode]=(410) OR [StatusCode]=(308) OR [StatusCode]=(301));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_Status]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts] WITH NOCHECK
    ADD CONSTRAINT [CK_NotificationBroadcasts_Status] CHECK ([Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_RecipientCount]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts] WITH NOCHECK
    ADD CONSTRAINT [CK_NotificationBroadcasts_RecipientCount] CHECK ([RecipientCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_AudienceType]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts] WITH NOCHECK
    ADD CONSTRAINT [CK_NotificationBroadcasts_AudienceType] CHECK ([AudienceType]=(2) OR [AudienceType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemDeliveries_ManualKey]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItemDeliveries_ManualKey] CHECK (([DeliveryType]=(3) OR [DeliveryType]=(2)) AND [ManualDeliveryItemKey]=[OrderItemId] OR NOT ([DeliveryType]=(3) OR [DeliveryType]=(2)) AND [ManualDeliveryItemKey] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemInputValues_SensitiveStorage]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItemInputValues_SensitiveStorage] CHECK ([IsSensitive]=(0) AND [EncryptedValue] IS NULL OR [IsSensitive]=(1) AND [Value] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemKycFinanceResolutions_Status]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItemKycFinanceResolutions_Status] CHECK ([Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemKycStates_Status]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItemKycStates_Status] CHECK ([Status]=(7) OR [Status]=(6) OR [Status]=(5) OR [Status]=(4) OR [Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_KycCustomerActionDeadlineHours]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItems_KycCustomerActionDeadlineHours] CHECK ([KycCustomerActionDeadlineHours] IS NULL OR [KycCustomerActionDeadlineHours]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItems_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_Quantity]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_Prices]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItems_Prices] CHECK ([UnitPrice]>=(0) AND [TotalPrice]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_KycSnapshot]...';


GO
ALTER TABLE [dbo].[OrderItems] WITH NOCHECK
    ADD CONSTRAINT [CK_OrderItems_KycSnapshot] CHECK (([KycRequirementMode]=(2) OR [KycRequirementMode]=(1) OR [KycRequirementMode]=(0)) AND [KycEvaluatedAmount]>=(0) AND ([KycRequirementMode]=(0) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NULL OR [KycRequirementMode]=(1) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NOT NULL OR [KycRequirementMode]=(2) AND [KycThresholdAmount]>(0) AND [KycPolicyVersionId] IS NOT NULL));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_CurrencyType]...';


GO
ALTER TABLE [dbo].[Orders] WITH NOCHECK
    ADD CONSTRAINT [CK_Orders_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_VatSnapshot]...';


GO
ALTER TABLE [dbo].[Orders] WITH NOCHECK
    ADD CONSTRAINT [CK_Orders_VatSnapshot] CHECK ([VatRatePercent]>=(0) AND [VatRatePercent]<=(100) AND [VatAmount]>=(0) AND [VatTaxableAmount]>=(0) AND ([VatCalculationMode]=(2) OR [VatCalculationMode]=(1)) AND ([VatEnabled]=(1) OR [VatAmount]=(0) AND [VatRatePercent]=(0)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_Amounts]...';


GO
ALTER TABLE [dbo].[Orders] WITH NOCHECK
    ADD CONSTRAINT [CK_Orders_Amounts] CHECK ([SubtotalAmount]>=(0) AND [DiscountAmount]>=(0) AND [FinalAmount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OtpCodes_AttemptCount]...';


GO
ALTER TABLE [dbo].[OtpCodes] WITH NOCHECK
    ADD CONSTRAINT [CK_OtpCodes_AttemptCount] CHECK ([AttemptCount]>=(0) AND [MaxAttempt]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OutboxMessages_RetryCount]...';


GO
ALTER TABLE [dbo].[OutboxMessages] WITH NOCHECK
    ADD CONSTRAINT [CK_OutboxMessages_RetryCount] CHECK ([RetryCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OutboxMessages_Status]...';


GO
ALTER TABLE [dbo].[OutboxMessages] WITH NOCHECK
    ADD CONSTRAINT [CK_OutboxMessages_Status] CHECK ([Status]>=(0) AND [Status]<=(3));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Method]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [CK_PaymentRefunds_Method] CHECK ([Method]=(2) OR [Method]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Amount]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [CK_PaymentRefunds_Amount] CHECK ([Amount]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Status]...';


GO
ALTER TABLE [dbo].[PaymentRefunds] WITH NOCHECK
    ADD CONSTRAINT [CK_PaymentRefunds_Status] CHECK ([Status]=(4) OR [Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Payments_Amount]...';


GO
ALTER TABLE [dbo].[Payments] WITH NOCHECK
    ADD CONSTRAINT [CK_Payments_Amount] CHECK ([Amount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Payments_CurrencyType]...';


GO
ALTER TABLE [dbo].[Payments] WITH NOCHECK
    ADD CONSTRAINT [CK_Payments_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductFeatures_Title_NotBlank]...';


GO
ALTER TABLE [dbo].[ProductFeatures] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductFeatures_Title_NotBlank] CHECK (len(ltrim(rtrim([Title])))>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductFeatures_Value_NotBlank]...';


GO
ALTER TABLE [dbo].[ProductFeatures] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductFeatures_Value_NotBlank] CHECK (len(ltrim(rtrim([Value])))>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Stage]...';


GO
ALTER TABLE [dbo].[ProductInputFields] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductInputFields_Stage] CHECK ([DisplayStage]=(2) OR [DisplayStage]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Type]...';


GO
ALTER TABLE [dbo].[ProductInputFields] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductInputFields_Type] CHECK ([FieldType]>=(1) AND [FieldType]<=(12));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Length]...';


GO
ALTER TABLE [dbo].[ProductInputFields] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductInputFields_Length] CHECK ([MinLength] IS NULL OR [MinLength]>=(0) AND ([MaxLength] IS NULL OR [MaxLength]>=[MinLength]));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductReviews_Rating]...';


GO
ALTER TABLE [dbo].[ProductReviews] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductReviews_Rating] CHECK ([Rating]>=(1) AND [Rating]<=(5));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductReviewVotes_VoteType]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductReviewVotes_VoteType] CHECK ([VoteType]=(2) OR [VoteType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Products_KycConfiguration]...';


GO
ALTER TABLE [dbo].[Products] WITH NOCHECK
    ADD CONSTRAINT [CK_Products_KycConfiguration] CHECK ([KycRequirementMode]=(0) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NULL OR [KycRequirementMode]=(1) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NOT NULL OR [KycRequirementMode]=(2) AND [KycThresholdAmount]>(0) AND [KycPolicyVersionId] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductVariants_StockQuantity_NonNegative]...';


GO
ALTER TABLE [dbo].[ProductVariants] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductVariants_StockQuantity_NonNegative] CHECK ([StockQuantity]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductVariants_Prices]...';


GO
ALTER TABLE [dbo].[ProductVariants] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductVariants_Prices] CHECK ([Price]>=(0) AND ([DiscountPrice] IS NULL OR [DiscountPrice]>=(0)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessageAttempts_Status]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts] WITH NOCHECK
    ADD CONSTRAINT [CK_SmsMessageAttempts_Status] CHECK ([Status]>=(0) AND [Status]<=(7));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessageAttempts_Number]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts] WITH NOCHECK
    ADD CONSTRAINT [CK_SmsMessageAttempts_Number] CHECK ([AttemptNumber]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_SendType]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [CK_SmsMessages_SendType] CHECK ([SendType]=(3) OR [SendType]=(2) OR [SendType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_RetryCount]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [CK_SmsMessages_RetryCount] CHECK ([RetryCount]>=(0) AND ([MaxRetryCount]>=(1) AND [MaxRetryCount]<=(10)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_Status]...';


GO
ALTER TABLE [dbo].[SmsMessages] WITH NOCHECK
    ADD CONSTRAINT [CK_SmsMessages_Status] CHECK ([Status]>=(0) AND [Status]<=(7));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Wallets_Balance]...';


GO
ALTER TABLE [dbo].[Wallets] WITH NOCHECK
    ADD CONSTRAINT [CK_Wallets_Balance] CHECK ([Balance]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_WalletTransactions_Amount]...';


GO
ALTER TABLE [dbo].[WalletTransactions] WITH NOCHECK
    ADD CONSTRAINT [CK_WalletTransactions_Amount] CHECK ([Amount]>=(0));


GO
PRINT N'Creating Trigger [dbo].[TR_DatabaseScriptHistory_Immutable]...';


GO

CREATE   TRIGGER dbo.TR_DatabaseScriptHistory_Immutable
ON dbo.DatabaseScriptHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51004, 'Database script history is immutable. UPDATE and DELETE are not permitted.', 1;
END;
GO
PRINT N'Creating Trigger [dbo].[TR_FinancialAuditLogs_Immutable]...';


GO

CREATE   TRIGGER dbo.TR_FinancialAuditLogs_Immutable
ON dbo.FinancialAuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51310, 'Financial audit history is immutable.', 1;
END;
GO
PRINT N'Creating Procedure [dbo].[usp_PurgeSmsHistory]...';


GO

-- Safeguarded retention procedure. It never removes active messages. @BeforeUtc
-- must be supplied explicitly by a privileged operator/job after reviewing retention policy.
CREATE   PROCEDURE dbo.usp_PurgeSmsHistory
    @BeforeUtc datetime2(7),
    @BatchSize int = 1000
AS
BEGIN
    SET NOCOUNT ON;
    IF @BeforeUtc IS NULL OR @BatchSize NOT BETWEEN 1 AND 5000
        THROW 50001, 'A valid cutoff and batch size are required.', 1;

    DELETE TOP (@BatchSize)
    FROM dbo.SmsMessages
    WHERE CreatedAt < @BeforeUtc
      AND Status IN (2,3,5,6,7);

    SELECT @@ROWCOUNT AS DeletedRows;
END;
GO
PRINT N'Checking existing data against newly created constraints';


GO


GO
ALTER TABLE [dbo].[AuditLogs] WITH CHECK CHECK CONSTRAINT [FK_AuditLogs_Users];

ALTER TABLE [dbo].[CartItemInputValues] WITH CHECK CHECK CONSTRAINT [FK_CartItemInputValues_CartItems];

ALTER TABLE [dbo].[CartItemInputValues] WITH CHECK CHECK CONSTRAINT [FK_CartItemInputValues_ProductInputFields];

ALTER TABLE [dbo].[CartItems] WITH CHECK CHECK CONSTRAINT [FK_CartItems_Carts];

ALTER TABLE [dbo].[CartItems] WITH CHECK CHECK CONSTRAINT [FK_CartItems_Products];

ALTER TABLE [dbo].[CartItems] WITH CHECK CHECK CONSTRAINT [FK_CartItems_ProductVariants];

ALTER TABLE [dbo].[Carts] WITH CHECK CHECK CONSTRAINT [FK_Carts_Users];

ALTER TABLE [dbo].[Categories] WITH CHECK CHECK CONSTRAINT [FK_Categories_Parent];

ALTER TABLE [dbo].[CouponUsages] WITH CHECK CHECK CONSTRAINT [FK_CouponUsages_Users];

ALTER TABLE [dbo].[CouponUsages] WITH CHECK CHECK CONSTRAINT [FK_CouponUsages_Coupons];

ALTER TABLE [dbo].[CouponUsages] WITH CHECK CHECK CONSTRAINT [FK_CouponUsages_Orders];

ALTER TABLE [dbo].[FAQs] WITH CHECK CHECK CONSTRAINT [FK_FAQs_Products_ProductId];

ALTER TABLE [dbo].[FinancialAuditLogs] WITH CHECK CHECK CONSTRAINT [FK_FinancialAuditLogs_Users];

ALTER TABLE [dbo].[FontAssets] WITH CHECK CHECK CONSTRAINT [FK_FontAssets_CreatedByUser];

ALTER TABLE [dbo].[GiftCodeBatches] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeBatches_Products];

ALTER TABLE [dbo].[GiftCodeBatches] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeBatches_ImportedByAdmin];

ALTER TABLE [dbo].[GiftCodeBatches] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeBatches_ProductVariants];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_ProductVariants];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_Users];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_Orders];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_Products];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_GiftCodes];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [FK_GiftCodeReservations_OrderItems];

ALTER TABLE [dbo].[GiftCodes] WITH CHECK CHECK CONSTRAINT [FK_GiftCodes_ProductVariants];

ALTER TABLE [dbo].[GiftCodes] WITH CHECK CHECK CONSTRAINT [FK_GiftCodes_Products];

ALTER TABLE [dbo].[GiftCodes] WITH CHECK CHECK CONSTRAINT [FK_GiftCodes_OrderItems];

ALTER TABLE [dbo].[GiftCodes] WITH CHECK CHECK CONSTRAINT [FK_GiftCodes_ReservedByUser];

ALTER TABLE [dbo].[GiftCodes] WITH CHECK CHECK CONSTRAINT [FK_GiftCodes_GiftCodeBatches];

ALTER TABLE [dbo].[IdempotencyKeys] WITH CHECK CHECK CONSTRAINT [FK_IdempotencyKeys_Users];

ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH CHECK CHECK CONSTRAINT [FK_KycPolicyDocumentRequirements_KycDocumentTypes];

ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH CHECK CHECK CONSTRAINT [FK_KycPolicyDocumentRequirements_KycPolicyVersions];

ALTER TABLE [dbo].[KycPolicyVersions] WITH CHECK CHECK CONSTRAINT [FK_KycPolicyVersions_KycPolicies];

ALTER TABLE [dbo].[NotificationBroadcasts] WITH CHECK CHECK CONSTRAINT [FK_NotificationBroadcasts_Users];

ALTER TABLE [dbo].[Notifications] WITH CHECK CHECK CONSTRAINT [FK_Notifications_Users];

ALTER TABLE [dbo].[Notifications] WITH CHECK CHECK CONSTRAINT [FK_Notifications_NotificationBroadcasts];

ALTER TABLE [dbo].[OrderItemDeliveries] WITH CHECK CHECK CONSTRAINT [FK_OrderItemDeliveries_GiftCodes];

ALTER TABLE [dbo].[OrderItemDeliveries] WITH CHECK CHECK CONSTRAINT [FK_OrderItemDeliveries_DeliveredByUser];

ALTER TABLE [dbo].[OrderItemDeliveries] WITH CHECK CHECK CONSTRAINT [FK_OrderItemDeliveries_OrderItems];

ALTER TABLE [dbo].[OrderItemInputValues] WITH CHECK CHECK CONSTRAINT [FK_OrderItemInputValues_ProductInputFields];

ALTER TABLE [dbo].[OrderItemInputValues] WITH CHECK CHECK CONSTRAINT [FK_OrderItemInputValues_OrderItems];

ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH CHECK CHECK CONSTRAINT [FK_OrderItemKycFinanceResolutions_ResolvedBy];

ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH CHECK CHECK CONSTRAINT [FK_OrderItemKycFinanceResolutions_OrderItems];

ALTER TABLE [dbo].[OrderItemKycStates] WITH CHECK CHECK CONSTRAINT [FK_OrderItemKycStates_OrderItems];

ALTER TABLE [dbo].[OrderItemKycStates] WITH CHECK CHECK CONSTRAINT [FK_OrderItemKycStates_SatisfiedByVerificationProfile];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [FK_OrderItems_Orders];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [FK_OrderItems_ProductVariants];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [FK_OrderItems_Products];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [FK_OrderItems_Tickets];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [FK_OrderItems_KycPolicyVersions];

ALTER TABLE [dbo].[Orders] WITH CHECK CHECK CONSTRAINT [FK_Orders_Coupons];

ALTER TABLE [dbo].[Orders] WITH CHECK CHECK CONSTRAINT [FK_Orders_Users];

ALTER TABLE [dbo].[OrderStatusHistories] WITH CHECK CHECK CONSTRAINT [FK_OrderStatusHistories_Orders];

ALTER TABLE [dbo].[OrderStatusHistories] WITH CHECK CHECK CONSTRAINT [FK_OrderStatusHistories_ChangedByUser];

ALTER TABLE [dbo].[OtpCodes] WITH CHECK CHECK CONSTRAINT [FK_OtpCodes_Users];

ALTER TABLE [dbo].[PaymentCallbacks] WITH CHECK CHECK CONSTRAINT [FK_PaymentCallbacks_Payments];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [FK_PaymentRefunds_Users];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [FK_PaymentRefunds_RequestedBy];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [FK_PaymentRefunds_Payments];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [FK_PaymentRefunds_Orders];

ALTER TABLE [dbo].[Payments] WITH CHECK CHECK CONSTRAINT [FK_Payments_Users];

ALTER TABLE [dbo].[Payments] WITH CHECK CHECK CONSTRAINT [FK_Payments_Orders];

ALTER TABLE [dbo].[ProductCategories] WITH CHECK CHECK CONSTRAINT [FK_ProductCategories_Products];

ALTER TABLE [dbo].[ProductCategories] WITH CHECK CHECK CONSTRAINT [FK_ProductCategories_Categories];

ALTER TABLE [dbo].[ProductFeatures] WITH CHECK CHECK CONSTRAINT [FK_ProductFeatures_Products];

ALTER TABLE [dbo].[ProductImages] WITH CHECK CHECK CONSTRAINT [FK_ProductImages_Products];

ALTER TABLE [dbo].[ProductInputFields] WITH CHECK CHECK CONSTRAINT [FK_ProductInputFields_Products];

ALTER TABLE [dbo].[ProductReviews] WITH CHECK CHECK CONSTRAINT [FK_ProductReviews_Product];

ALTER TABLE [dbo].[ProductReviews] WITH CHECK CHECK CONSTRAINT [FK_ProductReviews_Parent];

ALTER TABLE [dbo].[ProductReviews] WITH CHECK CHECK CONSTRAINT [FK_ProductReviews_User];

ALTER TABLE [dbo].[ProductReviewVotes] WITH CHECK CHECK CONSTRAINT [FK_ProductReviewVotes_Review];

ALTER TABLE [dbo].[ProductReviewVotes] WITH CHECK CHECK CONSTRAINT [FK_ProductReviewVotes_User];

ALTER TABLE [dbo].[Products] WITH CHECK CHECK CONSTRAINT [FK_Products_KycPolicyVersions];

ALTER TABLE [dbo].[Products] WITH CHECK CHECK CONSTRAINT [FK_Products_Brands];

ALTER TABLE [dbo].[Products] WITH CHECK CHECK CONSTRAINT [FK_Products_Categories];

ALTER TABLE [dbo].[ProductTagMappings] WITH CHECK CHECK CONSTRAINT [FK_ProductTagMappings_ProductTags];

ALTER TABLE [dbo].[ProductTagMappings] WITH CHECK CHECK CONSTRAINT [FK_ProductTagMappings_Products];

ALTER TABLE [dbo].[ProductVariants] WITH CHECK CHECK CONSTRAINT [FK_ProductVariants_Products];

ALTER TABLE [dbo].[SecurityLogs] WITH CHECK CHECK CONSTRAINT [FK_SecurityLogs_Users];

ALTER TABLE [dbo].[SmsMessageAttempts] WITH CHECK CHECK CONSTRAINT [FK_SmsMessageAttempts_SmsMessage];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [FK_SmsMessages_CreatedByUser];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [FK_SmsMessages_Outbox];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [FK_SmsMessages_User];

ALTER TABLE [dbo].[TicketMessages] WITH CHECK CHECK CONSTRAINT [FK_TicketMessages_Tickets];

ALTER TABLE [dbo].[TicketMessages] WITH CHECK CHECK CONSTRAINT [FK_TicketMessages_Users];

ALTER TABLE [dbo].[Tickets] WITH CHECK CHECK CONSTRAINT [FK_Tickets_Orders];

ALTER TABLE [dbo].[Tickets] WITH CHECK CHECK CONSTRAINT [FK_Tickets_Users];

ALTER TABLE [dbo].[UserAddresses] WITH CHECK CHECK CONSTRAINT [FK_UserAddresses_Users];

ALTER TABLE [dbo].[UserRefreshTokens] WITH CHECK CHECK CONSTRAINT [FK_UserRefreshTokens_Users];

ALTER TABLE [dbo].[UserRoles] WITH CHECK CHECK CONSTRAINT [FK_UserRoles_Roles];

ALTER TABLE [dbo].[UserRoles] WITH CHECK CHECK CONSTRAINT [FK_UserRoles_Users];

ALTER TABLE [dbo].[UserVerificationProfiles] WITH CHECK CHECK CONSTRAINT [FK_UserVerificationProfiles_Users];

ALTER TABLE [dbo].[UserVerificationProfiles] WITH CHECK CHECK CONSTRAINT [FK_UserVerificationProfiles_ReviewedByAdmin];

ALTER TABLE [dbo].[VerificationDocuments] WITH CHECK CHECK CONSTRAINT [FK_VerificationDocuments_UserVerificationProfiles];

ALTER TABLE [dbo].[VerificationDocuments] WITH CHECK CHECK CONSTRAINT [FK_VerificationDocuments_KycDocumentTypes];

ALTER TABLE [dbo].[VerificationDocuments] WITH CHECK CHECK CONSTRAINT [FK_VerificationDocuments_ReviewedByAdmin];

ALTER TABLE [dbo].[Wallets] WITH CHECK CHECK CONSTRAINT [FK_Wallets_Users];

ALTER TABLE [dbo].[WalletTopUps] WITH CHECK CHECK CONSTRAINT [FK_WalletTopUps_User];

ALTER TABLE [dbo].[WalletTransactions] WITH CHECK CHECK CONSTRAINT [FK_WalletTransactions_Wallets];

ALTER TABLE [dbo].[WalletTransactions] WITH CHECK CHECK CONSTRAINT [FK_WalletTransactions_Users];

ALTER TABLE [dbo].[WishList] WITH CHECK CHECK CONSTRAINT [FK_WishList_User];

ALTER TABLE [dbo].[WishList] WITH CHECK CHECK CONSTRAINT [FK_WishList_Product];

ALTER TABLE [dbo].[CartItemInputValues] WITH CHECK CHECK CONSTRAINT [CK_CartItemInputValues_SensitiveStorage];

ALTER TABLE [dbo].[CartItems] WITH CHECK CHECK CONSTRAINT [CK_CartItems_CurrencyType];

ALTER TABLE [dbo].[Carts] WITH CHECK CHECK CONSTRAINT [CK_Carts_ExactlyOneOwner];

ALTER TABLE [dbo].[DatabaseScriptHistory] WITH CHECK CHECK CONSTRAINT [CK_DatabaseScriptHistory_Hash];

ALTER TABLE [dbo].[DatabaseScriptHistory] WITH CHECK CHECK CONSTRAINT [CK_DatabaseScriptHistory_Success];

ALTER TABLE [dbo].[FontAssets] WITH CHECK CHECK CONSTRAINT [CK_FontAssets_Format];

ALTER TABLE [dbo].[FontAssets] WITH CHECK CHECK CONSTRAINT [CK_FontAssets_Path];

ALTER TABLE [dbo].[FontAssets] WITH CHECK CHECK CONSTRAINT [CK_FontAssets_Scope];

ALTER TABLE [dbo].[GiftCodeReservations] WITH CHECK CHECK CONSTRAINT [CK_GiftCodeReservations_Status];

ALTER TABLE [dbo].[KycDocumentTypes] WITH CHECK CHECK CONSTRAINT [CK_KycDocumentTypes_MaxFileSizeBytes];

ALTER TABLE [dbo].[KycPolicyDocumentRequirements] WITH CHECK CHECK CONSTRAINT [CK_KycPolicyDocumentRequirements_RedactionMode];

ALTER TABLE [dbo].[KycPolicyVersions] WITH CHECK CHECK CONSTRAINT [CK_KycPolicyVersions_CustomerActionDeadlineHours];

ALTER TABLE [dbo].[KycPolicyVersions] WITH CHECK CHECK CONSTRAINT [CK_KycPolicyVersions_Status];

ALTER TABLE [dbo].[LegacyRedirects] WITH CHECK CHECK CONSTRAINT [CK_LegacyRedirects_Destination];

ALTER TABLE [dbo].[LegacyRedirects] WITH CHECK CHECK CONSTRAINT [CK_LegacyRedirects_SourcePath];

ALTER TABLE [dbo].[LegacyRedirects] WITH CHECK CHECK CONSTRAINT [CK_LegacyRedirects_StatusCode];

ALTER TABLE [dbo].[NotificationBroadcasts] WITH CHECK CHECK CONSTRAINT [CK_NotificationBroadcasts_Status];

ALTER TABLE [dbo].[NotificationBroadcasts] WITH CHECK CHECK CONSTRAINT [CK_NotificationBroadcasts_RecipientCount];

ALTER TABLE [dbo].[NotificationBroadcasts] WITH CHECK CHECK CONSTRAINT [CK_NotificationBroadcasts_AudienceType];

ALTER TABLE [dbo].[OrderItemDeliveries] WITH CHECK CHECK CONSTRAINT [CK_OrderItemDeliveries_ManualKey];

ALTER TABLE [dbo].[OrderItemInputValues] WITH CHECK CHECK CONSTRAINT [CK_OrderItemInputValues_SensitiveStorage];

ALTER TABLE [dbo].[OrderItemKycFinanceResolutions] WITH CHECK CHECK CONSTRAINT [CK_OrderItemKycFinanceResolutions_Status];

ALTER TABLE [dbo].[OrderItemKycStates] WITH CHECK CHECK CONSTRAINT [CK_OrderItemKycStates_Status];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [CK_OrderItems_KycCustomerActionDeadlineHours];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [CK_OrderItems_CurrencyType];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [CK_OrderItems_Quantity];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [CK_OrderItems_Prices];

ALTER TABLE [dbo].[OrderItems] WITH CHECK CHECK CONSTRAINT [CK_OrderItems_KycSnapshot];

ALTER TABLE [dbo].[Orders] WITH CHECK CHECK CONSTRAINT [CK_Orders_CurrencyType];

ALTER TABLE [dbo].[Orders] WITH CHECK CHECK CONSTRAINT [CK_Orders_VatSnapshot];

ALTER TABLE [dbo].[Orders] WITH CHECK CHECK CONSTRAINT [CK_Orders_Amounts];

ALTER TABLE [dbo].[OtpCodes] WITH CHECK CHECK CONSTRAINT [CK_OtpCodes_AttemptCount];

ALTER TABLE [dbo].[OutboxMessages] WITH CHECK CHECK CONSTRAINT [CK_OutboxMessages_RetryCount];

ALTER TABLE [dbo].[OutboxMessages] WITH CHECK CHECK CONSTRAINT [CK_OutboxMessages_Status];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [CK_PaymentRefunds_Method];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [CK_PaymentRefunds_Amount];

ALTER TABLE [dbo].[PaymentRefunds] WITH CHECK CHECK CONSTRAINT [CK_PaymentRefunds_Status];

ALTER TABLE [dbo].[Payments] WITH CHECK CHECK CONSTRAINT [CK_Payments_Amount];

ALTER TABLE [dbo].[Payments] WITH CHECK CHECK CONSTRAINT [CK_Payments_CurrencyType];

ALTER TABLE [dbo].[ProductFeatures] WITH CHECK CHECK CONSTRAINT [CK_ProductFeatures_Title_NotBlank];

ALTER TABLE [dbo].[ProductFeatures] WITH CHECK CHECK CONSTRAINT [CK_ProductFeatures_Value_NotBlank];

ALTER TABLE [dbo].[ProductInputFields] WITH CHECK CHECK CONSTRAINT [CK_ProductInputFields_Stage];

ALTER TABLE [dbo].[ProductInputFields] WITH CHECK CHECK CONSTRAINT [CK_ProductInputFields_Type];

ALTER TABLE [dbo].[ProductInputFields] WITH CHECK CHECK CONSTRAINT [CK_ProductInputFields_Length];

ALTER TABLE [dbo].[ProductReviews] WITH CHECK CHECK CONSTRAINT [CK_ProductReviews_Rating];

ALTER TABLE [dbo].[ProductReviewVotes] WITH CHECK CHECK CONSTRAINT [CK_ProductReviewVotes_VoteType];

ALTER TABLE [dbo].[Products] WITH CHECK CHECK CONSTRAINT [CK_Products_KycConfiguration];

ALTER TABLE [dbo].[ProductVariants] WITH CHECK CHECK CONSTRAINT [CK_ProductVariants_StockQuantity_NonNegative];

ALTER TABLE [dbo].[ProductVariants] WITH CHECK CHECK CONSTRAINT [CK_ProductVariants_Prices];

ALTER TABLE [dbo].[SmsMessageAttempts] WITH CHECK CHECK CONSTRAINT [CK_SmsMessageAttempts_Status];

ALTER TABLE [dbo].[SmsMessageAttempts] WITH CHECK CHECK CONSTRAINT [CK_SmsMessageAttempts_Number];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [CK_SmsMessages_SendType];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [CK_SmsMessages_RetryCount];

ALTER TABLE [dbo].[SmsMessages] WITH CHECK CHECK CONSTRAINT [CK_SmsMessages_Status];

ALTER TABLE [dbo].[Wallets] WITH CHECK CHECK CONSTRAINT [CK_Wallets_Balance];

ALTER TABLE [dbo].[WalletTransactions] WITH CHECK CHECK CONSTRAINT [CK_WalletTransactions_Amount];


GO
PRINT N'Update complete.';


GO

PRINT N'Seeding dbo.Roles (5 row(s))...';
GO
INSERT INTO dbo.[Roles] ([Id], [Name], [DisplayName], [CreatedAt]) VALUES
  ('fd9f1aad-55d0-4a16-a067-5fc97fa3e7d0', N'Support', N'پشتیبان', CONVERT(datetime2, '2026-08-20T20:06:51.4151162', 126)),
  ('c62b0d74-c78e-4741-8cbd-62442b17fcd7', N'KycViewer', N'ناظر احراز هویت', CONVERT(datetime2, '2026-08-23T05:20:07.0047902', 126)),
  ('9e745bb2-e82a-49a2-acc7-a8c78c857dcc', N'Admin', N'مدیر فروشگاه', CONVERT(datetime2, '2026-08-20T20:06:51.4151162', 126)),
  ('94d1eb60-e4ea-441a-b414-c47ad4458c5d', N'SuperAdmin', N'مدیر کل', CONVERT(datetime2, '2026-08-20T20:06:51.4151162', 126)),
  ('25b9fb44-da18-4835-bbd3-d7c404157193', N'Customer', N'مشتری', CONVERT(datetime2, '2026-08-20T20:06:51.4151162', 126));
GO

PRINT N'Seeding dbo.Settings (190 row(s))...';
GO
INSERT INTO dbo.[Settings] ([Id], [Key], [Value], [GroupName], [ValueType], [Description], [UpdatedAt]) VALUES
  ('24f47bff-dbcb-455f-9581-007ee55ca15b', N'LogoSmallPath', N'', N'Logos', N'image', N'لوگوی کوچک / آیکون', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('c3c035a8-c4f5-4b37-b724-034ea353e574', N'WalletMaxCharge', N'100000000', N'Wallet', N'decimal', N'حداکثر شارژ کیف پول', CONVERT(datetime2, '2026-08-23T05:20:07.2608258', 126)),
  ('88341207-d967-414c-9ed8-04709b1d28d2', N'HeaderLogoPath', N'', N'Logos', N'image', N'لوگوی هدر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('691bce7d-56b6-4c17-b224-059509fd460b', N'Sms.VerificationApprovedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('8b164489-36fc-493b-a091-05c344c3652b', N'Error400Title', N'درخواست نامعتبر', N'Errors', N'string', N'عنوان ۴۰۰', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('b825f88d-c146-4192-818b-06148c18bb39', N'SmtpFromName', N'ویتورایز', N'Email', N'string', N'نام فرستنده', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('ece9cee3-1fe9-4cc8-ad80-08da12518dcf', N'CustomFooterHtml', N'', N'Scripts', N'string', N'کد سفارشی انتهای صفحه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('a75d857b-6a65-422d-af8d-0a799f21f978', N'NetworkErrorText', N'ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید.', N'Errors', N'string', N'متن خطای شبکه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('30b2c6ca-e19a-4797-b557-0a9f5adc269f', N'TrustSeal.Ecunion.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی ecunion.ir', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('784cb71f-3874-47bb-8fd0-0ab08828fdb7', N'Sms.OrderCompletedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('cda3ab16-a302-4e64-9be0-0ab8f0c88552', N'LogoPath', N'', N'Logos', N'image', N'لوگوی اصلی (تم روشن)', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('81458dad-4d59-4db8-81c5-0bb7739df8d5', N'Security.OutboxLockTimeoutMinutes', N'5', N'Security', N'int', N'زمان بازیابی پیام Outbox قفل‌شده', NULL),
  ('9855c7ba-86f7-4fe8-a735-0c0b45f5203c', N'TrustSeal.Samandehi.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش ساماندهی', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('b5cb7979-d754-4c56-a019-0daa14aac7d0', N'EmptySearchText', N'نتیجه‌ای برای جستجوی شما پیدا نشد.', N'Empty', N'string', N'جستجوی بدون نتیجه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('1c2f314b-58bd-429a-aba2-0e58b23b23cb', N'Sms.RegisterOtpTemplateId', N'', N'SMS', N'int', N'کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('72789c9e-2b8e-47b1-a03a-13160cc2a37e', N'Error400Text', N'درخواست شما معتبر نیست. لطفاً دوباره تلاش کنید.', N'Errors', N'string', N'متن ۴۰۰', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('ca492d86-7fc0-439a-b892-169c3e7ad8fc', N'EmptyTicketsText', N'تیکتی ثبت نکرده‌اید.', N'Empty', N'string', N'تیکت خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('84c40f2b-9134-4690-8cbc-16ad6ac137ad', N'SiteTagline', N'بازارگاه دیجیتال گیمینگ و خدمات آنلاین', N'Branding', N'string', N'شعار سایت (کنار لوگو و عنوان صفحات)', CONVERT(datetime2, '2026-08-23T05:20:07.1093770', 126)),
  ('44f1a545-181b-4ef6-be00-179bbf66f2a7', N'TrustSeal.Ecunion.Title', N'اتحادیه کسب‌وکارهای مجازی', N'TrustSeals', N'string', N'عنوان مجوز', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('ea9c3668-ad23-4d12-96c8-19f29da91b26', N'Typography.Version', N'1', N'Typography', N'string', N'نسخه کش فونت', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('4fb5a567-b8a5-4b52-b052-1a2e6d9146df', N'Sms.UseOutbox', N'true', N'SMS', N'bool', N'ارسال پیامک رویدادهای تجاری از طریق Outbox', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('2102b06b-721a-468a-8218-1f8956122aaa', N'Sms.MaxCustomTextLength', N'500', N'SMS', N'int', N'حداکثر طول پیامک متنی سفارشی', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('1612c434-53b5-40d7-9a91-200d864c6409', N'MaxLoginAttempts', N'5', N'Security', N'int', N'حداکثر تلاش ورود', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('bf0b4692-09a8-4ad7-9ced-2047bd048382', N'FacebookUrl', N'', N'Social', N'string', N'فیسبوک', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('4627c4d3-bf0b-4b61-9617-21a2e0bf6f43', N'EmptyOrdersText', N'هنوز سفارشی ثبت نکرده‌اید.', N'Empty', N'string', N'سفارش‌های خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('5afd0db2-fa2d-4434-abf6-22fe74a942bd', N'MetaDescription', N'خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.', N'SEO', N'string', N'توضیح متای پیش‌فرض', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('4f4251d0-8a22-4873-8a5b-26af1975cdd6', N'LoadingMediaPath', N'', N'Logos', N'image', N'تصویر یا GIF بارگذاری اولیه (خالی = لودر پیش‌فرض ویتورایز)', CONVERT(datetime2, '2026-08-20T20:06:56.9960219', 126)),
  ('65cf32a0-ce09-4cc0-bf1a-2916ffba805d', N'SmtpHost', N'', N'Email', N'string', N'میزبان SMTP', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8865d3ed-98cf-485f-aba6-2f087877afed', N'FooterDescription', N'بازارگاه دیجیتال گیمینگ و خدمات آنلاین؛ خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی.', N'Branding', N'string', N'توضیح فوتر', CONVERT(datetime2, '2026-08-23T05:20:07.1121361', 126)),
  ('680998ac-b83b-4c21-aecd-2f44cf285bae', N'SmsEnabled', N'false', N'SMS', N'bool', N'ارسال پیامک (کلید قدیمی؛ از Sms.IsEnabled استفاده کنید)', CONVERT(datetime2, '2026-08-23T05:20:07.2189988', 126)),
  ('2c20f154-fd4f-4882-81b0-3443bad2e499', N'SmtpPort', N'587', N'Email', N'int', N'پورت SMTP', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('ec165088-5eb8-40e6-a63a-345200946cde', N'TrustSeal.Ecunion.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('05eb6b6a-4b2d-4994-8321-34f16289fceb', N'Sms.CustomSendEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی ارسال پیامک سفارشی توسط مدیر', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('64854d76-487f-4aa0-b86e-366585ae1dad', N'Security.RefreshTokenRetentionDays', N'30', N'Security', N'int', N'مدت نگهداری توکن‌های منقضی یا لغوشده', NULL),
  ('ccb87497-f74f-438c-84ca-38d03b472b35', N'FooterText', N'', N'Footer', N'string', N'متن آزاد فوتر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('03564aeb-bf53-4387-adfc-38e0efc3a112', N'VatCalculationMode', N'BeforeDiscount', N'Tax', N'vatmode', N'نحوه محاسبه مالیات بر ارزش افزوده', CONVERT(datetime2, '2026-08-20T20:06:56.2515319', 126)),
  ('f6d37844-16c7-4570-ad0b-3a49ace1397d', N'HeroTitle', N'دنیای بازی و دیجیتال در دستان تو', N'Homepage', N'string', N'عنوان اصلی Hero صفحه اول', CONVERT(datetime2, '2026-08-23T05:20:07.1659427', 126)),
  ('f2ae21d2-ff57-4c88-aee9-3bd82120b543', N'SocialPreviewImagePath', N'', N'Logos', N'image', N'تصویر پیش‌نمایش شبکه‌های اجتماعی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('40453621-040d-4b20-9e6d-3d7bf1eef55d', N'StorefrontDefaultProductSort', N'AvailabilityFirst', N'General', N'string', N'ترتیب پیش‌فرض نمایش کالاها برای مشتریان در فروشگاه', CONVERT(datetime2, '2026-08-23T05:20:07.1734750', 126)),
  ('bdfff99f-5f79-4bf4-97f9-3ea6a8076ec4', N'Security.OtpRetentionDays', N'7', N'Security', N'int', N'مدت نگهداری سوابق کد یکبار مصرف', NULL),
  ('d36f2a92-616c-49c2-a9bb-424ca2971650', N'Typography.FontFamily', N'Vazirmatn', N'Typography', N'string', N'نام فونت فعال؛ پیش‌فرض Vazirmatn', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('7a1a9f11-9ace-4996-88f5-4390399a018a', N'Error404Text', N'صفحه‌ای که دنبال آن هستید وجود ندارد یا منتقل شده است.', N'Errors', N'string', N'متن ۴۰۴', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('f8270baa-743c-4c69-8598-444d31220d56', N'Sms.Provider', N'SMS.ir', N'SMS', N'string', N'ارائه‌دهنده پیامک', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('7956423f-1a33-4291-8084-472671b40904', N'EmptyNotificationsText', N'اعلان جدیدی ندارید.', N'Empty', N'string', N'اعلان خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('47595f09-82ca-4f45-8a5f-4790ccc66345', N'Sms.SenderName', N'ویتورایز', N'SMS', N'string', N'نام فرستنده', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('a4cbc7e6-d01a-471b-b8ef-49fc53b6438c', N'Error401Title', N'نیاز به ورود', N'Errors', N'string', N'عنوان ۴۰۱', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('7eddb153-683f-459f-9389-4aee996101a2', N'Typography.FontPath', N'', N'Typography', N'string', N'مسیر فایل فونت فعال', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('fb2cac1f-c970-41d1-9266-4b1c89e0424c', N'MaxUploadSizeMb', N'2', N'Uploads', N'int', N'حداکثر حجم آپلود (مگابایت)', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('213a1d63-5830-4e38-9f05-4d397e28d398', N'Sms.OrderPaidTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('87dff737-a85b-443a-9ef7-4f3cd572984d', N'TrustSeal.Samandehi.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('91169a5c-339b-4dc7-bad3-4f56c3701121', N'Sms.CustomTextEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی پیامک متنی سفارشی', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('1220c210-5e36-4b3e-97c5-4fa391d6b023', N'Security.KycRejectedRetentionDays', N'90', N'Security', N'int', N'مدت نگهداری مدارک ردشده احراز هویت', NULL),
  ('dbfdd4c5-8c37-499a-ad60-52c922992899', N'FooterLogoPath', N'', N'Logos', N'image', N'لوگوی فوتر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('306f7005-5090-43f7-92ca-57d5d5c1a973', N'Sms.NotificationTemplateId', N'', N'SMS', N'int', N'شناسه قالب اطلاع‌رسانی عمومی', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('d5d529e3-fe67-4717-9b1f-5af2fbba298e', N'HomeFeaturesKicker', N'چرا ویتورایز؟', N'Trust', N'string', N'برچسب بخش چرا ما', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('f63bf343-09e4-47e3-91ea-5d0f880b6b81', N'CopyrightText', N'تمامی حقوق برای ویتورایز محفوظ است.', N'Branding', N'string', N'متن کپی‌رایت فوتر', CONVERT(datetime2, '2026-08-23T05:20:07.1130187', 126)),
  ('21ba729d-172e-405d-bcea-5d1e5a82e47d', N'ZarinpalMerchantId', N'00000000-0000-0000-0000-000000000000', N'Payment', N'string', N'شناسه پذیرنده زرین‌پال (مقدار نصب اولیه؛ پیش از پذیرش پرداخت باید با شناسه واقعی جایگزین شود)', CONVERT(datetime2, '2026-08-23T05:20:07.2616159', 126)),
  ('adad3f59-8b9f-4f65-951f-5d43a1582fd7', N'HeroCtaText', N'ورود به فروشگاه', N'Homepage', N'string', N'متن دکمه اصلی Hero', CONVERT(datetime2, '2026-08-23T05:20:07.1673054', 126)),
  ('fc80ea36-bb1c-4666-9d31-5f802e24252f', N'TrustBadgesJson', N'[{"icon":"shield-check","title":"تضمین اصالت","text":"محصولات رسمی و اورجینال"},{"icon":"zap","title":"تحویل آنی","text":"سریع و بدون انتظار"},{"icon":"headphones","title":"پشتیبانی ۲۴/۷","text":"همیشه کنار شما"},{"icon":"lock","title":"پرداخت امن","text":"درگاه‌های معتبر"}]', N'Trust', N'json', N'نشان‌های اعتماد', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('b68ed571-41ff-4afd-a904-605063954fe6', N'SiteDescription', N'فروشگاه گیفت کارت و سرویس‌های دیجیتال', N'General', N'string', N'توضیح کوتاه فروشگاه', CONVERT(datetime2, '2026-08-23T05:20:07.1015446', 126)),
  ('2a8195e9-1b6c-4f1c-a46b-61083fe0ce95', N'Sms.ApiKey', N'', N'SMS', N'secret', N'کلید API پنل SMS.ir (محرمانه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('6264a319-4a45-4a9a-bd95-633ce41bcf71', N'LinkedInUrl', N'', N'Social', N'string', N'لینکدین', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('c89302c6-4fb5-45a1-946d-6366184d142f', N'Sms.MaxRetryCount', N'5', N'SMS', N'int', N'حداکثر تعداد بازتلاش ارسال', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('454e8f97-55ca-4cfe-93c9-63be6352aea8', N'TelegramUrl', N'https://t.me/vitorize', N'Social', N'string', N'کانال تلگرام', CONVERT(datetime2, '2026-08-23T05:20:07.1795295', 126)),
  ('a25c8e26-3962-4eec-8b7a-670afea2d440', N'StorefrontPersianFont', N'Vazirmatn', N'Typography', N'font', N'Default Persian storefront font.', CONVERT(datetime2, '2026-08-20T20:06:53.8940487', 126)),
  ('a7c96c11-cd6a-48bb-a49f-674d911ead20', N'Sms.AllowAdminViewFullMobile', N'false', N'SMS', N'bool', N'اجازه مشاهده شماره کامل برای مدیر کل', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('f824fda5-b225-45e7-97b3-6811921c1573', N'Error401Text', N'برای مشاهده این صفحه ابتدا وارد حساب کاربری شوید.', N'Errors', N'string', N'متن ۴۰۱', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('6b3b06b5-f620-420a-bbee-6a9689d48d3e', N'Sms.OtpTemplateId', N'', N'SMS', N'int', N'شناسه قالب کد یکبار مصرف', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('a9e4e095-361a-4d43-b3e8-6c426f2319bd', N'RequireEmailConfirmation', N'false', N'Security', N'bool', N'الزام تأیید ایمیل', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('39b01045-5cc5-4af0-b0e4-6c68bfbc885b', N'CustomHeadHtml', N'', N'Scripts', N'string', N'کد سفارشی <head>', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('a87b36cf-6875-4f8d-991b-6d2280945f45', N'Sms.LoginOtpTemplateId', N'', N'SMS', N'int', N'کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('47012538-8334-4068-a4b9-6f91d02a8134', N'WhatsAppUrl', N'', N'Social', N'string', N'واتساپ', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('b8c3590b-a944-490b-b796-73536a4cd271', N'TrustSeal.Enamad.SortOrder', N'10', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('00159276-a8d4-43da-822c-742af17f7e8d', N'LogoDarkPath', N'', N'Logos', N'image', N'لوگوی تم تیره', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('0196c7e2-386e-4b8d-ad07-757b2313355a', N'Sms.RetryDelaySeconds', N'30', N'SMS', N'int', N'پایه تأخیر بازتلاش (ثانیه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('00ed1fc8-3457-4df7-8208-76b546ccab8a', N'TrustSeal.Samandehi.ImagePath', N'', N'TrustSeals', N'image', N'تصویر نشان', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('26eeb07b-6fc1-498f-8848-76f08d1e73ba', N'SmtpFromEmail', N'', N'Email', N'string', N'ایمیل فرستنده', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('931a825e-86c5-4a13-a48b-784ff5680e68', N'VatRatePercent', N'0', N'Tax', N'decimal', N'نرخ مالیات بر ارزش افزوده (درصد)', CONVERT(datetime2, '2026-08-20T20:06:56.2515319', 126)),
  ('98198b12-a390-4dfe-85ba-78f320f7c301', N'TrustSeal.Ecunion.SortOrder', N'20', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('41a2771e-701b-49b3-8519-7bb694732bb1', N'NoProductsText', N'محصولی برای نمایش وجود ندارد.', N'Empty', N'string', N'نبود محصول', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('bc3e140d-02e3-4de7-98d5-7c117740d978', N'TrustSeal.Ecunion.ImagePath', N'', N'TrustSeals', N'image', N'تصویر مجوز', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('81e4444c-570d-4a61-b6c1-7d51103c0f04', N'Branding.AssetVersion', N'1', N'Branding', N'string', N'نسخه کش دارایی‌های برند', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('f846bb81-2724-4288-bad6-7eab732e3417', N'BrandPrimaryColor', N'', N'Branding', N'color', N'رنگ اصلی برند', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('95b0e23f-ebce-425b-81ab-7ec60db9e17c', N'Error404IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۴۰۴', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('cbb8bbd3-ac40-4794-b358-8150e2698355', N'Sms.GiftCodeDeliveredTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('c128a129-c0a0-40b6-882c-81543aac863f', N'Sms.AllowImmediateSend', N'false', N'SMS', N'bool', N'اجازه ارسال فوری به جای صف', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('7477420e-7f03-48ff-8d26-8275aeeaaec2', N'Sms.MaxCustomRecipients', N'1', N'SMS', N'int', N'حداکثر گیرنده در هر ارسال سفارشی', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('b134336c-fa14-48ef-81a1-833215e7b4f0', N'Sms.LogSensitiveData', N'false', N'SMS', N'bool', N'لاگ‌کردن داده حساس (فقط توسعه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('ae653060-ed4d-493e-bf23-83e8bcae4807', N'Error503Title', N'در حال به‌روزرسانی', N'Errors', N'string', N'عنوان ۵۰۳', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('0a54d123-1c5f-490f-b8df-84522a495c13', N'Sms.DefaultLineNumber', N'', N'SMS', N'string', N'شماره خط اختصاصی برای پیامک متنی (محرمانه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('413bc1f8-9ab9-483a-878c-859080b9723d', N'VatEnabled', N'false', N'Tax', N'bool', N'فعال بودن مالیات بر ارزش افزوده', CONVERT(datetime2, '2026-08-20T20:06:56.2515319', 126)),
  ('52b3151c-5189-466c-81db-85f96bb00602', N'MaintenanceMode', N'false', N'General', N'bool', N'حالت تعمیر و نگهداری', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('bc299bb0-ccab-4148-96bd-8622539a349c', N'Sms.MaskMobileInAdmin', N'true', N'SMS', N'bool', N'پنهان‌سازی شماره موبایل در تاریخچه مدیر', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('ae63b85e-6609-4c57-aa1b-87d479aa25f3', N'SmtpEnableSsl', N'true', N'Email', N'bool', N'استفاده از SSL', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8592d00f-f5fe-4b48-9ef2-8970174b4ee3', N'StorefrontEnglishFont', N'Funnel Display', N'Typography', N'font', N'Default English storefront font.', CONVERT(datetime2, '2026-08-20T20:06:53.8940487', 126)),
  ('b46bfdf2-85e6-4d0a-a9eb-8a46fa2d3b68', N'Sms.AllowRetryFailed', N'true', N'SMS', N'bool', N'اجازه بازتلاش امن پیامک ناموفق', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('86f37720-a44c-4dc4-8471-8af1c352dbaa', N'HeroSubtitle', N'خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.', N'Homepage', N'string', N'زیرعنوان Hero صفحه اول', CONVERT(datetime2, '2026-08-23T05:20:07.1666090', 126)),
  ('189b1a73-e37f-423c-b6ed-8b48f027b96d', N'SmtpUsername', N'', N'Email', N'string', N'نام کاربری SMTP', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('15f1d305-cbb9-4ecb-b711-8c6ea67cf138', N'Error404Title', N'صفحه پیدا نشد', N'Errors', N'string', N'عنوان ۴۰۴', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('26b5bcac-eef3-49cf-ba65-8d849d994d67', N'Security.AuditRetentionDays', N'730', N'Security', N'int', N'مدت نگهداری رویدادهای ممیزی', NULL),
  ('87f8058b-14b0-4138-bd5b-8de1f52efda4', N'HomeFeaturesTitle', N'خرید دیجیتال، ساده و مطمئن', N'Trust', N'string', N'عنوان بخش چرا ما', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('9753beae-573b-4149-87e8-8fb416747d08', N'YouTubeUrl', N'', N'Social', N'string', N'یوتیوب', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('5335467c-051b-4ca7-8581-93efd57e9347', N'Sms.TicketReplyTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('f4ed1ec2-66db-4351-b561-942c643c50e0', N'TrustSeal.Ecunion.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش ecunion', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('9239862d-3ad7-4933-ba73-945d8abb4352', N'Sms.ForgotPasswordTemplateId', N'', N'SMS', N'int', N'کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('b7e16c35-7721-4816-82f5-94fc36692417', N'TwitterImagePath', N'', N'Logos', N'image', N'تصویر توییتر / X', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('38786005-35b0-4406-9f33-953c6a902ff5', N'AppleTouchIconPath', N'', N'Logos', N'image', N'آیکون Apple Touch', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('31e71740-2c7a-4b49-aada-979aeddd1b42', N'Sms.RequireConfirmation', N'true', N'SMS', N'bool', N'نیاز به تایید نهایی پیش از ارسال سفارشی', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('ffcd5f15-c3d5-4a79-8330-9becb7f8e4f9', N'InstagramUrl', N'https://instagram.com/vitorize', N'Social', N'string', N'صفحه اینستاگرام', CONVERT(datetime2, '2026-08-23T05:20:07.1789490', 126)),
  ('64d43c26-d47d-46a3-8c5f-9c173aa11e3b', N'Error500IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۵۰۰', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d951b21c-84ff-4b04-a501-9c8b62e83416', N'HeroKicker', N'ویتورایز · بازارگاه دیجیتال', N'Homepage', N'string', N'متن کوچک بالای عنوان Hero', CONVERT(datetime2, '2026-08-23T05:20:07.1652490', 126)),
  ('68fc7d52-77e2-4711-9e28-9ecf0de1728c', N'ZarinpalBaseUrl', N'https://sandbox.zarinpal.com/pg/v4/payment', N'Payment', N'string', N'آدرس اصلی زرین‌پال', CONVERT(datetime2, '2026-08-23T05:20:07.2636292', 126)),
  ('512c214f-50a1-41a6-afa9-9f58e47af990', N'NewsletterSubtitle', N'با عضویت در خبرنامه، از تخفیف‌ها و محصولات تازه زودتر از همه مطلع شو.', N'Homepage', N'string', N'زیرعنوان خبرنامه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('06c2a630-35d3-4a6c-a6e0-9fdacd03c84d', N'ZarinpalCallbackUrl', N'https://localhost:7221/api/payments/zarinpal/callback', N'Payment', N'string', N'آدرس بازگشت پرداخت زرین‌پال', CONVERT(datetime2, '2026-08-23T05:20:07.2643960', 126)),
  ('1a8d6bbe-27b1-464d-8649-a138f471caf7', N'EnableWallet', N'true', N'Features', N'bool', N'کیف پول کاربران', CONVERT(datetime2, '2026-08-23T05:20:07.2181945', 126)),
  ('ad90a71b-2462-4ca8-975c-a21669b01e73', N'Error403Text', N'شما اجازه دسترسی به این بخش را ندارید.', N'Errors', N'string', N'متن ۴۰۳', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('30f0727e-d2b7-4094-a593-a54ae6b3147e', N'SiteLogoPath', N'', N'Branding', N'string', N'مسیر لوگوی سایت (خالی = لوگوی پیش‌فرض)', CONVERT(datetime2, '2026-08-23T05:20:07.1103389', 126)),
  ('abca10ec-946c-4a07-83be-a5bd10114881', N'TrustSeal.Samandehi.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی samandehi.ir', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('166699a0-28f5-4132-a79a-a86aa13db696', N'TrustSeal.Enamad.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('0e1a89b2-f57c-469e-8209-a989d6284248', N'HeroBackgroundPath', N'', N'Logos', N'image', N'پس‌زمینه Hero', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('74becc79-c2e5-4a3b-bb7c-a9b5052846d3', N'EmptyReviewsText', N'هنوز نظری ثبت نشده است.', N'Empty', N'string', N'نظرات خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('ede505cf-b13d-40ab-a48a-ad11913d3b29', N'AboutTitle', N'درباره ویتورایز', N'About', N'string', N'عنوان درباره ما', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('cc5a7b91-7f1c-453a-9041-ad156714fd02', N'NetworkErrorTitle', N'خطای ارتباط', N'Errors', N'string', N'عنوان خطای شبکه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8945a665-28a5-4d28-87b3-ae19930c2ecd', N'Sms.IsEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی سرویس پیامک SMS.ir', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('64496906-e6d9-4373-9eb3-afa3963597a7', N'TrustSeal.Samandehi.Alt', N'نشان ساماندهی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('bbeea199-7bfa-40d1-bddd-b083afa25f1c', N'HomeFeaturesJson', N'[{"icon":"layout-grid","title":"انتخاب محصول","text":"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن."},{"icon":"credit-card","title":"پرداخت امن","text":"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده."},{"icon":"zap","title":"تحویل آنی","text":"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود."}]', N'Trust', N'json', N'مراحل صفحه اول', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('46490b95-69d4-477b-8a78-b0c3c6e223ad', N'Typography.FontFormat', N'woff2', N'Typography', N'string', N'فرمت فایل فونت فعال', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('44c41f32-60d9-4aff-9e5e-b12aef184e5b', N'TrustSeal.Enamad.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش Enamad', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('f28bf5ca-98a3-4d81-95ef-b22a089840a6', N'WorkingHours', N'شنبه تا پنجشنبه، ۹ تا ۱۸', N'Contact', N'string', N'ساعات کاری', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('44352d51-ffec-46d4-90db-b2bb404292cc', N'MaintenanceMessage', N'به‌زودی با نسخه‌ای بهتر برمی‌گردیم. از صبوری شما سپاسگزاریم.', N'General', N'string', N'پیام صفحه حالت تعمیر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d337d95f-99d6-4474-b165-b3e6b2ce135f', N'PageRemovedText', N'محتوایی که دنبال آن بودید دیگر در دسترس نیست.', N'Errors', N'string', N'متن صفحه حذف‌شده', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('2a844186-849d-46cf-8a14-b4a61ce9b1bc', N'Typography.Scope', N'3', N'Typography', N'int', N'محدوده اعمال: ۱ فروشگاه، ۲ مدیریت، ۳ کل برنامه', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('d7f2442d-a83b-47c7-8db9-b63c15c484ec', N'HeroSecondaryCtaUrl', N'/categories', N'Homepage', N'string', N'لینک دکمه دوم Hero', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('dbaa278b-fd83-4cf9-8bdf-b7379b9cb989', N'SupportPhone', N'02100000000', N'Contact', N'string', N'شماره پشتیبانی', CONVERT(datetime2, '2026-08-23T05:20:07.1854615', 126)),
  ('ae9770d8-86c5-4c44-9f9e-b7ddab193460', N'NewsletterCtaText', N'عضویت', N'Homepage', N'string', N'متن دکمه خبرنامه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('cb2f60a5-27d4-4266-ae23-b81db6900e35', N'HeroCtaUrl', N'/shop', N'Homepage', N'string', N'لینک دکمه اصلی Hero', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('20821660-cc7b-4519-8987-b8e00745e7a2', N'TrustSeal.Enamad.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی enamad.ir', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('2aa2fb06-683d-4372-a2bf-b8e30949148e', N'Error500Text', N'مشکلی در سرور رخ داد. تیم ما در حال بررسی است.', N'Errors', N'string', N'متن ۵۰۰', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('9b3a8203-aa94-4e52-980a-b960b41f27d7', N'SessionExpiredTitle', N'نشست شما منقضی شد', N'Errors', N'string', N'عنوان نشست منقضی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('f5b1e532-fc66-4086-a505-bdb1c003ce28', N'FaviconPath', N'', N'Logos', N'image', N'فاوآیکون سایت', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('a0c5ac69-3774-4ffc-bea4-bf1767447aa2', N'Sms.HistoryRetentionDays', N'180', N'SMS', N'int', N'مدت نگهداری تاریخچه پیامک بر حسب روز', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('ceab91d2-de46-4259-a35d-bfcd61c42303', N'SessionExpiredText', N'برای ادامه دوباره وارد شوید.', N'Errors', N'string', N'متن نشست منقضی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8bee8e30-ee6e-4602-8451-c0685e89982e', N'MinPasswordLength', N'8', N'Security', N'int', N'حداقل طول رمز', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('4f6d5af8-8a98-47c1-afbf-c137de3a2a01', N'Typography.MaxUploadMb', N'5', N'Typography', N'int', N'حداکثر حجم فونت', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('5cbbaf0f-712d-48c7-a4ef-c1472e2b5568', N'EmptyWishlistText', N'هنوز محصولی به علاقه‌مندی‌ها اضافه نکرده‌اید.', N'Empty', N'string', N'علاقه‌مندی خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('2cce892d-5a6d-4e18-894b-c258ed36638a', N'SmsProvider', N'Mock', N'SMS', N'string', N'ارائه‌دهنده پیامک (کلید قدیمی)', CONVERT(datetime2, '2026-08-23T05:20:07.2197396', 126)),
  ('6430006f-8b1d-4c55-8375-c2aac416e4fb', N'NewsletterTitle', N'از جدیدترین‌ها باخبر شو', N'Homepage', N'string', N'عنوان خبرنامه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('9a9cc338-6f24-416b-ad22-c2d4c1b21b8e', N'EmptyCartText', N'سبد خرید شما خالی است.', N'Empty', N'string', N'سبد خرید خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('65cfa668-4892-428f-aafe-c4794313ec59', N'Sms.DailyOtpLimitPerMobile', N'10', N'SMS', N'int', N'سقف کد روزانه برای هر شماره', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('4cdbfe17-d6fe-494a-aee4-c577c9a1e3fe', N'AboutText', N'ویتورایز بازارگاهی دیجیتال برای خرید امن و آنی گیفت کارت، اشتراک و خدمات آنلاین است.', N'About', N'string', N'متن درباره ما', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('44615788-908c-4cb1-97bd-c63935815e10', N'Error403Title', N'دسترسی مجاز نیست', N'Errors', N'string', N'عنوان ۴۰۳', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('85f21123-2b56-4d4d-8580-c657c0657a59', N'Sms.DailySmsLimitPerMobile', N'30', N'SMS', N'int', N'سقف پیامک روزانه برای هر شماره', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('7411da0b-0a7d-4d63-b510-c689a9164ad8', N'DiscordUrl', N'', N'Social', N'string', N'دیسکورد', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('30e4526a-95c8-467e-a64f-c7480ef6ff25', N'PageRemovedTitle', N'این صفحه حذف شده است', N'Errors', N'string', N'عنوان صفحه حذف‌شده', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d1518936-de84-40d8-a801-c7b75b5eee0a', N'Sms.OtpResendCooldownSeconds', N'90', N'SMS', N'int', N'فاصله ارسال مجدد کد (ثانیه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('a9997bc7-4465-4f0d-9543-c91a2b044ee4', N'GoogleAnalyticsId', N'', N'SEO', N'string', N'شناسه Google Analytics', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('4214c811-c6c2-4b18-a21e-cb0c8a0fb089', N'Error500Title', N'خطای غیرمنتظره', N'Errors', N'string', N'عنوان ۵۰۰', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('1c82ce2f-3704-4c5d-84a2-ce4a2b027570', N'TrustSeal.Enamad.Title', N'نماد اعتماد الکترونیکی', N'TrustSeals', N'string', N'عنوان نماد', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('ece4577f-5b70-4ea2-b388-cf0058b9d4a2', N'OgImagePath', N'', N'Logos', N'image', N'تصویر OpenGraph', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('ce303e40-a14e-4f96-863d-d218563219dc', N'ZarinpalSandbox', N'true', N'Payment', N'bool', N'حالت آزمایشی زرین‌پال', CONVERT(datetime2, '2026-08-23T05:20:07.2622485', 126)),
  ('2f3ff4bf-d2fc-4d48-9c98-d32acbe6978c', N'TrustSeal.Ecunion.Alt', N'مجوز اتحادیه کسب‌وکارهای مجازی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('9e6d9fcb-17f8-453f-aa4d-d99df0f8b6df', N'WalletMinCharge', N'100000', N'Wallet', N'decimal', N'حداقل شارژ کیف پول', CONVERT(datetime2, '2026-08-23T05:20:07.2600874', 126)),
  ('4a93b619-4f43-4636-a74f-d9d7ac1dcf17', N'Seo.CanonicalBaseUrl', N'', N'SEO', N'string', N'آدرس پایه HTTPS و میزبان اصلی برای canonical، robots و sitemap', CONVERT(datetime2, '2026-08-20T20:06:52.0967950', 126)),
  ('60f1ed20-cce7-40db-b9a4-dac0b166700b', N'TrustSeal.Enamad.ImagePath', N'', N'TrustSeals', N'image', N'تصویر نماد', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('0e1ad12b-ac2b-49b9-9ed3-dd9860c0f20b', N'EmptyStateIllustrationPath', N'', N'Logos', N'image', N'تصویر پیش‌فرض حالت خالی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8859edc3-32fb-4778-ba04-deb301e870ec', N'XUrl', N'', N'Social', N'string', N'X (توییتر)', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d77c274f-a1ca-43a5-b764-df98fd5b7426', N'Error503Text', N'سایت موقتاً در دسترس نیست. به‌زودی برمی‌گردیم.', N'Errors', N'string', N'متن ۵۰۳', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('23a427a0-112b-4d08-88d4-e165577b466a', N'Sms.VerificationRejectedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('18624396-ec24-474e-a0df-e2785a51e780', N'TrustSeal.Samandehi.Title', N'نشان ملی ثبت رسانه‌های دیجیتال', N'TrustSeals', N'string', N'عنوان نشان', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('38667a0a-b0be-4e57-9609-e2874e03649b', N'OfflineText', N'به نظر می‌رسد اینترنت شما قطع شده است.', N'Errors', N'string', N'متن آفلاین', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d3dc7943-208e-434f-9462-e429f14c1d26', N'SeoTitleTemplate', N'{page} | {site}', N'SEO', N'string', N'قالب عنوان صفحات', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('d7e60add-d3b0-41f9-9072-e8053b5010d7', N'Sms.OrderStatusChangedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('c76cb7ba-d803-4889-9552-e858b1791741', N'OfflineTitle', N'اتصال اینترنت قطع است', N'Errors', N'string', N'عنوان آفلاین', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('6180d017-6843-406a-b888-e9e6144ec264', N'ZarinpalStartPayUrl', N'https://sandbox.zarinpal.com/pg/StartPay', N'Payment', N'string', N'آدرس شروع پرداخت زرین‌پال', CONVERT(datetime2, '2026-08-23T05:20:07.2629721', 126)),
  ('adca3404-15fb-4034-bb09-ea33fd628778', N'HomePopularProductsEnabled', N'false', N'Homepage', N'bool', N'نمایش محبوب‌ترین کالاها در صفحه اصلی', CONVERT(datetime2, '2026-08-23T05:20:07.1724721', 126)),
  ('a43290ab-812d-43d0-b347-eac5acca4c46', N'Sms.OtpMaxAttempts', N'5', N'SMS', N'int', N'حداکثر تلاش مجاز برای هر کد', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('17bfaaef-76b4-4bbd-87f5-ecb3affc54c9', N'MetaTitle', N'ویتورایز | بازارگاه دیجیتال گیمینگ و خدمات آنلاین', N'SEO', N'string', N'عنوان متای پیش‌فرض', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('b50e3418-addc-46c0-9e87-ed48852e5539', N'SupportEmail', N'support@vitorize.com', N'Contact', N'string', N'ایمیل پشتیبانی', CONVERT(datetime2, '2026-08-23T05:20:07.1844433', 126)),
  ('48407e90-330e-410d-adb6-f11b01f60e12', N'MetaKeywords', N'گیفت کارت, اشتراک, خدمات دیجیتال, بازی, گیمینگ, ویتورایز', N'SEO', N'string', N'کلمات کلیدی', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('50b6ece8-0194-4558-bbe7-f25556f4a87c', N'AllowedImageFormats', N'jpg,jpeg,png,webp', N'Uploads', N'string', N'فرمت‌های مجاز تصویر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('be00d080-70bd-412e-944b-f4bf7e16a925', N'Sms.WalletTopUpSuccessTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126)),
  ('88173ba0-052b-47c5-8365-f5358d65ab2a', N'ContactAddress', N'', N'Contact', N'string', N'آدرس', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('dd474ae9-3b05-48e6-b7d9-f5af191df2d4', N'HeroSecondaryCtaText', N'دسته‌بندی‌ها', N'Homepage', N'string', N'متن دکمه دوم Hero', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('625a98b6-c524-43d6-8dd4-f7483dccd11b', N'TrustSeal.Enamad.Alt', N'نماد اعتماد الکترونیکی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('b801fdc6-c1b7-49fd-945d-f8cdd9552606', N'NewsletterPlaceholder', N'ایمیل خود را وارد کنید', N'Homepage', N'string', N'راهنمای ورودی خبرنامه', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('89b34d1b-b7b0-4768-86d9-fa66e791bce5', N'TrustSeal.Samandehi.SortOrder', N'30', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-20T20:06:53.6635192', 126)),
  ('133c8a91-4767-466d-b608-fb1c5e1d234f', N'SiteName', N'ویتورایز', N'General', N'string', N'نام فروشگاه', CONVERT(datetime2, '2026-08-23T05:20:07.0875260', 126)),
  ('f36c804c-774a-4be0-a464-fbf42dc4c952', N'MaintenanceIllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه تعمیر', CONVERT(datetime2, '2026-08-20T20:06:53.1212965', 126)),
  ('8a8fd814-6141-4154-bbb4-fcc3d29e0bc8', N'EnableRegistration', N'true', N'Features', N'bool', N'ثبت‌نام کاربران', CONVERT(datetime2, '2026-08-23T05:20:07.2173649', 126)),
  ('69323d95-dd1f-4493-a641-fd1b2243a600', N'Sms.OtpExpiryMinutes', N'3', N'SMS', N'int', N'مدت اعتبار کد یکبار‌مصرف (دقیقه)', CONVERT(datetime2, '2026-08-20T20:06:53.4258874', 126));
GO

PRINT N'Seeding dbo.Pages (4 row(s))...';
GO
INSERT INTO dbo.[Pages] ([Id], [Title], [Slug], [ContentHtml], [SeoTitle], [SeoDescription], [IsPublished], [CreatedAt], [UpdatedAt], [FocusKeyword], [IsSystem]) VALUES
  ('4505f841-6a5c-42fd-b662-3d206bfc8942', N'درباره ما', N'about', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'درباره ما', N'معرفی فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-20T20:06:56.4737355', 126), NULL, NULL, 1),
  ('c6555422-ef39-4459-91e4-64cd11ebb9d7', N'تماس با ما', N'contact', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'تماس با ما', N'راه‌های ارتباط با پشتیبانی ویتورایز', 0, CONVERT(datetime2, '2026-08-20T20:06:56.4737355', 126), NULL, NULL, 1),
  ('3085ae2f-3217-407b-b9b2-d561327ca478', N'قوانین و مقررات', N'terms', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'قوانین و مقررات', N'قوانین و مقررات استفاده از فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-20T20:06:56.4737355', 126), NULL, NULL, 1),
  ('58791d2c-9a3c-4395-a11f-d8e8fa7bc5e6', N'حریم خصوصی', N'privacy', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'حریم خصوصی', N'سیاست حریم خصوصی فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-20T20:06:56.4737355', 126), NULL, NULL, 1);
GO

PRINT N'Seeding dbo.FontAssets (1 row(s))...';
GO
INSERT INTO dbo.[FontAssets] ([Id], [FamilyName], [FilePath], [FileFormat], [MimeType], [SizeBytes], [IsBuiltIn], [IsActive], [Scope], [CreatedByUserId], [CreatedAt], [UpdatedAt]) VALUES
  ('3cbfe9d5-4d20-402c-afcd-3766ec7b8be3', N'Vazirmatn', NULL, N'woff2', N'font/woff2', 0, 1, 1, 3, NULL, CONVERT(datetime2, '2026-08-20T20:06:51.0895976', 126), NULL);
GO

PRINT N'Seeding dbo.KycPolicies (1 row(s))...';
GO
INSERT INTO dbo.[KycPolicies] ([Id], [Code], [Name], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
  ('0d11426d-e120-4c4b-a5db-101ca14e1252', N'legacy-profile-verification', N'احراز هویت پروفایل (سیاست انتقالی)', 1, CONVERT(datetime2, '2026-08-20T20:06:54.5590534', 126), NULL);
GO

PRINT N'Seeding dbo.KycPolicyVersions (1 row(s))...';
GO
INSERT INTO dbo.[KycPolicyVersions] ([Id], [KycPolicyId], [Version], [Status], [CustomerTitle], [CustomerInstructions], [CreatedAt], [PublishedAt], [CustomerActionDeadlineHours]) VALUES
  ('53d354e8-9482-47b6-ad51-6e44f753479e', '0d11426d-e120-4c4b-a5db-101ca14e1252', 1, 2, N'احراز هویت لازم است', N'برای ادامه خرید، تأیید شماره همراه و احراز هویت حساب خود را تکمیل کنید.', CONVERT(datetime2, '2026-08-20T20:06:54.5590534', 126), CONVERT(datetime2, '2026-08-20T20:06:54.5590534', 126), NULL);
GO


/*----------------------------------------------------------------------------
  Migration ledger: records every script represented by this bootstrap so a
  future versioned deployment applies only newer scripts. Checksums are the
  real canonical values from the deployment manifest.
----------------------------------------------------------------------------*/
PRINT N'Recording deployment history (29 script(s))...';
GO
INSERT INTO dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, AppliedAt, AppliedBy, Environment, Success, Notes) VALUES
  (N'V0001__create_database_script_history.sql', N'V0001', N'0d95329a1e6b5eafbb377b6898f6f43ade76054ad22c970a00c92ffcdc8c6053', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0002__normalize_gift_code_reservation_status_constraint.sql', N'V0002', N'918491680f470df380fff99caaa3b291b8e3354309e28b144945950ae7bc4b45', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'2026-07-13_create_sms_history.sql', N'H20260713-SMS-SCHEMA', N'ece5f2dbebf7266c2c58e079377148a43bc02699d31ff9c3e853ca30b731a8f0', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'2026-07-14_product_experience_schema.sql', N'H20260714-PRODUCT-SCHEMA', N'907cabcb1eefb753ae3b2ff19add608d2f011c448295f2e39a2a22e3799c393c', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0003__seed_reference_roles.sql', N'V0003', N'9cd5ff472bb5d776269b43f14565870c6c1de862b0a275a36e342138e635be35', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0004__financial_integrity_and_security_hardening.sql', N'V0004', N'8a896e8cdbfbee4d84a0c6415192c03cd4fda4088b51828acb73f9ea5c862ef4', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0005__seo_content_and_legacy_redirects.sql', N'V0005', N'ed6b02b7453590d09fc2d1a085ea3e8f006ab66659c046c911196d7af8955b22', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0006__preserve_currency_through_checkout.sql', N'V0006', N'70c4485300b40cc94547177682fba3e82e90a7deb1937d2a66c27ea4be1287cc', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0007__support_fulfillment_ticket_uniqueness.sql', N'V0007', N'b39587eed17e512d60e6db99986d488f1d770c54b02f8cee4fac3e54331d2a10', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'2026-07-08_seed_settings_ui_customization.sql', N'H20260708-UI', N'a9da7ed7e2b87e27298b8005befb10954c228a574786c3cf14f9db8c535b2ed3', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'2026-07-13_seed_sms_settings.sql', N'H20260713-SMS-SEED', N'a950e3b326fe99e197c6e08c0024e0a601e7bfdbcfceb130a40736f8281f2b6e', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'2026-07-14_seed_product_experience_settings.sql', N'H20260714-PRODUCT-SEED', N'90ae9b6278a85536accf28e7a927755b980cc062b07afb65d1a6d43fcaad4c00', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0008__seed_storefront_typography_settings.sql', N'V0008', N'fff9d2f0f22c6ac51629f3edee38c30a3e90dc7433d3914e10fbf2035eaade15', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0009__persistent_guest_carts.sql', N'V0009', N'3763a5b6236065f47b6b461f42188672bbb5584408a495eb7f1bd30c327ab438', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0010__kyc_policy_and_order_item_snapshot.sql', N'V0010', N'029bf21945b4d1f19c14b3620955a64cbcf37512767e92b482ac8b7d5c5557f0', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0011__order_item_kyc_lifecycle_state.sql', N'V0011', N'0f838bdbe7d783d7e28f93a06d7c2f7aefd39e244ba00e11881cd903d6315181', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0012__verification_document_kyc_document_type.sql', N'V0012', N'73d8c8b043c8f610fd8896762fedaf8e7cd56b2333994722478bd9d4d5add73a', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0013__kyc_document_redaction_configuration.sql', N'V0013', N'714524653dbbb8a03e304ad10de1bd7664d643e4f0f9b5904c122c0653e87e98', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0014__kyc_customer_action_deadline.sql', N'V0014', N'10f71bfaea811724ed1a7008ceaf80ba70a0c3b077377dee354d2dc66e23468f', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0015__kyc_finance_resolution.sql', N'V0015', N'29a1af41c42b655785f6c90b419921992bb3029d41f943ebe3a721dc2ed9241a', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0016__order_vat_snapshot.sql', N'V0016', N'e44e9af54422816c603c87ef1812e54a2bd817f2ea815cbf20ae30959f21b16e', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0017__cms_system_pages_seed.sql', N'V0017', N'7929e2007087f635eed95ecaa1b75b205e5cc34a45621e4a5f999d8b2eb538a1', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0018__notification_broadcasts.sql', N'V0018', N'4e331e3b0475f07b4af8f623fcdf49b98cf21a04b62890148ca04916605b1473', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0019__loading_media_setting_seed.sql', N'V0019', N'2397e2acba47a1af09d214d815118b04db61c557b45bac0f2557ae46add0711e', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0020__product_variant_managed_stock.sql', N'V0020', N'1c87108b7e3ae8e762aeb1c3763c558a9adf269e8fb5d22e18805914a3cacdfe', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0021__default_variants_for_managed_products.sql', N'V0021', N'e0640d9ca8c6292bd69d7b53672f6b9cb20f1c832dee1c755d1239ffdb2cd587', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0022__force_out_of_stock_and_product_faqs.sql', N'V0022', N'd1d4e22e1afcde5188c51dc87a43467dd4cd2f1d77175f2ec1553e50ed973fff', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0023__product_categories_and_default_font.sql', N'V0023', N'c0e4ac5aca4fc1b76ce62b136038836a75a66ac3943eed95a7523ee3d751f4d0', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain'),
  (N'V0024__customer_order_visibility.sql', N'V0024', N'b762994c35f64fdd0e779e91536fd28ca4091d8a5b821f8c4d18a13b72a8921b', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain');
GO

PRINT N'Vitorize fresh install completed successfully.';
PRINT N'Next: deploy the API and Web packages, then configure the first administrator.';
GO

SET NOEXEC OFF;
GO