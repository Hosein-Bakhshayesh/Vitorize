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
    CONSTRAINT [PK_FAQs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


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
PRINT N'Creating Index [dbo].[FinancialAuditLogs].[IX_FinancialAuditLogs_Event]...';


GO
CREATE NONCLUSTERED INDEX [IX_FinancialAuditLogs_Event]
    ON [dbo].[FinancialAuditLogs]([EventType] ASC, [CreatedAt] DESC);


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
PRINT N'Creating Index [dbo].[FontAssets].[UX_FontAssets_OneActive]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_FontAssets_OneActive]
    ON [dbo].[FontAssets]([IsActive] ASC) WHERE ([IsActive]=(1));


GO
PRINT N'Creating Index [dbo].[FontAssets].[UX_FontAssets_FamilyName]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_FontAssets_FamilyName]
    ON [dbo].[FontAssets]([FamilyName] ASC);


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
PRINT N'Creating Index [dbo].[GiftCodeReservations].[UX_GiftCodeReservations_Active_GiftCode]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_GiftCodeReservations_Active_GiftCode]
    ON [dbo].[GiftCodeReservations]([GiftCodeId] ASC) WHERE ([Status]=(1));


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
PRINT N'Creating Index [dbo].[GiftCodes].[UX_GiftCodes_CodeHashFingerprint]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_GiftCodes_CodeHashFingerprint]
    ON [dbo].[GiftCodes]([CodeHashFingerprint] ASC) WHERE ([CodeHashFingerprint] IS NOT NULL);


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
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_ProductVariant_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_ProductVariant_Status]
    ON [dbo].[GiftCodes]([ProductVariantId] ASC, [Status] ASC);


GO
PRINT N'Creating Index [dbo].[GiftCodes].[IX_GiftCodes_OrderItemId]...';


GO
CREATE NONCLUSTERED INDEX [IX_GiftCodes_OrderItemId]
    ON [dbo].[GiftCodes]([OrderItemId] ASC);


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
PRINT N'Creating Index [dbo].[IdempotencyKeys].[UX_IdempotencyKeys_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_IdempotencyKeys_Key]
    ON [dbo].[IdempotencyKeys]([Key] ASC);


GO
PRINT N'Creating Index [dbo].[IdempotencyKeys].[IX_IdempotencyKeys_UserId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_IdempotencyKeys_UserId_CreatedAt]
    ON [dbo].[IdempotencyKeys]([UserId] ASC, [CreatedAt] ASC);


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
PRINT N'Creating Index [dbo].[Notifications].[IX_Notifications_UserId_IsRead]...';


GO
CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_IsRead]
    ON [dbo].[Notifications]([UserId] ASC, [IsRead] ASC);


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
PRINT N'Creating Index [dbo].[OrderItems].[IX_OrderItems_KycPolicyVersionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_OrderItems_KycPolicyVersionId]
    ON [dbo].[OrderItems]([KycPolicyVersionId] ASC) WHERE ([KycPolicyVersionId] IS NOT NULL);


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
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Orders].[UX_Orders_OrderNumber]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Orders_OrderNumber]
    ON [dbo].[Orders]([OrderNumber] ASC);


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
PRINT N'Creating Index [dbo].[OtpCodes].[UX_OtpCodes_OneActive_Mobile_Purpose]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_OtpCodes_OneActive_Mobile_Purpose]
    ON [dbo].[OtpCodes]([Mobile] ASC, [Purpose] ASC) WHERE ([Mobile] IS NOT NULL AND [ConsumedAt] IS NULL);


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
PRINT N'Creating Index [dbo].[PaymentCallbacks].[IX_PaymentCallbacks_PaymentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_PaymentCallbacks_PaymentId]
    ON [dbo].[PaymentCallbacks]([PaymentId] ASC);


GO
PRINT N'Creating Index [dbo].[PaymentCallbacks].[UX_PaymentCallbacks_PaymentId_CallbackKey]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentCallbacks_PaymentId_CallbackKey]
    ON [dbo].[PaymentCallbacks]([PaymentId] ASC, [CallbackKey] ASC) WHERE ([CallbackKey] IS NOT NULL);


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
PRINT N'Creating Index [dbo].[Payments].[UX_Payments_Gateway_Authority]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payments_Gateway_Authority]
    ON [dbo].[Payments]([Gateway] ASC, [Authority] ASC) WHERE ([Authority] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_OrderId_Status]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_OrderId_Status]
    ON [dbo].[Payments]([OrderId] ASC, [Status] ASC, [RequestedAt] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[UX_Payments_IdempotencyKey]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payments_IdempotencyKey]
    ON [dbo].[Payments]([IdempotencyKey] ASC) WHERE ([IdempotencyKey] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_Authority]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_Authority]
    ON [dbo].[Payments]([Authority] ASC) WHERE ([Authority] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_TransactionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_TransactionId]
    ON [dbo].[Payments]([TransactionId] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_OrderId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_OrderId]
    ON [dbo].[Payments]([OrderId] ASC);


GO
PRINT N'Creating Index [dbo].[Payments].[IX_Payments_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Payments_UserId]
    ON [dbo].[Payments]([UserId] ASC);


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
PRINT N'Creating Index [dbo].[ProductInputFields].[IX_ProductInputFields_Product_Order]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductInputFields_Product_Order]
    ON [dbo].[ProductInputFields]([ProductId] ASC, [SortOrder] ASC, [Id] ASC);


GO
PRINT N'Creating Index [dbo].[ProductInputFields].[UX_ProductInputFields_Product_Key]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductInputFields_Product_Key]
    ON [dbo].[ProductInputFields]([ProductId] ASC, [Key] ASC);


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
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_UserId]
    ON [dbo].[ProductReviews]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_ProductId]
    ON [dbo].[ProductReviews]([ProductId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_ParentId]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_ParentId]
    ON [dbo].[ProductReviews]([ParentId] ASC);


GO
PRINT N'Creating Index [dbo].[ProductReviews].[IX_ProductReviews_IsApproved]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductReviews_IsApproved]
    ON [dbo].[ProductReviews]([IsApproved] ASC);


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
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_CategoryId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_CategoryId]
    ON [dbo].[Products]([CategoryId] ASC);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_KycPolicyVersionId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_KycPolicyVersionId]
    ON [dbo].[Products]([KycPolicyVersionId] ASC) WHERE ([KycPolicyVersionId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[Products].[IX_Products_BrandId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Products_BrandId]
    ON [dbo].[Products]([BrandId] ASC);


GO
PRINT N'Creating Index [dbo].[Products].[UX_Products_Slug]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Slug]
    ON [dbo].[Products]([Slug] ASC);


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
PRINT N'Creating Index [dbo].[ProductVariants].[IX_ProductVariants_StockMode_StockQuantity]...';


GO
CREATE NONCLUSTERED INDEX [IX_ProductVariants_StockMode_StockQuantity]
    ON [dbo].[ProductVariants]([StockMode] ASC, [StockQuantity] ASC)
    INCLUDE([ProductId], [IsActive]);


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
PRINT N'Creating Index [dbo].[SecurityLogs].[IX_SecurityLogs_EventType_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SecurityLogs_EventType_CreatedAt]
    ON [dbo].[SecurityLogs]([EventType] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[SecurityLogs].[IX_SecurityLogs_UserId_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SecurityLogs_UserId_CreatedAt]
    ON [dbo].[SecurityLogs]([UserId] ASC, [CreatedAt] DESC);


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
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_SendType_CreatedAt]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_SendType_CreatedAt]
    ON [dbo].[SmsMessages]([SendType] ASC, [CreatedAt] DESC);


GO
PRINT N'Creating Index [dbo].[SmsMessages].[IX_SmsMessages_PublicReference]...';


GO
CREATE NONCLUSTERED INDEX [IX_SmsMessages_PublicReference]
    ON [dbo].[SmsMessages]([PublicReference] ASC) WHERE ([PublicReference] IS NOT NULL);


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
PRINT N'Creating Index [dbo].[Tickets].[IX_Tickets_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_Tickets_UserId]
    ON [dbo].[Tickets]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[Tickets].[UX_Tickets_OneFulfillmentPerOrder]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tickets_OneFulfillmentPerOrder]
    ON [dbo].[Tickets]([OrderId] ASC) WHERE ([IsFulfillmentTicket]=(1) AND [OrderId] IS NOT NULL);


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
PRINT N'Creating Index [dbo].[VerificationDocuments].[IX_VerificationDocuments_KycDocumentTypeId]...';


GO
CREATE NONCLUSTERED INDEX [IX_VerificationDocuments_KycDocumentTypeId]
    ON [dbo].[VerificationDocuments]([KycDocumentTypeId] ASC) WHERE ([KycDocumentTypeId] IS NOT NULL);


GO
PRINT N'Creating Index [dbo].[VerificationDocuments].[IX_VerificationDocuments_ProfileId]...';


GO
CREATE NONCLUSTERED INDEX [IX_VerificationDocuments_ProfileId]
    ON [dbo].[VerificationDocuments]([UserVerificationProfileId] ASC);


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
PRINT N'Creating Index [dbo].[WalletTransactions].[IX_WalletTransactions_WalletId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WalletTransactions_WalletId]
    ON [dbo].[WalletTransactions]([WalletId] ASC);


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
PRINT N'Creating Index [dbo].[WishList].[IX_WishList_UserId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WishList_UserId]
    ON [dbo].[WishList]([UserId] ASC);


GO
PRINT N'Creating Index [dbo].[WishList].[IX_WishList_ProductId]...';


GO
CREATE NONCLUSTERED INDEX [IX_WishList_ProductId]
    ON [dbo].[WishList]([ProductId] ASC);


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
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Banners]...';


GO
ALTER TABLE [dbo].[Banners]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Brands]...';


GO
ALTER TABLE [dbo].[Brands]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Brands]...';


GO
ALTER TABLE [dbo].[Brands]
    ADD DEFAULT ((1)) FOR [IsActive];


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
    ADD DEFAULT ((1)) FOR [Quantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CartItems]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_CartItems_InputFingerprint]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [DF_CartItems_InputFingerprint] DEFAULT ('NONE') FOR [InputFingerprint];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CartItems]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


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
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Categories]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT ((0)) FOR [UsedCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Coupons]...';


GO
ALTER TABLE [dbo].[Coupons]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CouponUsages]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD DEFAULT (sysutcdatetime()) FOR [UsedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[CouponUsages]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


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
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[FAQs]...';


GO
ALTER TABLE [dbo].[FAQs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeBatches]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD DEFAULT (sysutcdatetime()) FOR [ImportedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT (sysutcdatetime()) FOR [ReservedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT ((1)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodeReservations]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT ((1)) FOR [EncryptionVersion];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[IdempotencyKeys]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[IdempotencyKeys]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_IdempotencyKeys_Status]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD CONSTRAINT [DF_IdempotencyKeys_Status] DEFAULT ((1)) FOR [Status];


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
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Notifications]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT ((1)) FOR [IsVisibleToCustomer];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItemDeliveries]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD DEFAULT ((0)) FOR [DeliveryType];


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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [DeliveryStatus];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((1)) FOR [Quantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [DeliveryType];


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
    ADD DEFAULT ((0)) FOR [TotalPrice];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD DEFAULT ((0)) FOR [RequiresVerification];


GO
PRINT N'Creating Default Constraint [dbo].[DF_OrderItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [DF_OrderItems_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


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
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [FinalAmount];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatEnabled]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatEnabled] DEFAULT ((0)) FOR [VatEnabled];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [DiscountAmount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [SubtotalAmount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_VatCalculationMode]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_VatCalculationMode] DEFAULT ((1)) FOR [VatCalculationMode];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Orders_CurrencyType]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [DF_Orders_CurrencyType] DEFAULT ((2)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Orders]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD DEFAULT ((0)) FOR [Status];


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
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OrderStatusHistories]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OtpCodes]...';


GO
ALTER TABLE [dbo].[OtpCodes]
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
    ADD DEFAULT ((5)) FOR [MaxAttempt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT ((0)) FOR [RetryCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[OutboxMessages]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Pages]...';


GO
ALTER TABLE [dbo].[Pages]
    ADD DEFAULT ((1)) FOR [IsPublished];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[PaymentCallbacks]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[PaymentCallbacks]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks]
    ADD DEFAULT (newsequentialid()) FOR [Id];


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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT (sysutcdatetime()) FOR [RequestedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Payments]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD DEFAULT ((0)) FOR [CallbackVerified];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductFeatures_Id]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [DF_ProductFeatures_Id] DEFAULT (newsequentialid()) FOR [Id];


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
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_IsActive]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_RequiresConfirmation]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_RequiresConfirmation] DEFAULT ((0)) FOR [RequiresConfirmation];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_IsSensitive]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_IsSensitive] DEFAULT ((0)) FOR [IsSensitive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_Id]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_Id] DEFAULT (newsequentialid()) FOR [Id];


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
PRINT N'Creating Default Constraint [dbo].[DF_ProductInputFields_SortOrder]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [DF_ProductInputFields_SortOrder] DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsApproved];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [DislikeCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [LikeCount];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsBuyer];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviews]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD DEFAULT ((0)) FOR [IsRejected];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviewVotes]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductReviewVotes]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [RequiresSupportMessage];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [IsFeatured];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [DeliveryType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [SortOrder];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [CurrencyType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Products_KycRequirementMode]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [DF_Products_KycRequirementMode] DEFAULT ((0)) FOR [KycRequirementMode];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [RequiresVerification];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [BasePrice];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((1)) FOR [MinOrderQuantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [ProductType];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((0)) FOR [IsDeleted];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Products]...';


GO
ALTER TABLE [dbo].[Products]
    ADD DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductTags_CreatedAt]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD CONSTRAINT [DF_ProductTags_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductTags_IsActive]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD CONSTRAINT [DF_ProductTags_IsActive] DEFAULT ((1)) FOR [IsActive];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductTags]...';


GO
ALTER TABLE [dbo].[ProductTags]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((0)) FOR [StockMode];


GO
PRINT N'Creating Default Constraint [dbo].[DF_ProductVariants_StockQuantity]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD CONSTRAINT [DF_ProductVariants_StockQuantity] DEFAULT ((0)) FOR [StockQuantity];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT ((1)) FOR [IsActive];


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
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[ProductVariants]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Roles]...';


GO
ALTER TABLE [dbo].[Roles]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Roles]...';


GO
ALTER TABLE [dbo].[Roles]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[SecurityLogs]...';


GO
ALTER TABLE [dbo].[SecurityLogs]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[SecurityLogs]...';


GO
ALTER TABLE [dbo].[SecurityLogs]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Settings]...';


GO
ALTER TABLE [dbo].[Settings]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessageAttempts_Id]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [DF_SmsMessageAttempts_Id] DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessageAttempts_AttemptedAt]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [DF_SmsMessageAttempts_AttemptedAt] DEFAULT (sysutcdatetime()) FOR [AttemptedAt];


GO
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_Id]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_Id] DEFAULT (newsequentialid()) FOR [Id];


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
PRINT N'Creating Default Constraint [dbo].[DF_SmsMessages_CreatedAt]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [DF_SmsMessages_CreatedAt] DEFAULT (sysutcdatetime()) FOR [CreatedAt];


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
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT ((0)) FOR [IsInternalNote];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[TicketMessages]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((0)) FOR [Department];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint [dbo].[DF_Tickets_IsFulfillmentTicket]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [DF_Tickets_IsFulfillmentTicket] DEFAULT ((0)) FOR [IsFulfillmentTicket];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Tickets]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD DEFAULT ((1)) FOR [Priority];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserAddresses]...';


GO
ALTER TABLE [dbo].[UserAddresses]
    ADD DEFAULT ((0)) FOR [IsDefault];


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
    ADD DEFAULT (newsequentialid()) FOR [Id];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [VerificationStatus];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [IsEmailConfirmed];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Users]...';


GO
ALTER TABLE [dbo].[Users]
    ADD DEFAULT ((0)) FOR [IsMobileConfirmed];


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
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[VerificationDocuments]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD DEFAULT ((0)) FOR [Status];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[VerificationDocuments]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD DEFAULT (newsequentialid()) FOR [Id];


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
    ADD DEFAULT (sysutcdatetime()) FOR [CreatedAt];


GO
PRINT N'Creating Default Constraint unnamed constraint on [dbo].[Wallets]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD DEFAULT ((0)) FOR [Balance];


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
ALTER TABLE [dbo].[AuditLogs]
    ADD CONSTRAINT [FK_AuditLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItemInputValues_ProductInputFields]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [FK_CartItemInputValues_ProductInputFields] FOREIGN KEY ([ProductInputFieldId]) REFERENCES [dbo].[ProductInputFields] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItemInputValues_CartItems]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [FK_CartItemInputValues_CartItems] FOREIGN KEY ([CartItemId]) REFERENCES [dbo].[CartItems] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_Products]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [FK_CartItems_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_ProductVariants]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [FK_CartItems_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CartItems_Carts]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [FK_CartItems_Carts] FOREIGN KEY ([CartId]) REFERENCES [dbo].[Carts] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Carts_Users]...';


GO
ALTER TABLE [dbo].[Carts]
    ADD CONSTRAINT [FK_Carts_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Categories_Parent]...';


GO
ALTER TABLE [dbo].[Categories]
    ADD CONSTRAINT [FK_Categories_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Categories] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Orders]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD CONSTRAINT [FK_CouponUsages_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Users]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD CONSTRAINT [FK_CouponUsages_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_CouponUsages_Coupons]...';


GO
ALTER TABLE [dbo].[CouponUsages]
    ADD CONSTRAINT [FK_CouponUsages_Coupons] FOREIGN KEY ([CouponId]) REFERENCES [dbo].[Coupons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_FinancialAuditLogs_Users]...';


GO
ALTER TABLE [dbo].[FinancialAuditLogs]
    ADD CONSTRAINT [FK_FinancialAuditLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_FontAssets_CreatedByUser]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [FK_FontAssets_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_Products]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD CONSTRAINT [FK_GiftCodeBatches_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD CONSTRAINT [FK_GiftCodeBatches_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeBatches_ImportedByAdmin]...';


GO
ALTER TABLE [dbo].[GiftCodeBatches]
    ADD CONSTRAINT [FK_GiftCodeBatches_ImportedByAdmin] FOREIGN KEY ([ImportedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_GiftCodes]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_GiftCodes] FOREIGN KEY ([GiftCodeId]) REFERENCES [dbo].[GiftCodes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_OrderItems]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Users]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Orders]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodeReservations_Products]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [FK_GiftCodeReservations_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_ProductVariants]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD CONSTRAINT [FK_GiftCodes_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_ReservedByUser]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD CONSTRAINT [FK_GiftCodes_ReservedByUser] FOREIGN KEY ([ReservedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_OrderItems]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD CONSTRAINT [FK_GiftCodes_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_GiftCodeBatches]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD CONSTRAINT [FK_GiftCodes_GiftCodeBatches] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[GiftCodeBatches] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_GiftCodes_Products]...';


GO
ALTER TABLE [dbo].[GiftCodes]
    ADD CONSTRAINT [FK_GiftCodes_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_IdempotencyKeys_Users]...';


GO
ALTER TABLE [dbo].[IdempotencyKeys]
    ADD CONSTRAINT [FK_IdempotencyKeys_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyDocumentRequirements_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [FK_KycPolicyDocumentRequirements_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyDocumentRequirements_KycDocumentTypes]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [FK_KycPolicyDocumentRequirements_KycDocumentTypes] FOREIGN KEY ([KycDocumentTypeId]) REFERENCES [dbo].[KycDocumentTypes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_KycPolicyVersions_KycPolicies]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions]
    ADD CONSTRAINT [FK_KycPolicyVersions_KycPolicies] FOREIGN KEY ([KycPolicyId]) REFERENCES [dbo].[KycPolicies] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_NotificationBroadcasts_Users]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [FK_NotificationBroadcasts_Users] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Notifications_Users]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD CONSTRAINT [FK_Notifications_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Notifications_NotificationBroadcasts]...';


GO
ALTER TABLE [dbo].[Notifications]
    ADD CONSTRAINT [FK_Notifications_NotificationBroadcasts] FOREIGN KEY ([BroadcastId]) REFERENCES [dbo].[NotificationBroadcasts] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_GiftCodes]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD CONSTRAINT [FK_OrderItemDeliveries_GiftCodes] FOREIGN KEY ([GiftCodeId]) REFERENCES [dbo].[GiftCodes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_DeliveredByUser]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD CONSTRAINT [FK_OrderItemDeliveries_DeliveredByUser] FOREIGN KEY ([DeliveredByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemDeliveries_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD CONSTRAINT [FK_OrderItemDeliveries_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemInputValues_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [FK_OrderItemInputValues_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemInputValues_ProductInputFields]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [FK_OrderItemInputValues_ProductInputFields] FOREIGN KEY ([ProductInputFieldId]) REFERENCES [dbo].[ProductInputFields] ([Id]) ON DELETE SET NULL;


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycFinanceResolutions_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions]
    ADD CONSTRAINT [FK_OrderItemKycFinanceResolutions_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycFinanceResolutions_ResolvedBy]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions]
    ADD CONSTRAINT [FK_OrderItemKycFinanceResolutions_ResolvedBy] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycStates_OrderItems]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [FK_OrderItemKycStates_OrderItems] FOREIGN KEY ([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItemKycStates_SatisfiedByVerificationProfile]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [FK_OrderItemKycStates_SatisfiedByVerificationProfile] FOREIGN KEY ([SatisfiedByVerificationProfileId]) REFERENCES [dbo].[UserVerificationProfiles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [FK_OrderItems_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Orders]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Tickets]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [FK_OrderItems_Tickets] FOREIGN KEY ([SupportTicketId]) REFERENCES [dbo].[Tickets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_Products]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [FK_OrderItems_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderItems_ProductVariants]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [FK_OrderItems_ProductVariants] FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariants] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Orders_Coupons]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [FK_Orders_Coupons] FOREIGN KEY ([CouponId]) REFERENCES [dbo].[Coupons] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Orders_Users]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [FK_Orders_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderStatusHistories_Orders]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories]
    ADD CONSTRAINT [FK_OrderStatusHistories_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OrderStatusHistories_ChangedByUser]...';


GO
ALTER TABLE [dbo].[OrderStatusHistories]
    ADD CONSTRAINT [FK_OrderStatusHistories_ChangedByUser] FOREIGN KEY ([ChangedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_OtpCodes_Users]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD CONSTRAINT [FK_OtpCodes_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentCallbacks_Payments]...';


GO
ALTER TABLE [dbo].[PaymentCallbacks]
    ADD CONSTRAINT [FK_PaymentCallbacks_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Users]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [FK_PaymentRefunds_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Payments]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [FK_PaymentRefunds_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_RequestedBy]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [FK_PaymentRefunds_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_PaymentRefunds_Orders]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [FK_PaymentRefunds_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Payments_Users]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD CONSTRAINT [FK_Payments_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Payments_Orders]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductFeatures_Products]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [FK_ProductFeatures_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductImages_Products]...';


GO
ALTER TABLE [dbo].[ProductImages]
    ADD CONSTRAINT [FK_ProductImages_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductInputFields_Products]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [FK_ProductInputFields_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_User]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD CONSTRAINT [FK_ProductReviews_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_Parent]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD CONSTRAINT [FK_ProductReviews_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[ProductReviews] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviews_Product]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD CONSTRAINT [FK_ProductReviews_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviewVotes_Review]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD CONSTRAINT [FK_ProductReviewVotes_Review] FOREIGN KEY ([ReviewId]) REFERENCES [dbo].[ProductReviews] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductReviewVotes_User]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD CONSTRAINT [FK_ProductReviewVotes_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_Categories]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [FK_Products_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_Brands]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [FK_Products_Brands] FOREIGN KEY ([BrandId]) REFERENCES [dbo].[Brands] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Products_KycPolicyVersions]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [FK_Products_KycPolicyVersions] FOREIGN KEY ([KycPolicyVersionId]) REFERENCES [dbo].[KycPolicyVersions] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductTagMappings_Products]...';


GO
ALTER TABLE [dbo].[ProductTagMappings]
    ADD CONSTRAINT [FK_ProductTagMappings_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductTagMappings_ProductTags]...';


GO
ALTER TABLE [dbo].[ProductTagMappings]
    ADD CONSTRAINT [FK_ProductTagMappings_ProductTags] FOREIGN KEY ([TagId]) REFERENCES [dbo].[ProductTags] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_ProductVariants_Products]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD CONSTRAINT [FK_ProductVariants_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SecurityLogs_Users]...';


GO
ALTER TABLE [dbo].[SecurityLogs]
    ADD CONSTRAINT [FK_SecurityLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessageAttempts_SmsMessage]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [FK_SmsMessageAttempts_SmsMessage] FOREIGN KEY ([SmsMessageId]) REFERENCES [dbo].[SmsMessages] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_Outbox]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [FK_SmsMessages_Outbox] FOREIGN KEY ([OutboxMessageId]) REFERENCES [dbo].[OutboxMessages] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_CreatedByUser]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [FK_SmsMessages_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_SmsMessages_User]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [FK_SmsMessages_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TicketMessages_Tickets]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD CONSTRAINT [FK_TicketMessages_Tickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_TicketMessages_Users]...';


GO
ALTER TABLE [dbo].[TicketMessages]
    ADD CONSTRAINT [FK_TicketMessages_Users] FOREIGN KEY ([SenderUserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Tickets_Users]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [FK_Tickets_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Tickets_Orders]...';


GO
ALTER TABLE [dbo].[Tickets]
    ADD CONSTRAINT [FK_Tickets_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserAddresses_Users]...';


GO
ALTER TABLE [dbo].[UserAddresses]
    ADD CONSTRAINT [FK_UserAddresses_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRefreshTokens_Users]...';


GO
ALTER TABLE [dbo].[UserRefreshTokens]
    ADD CONSTRAINT [FK_UserRefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Users]...';


GO
ALTER TABLE [dbo].[UserRoles]
    ADD CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserRoles_Roles]...';


GO
ALTER TABLE [dbo].[UserRoles]
    ADD CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserVerificationProfiles_Users]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD CONSTRAINT [FK_UserVerificationProfiles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_UserVerificationProfiles_ReviewedByAdmin]...';


GO
ALTER TABLE [dbo].[UserVerificationProfiles]
    ADD CONSTRAINT [FK_UserVerificationProfiles_ReviewedByAdmin] FOREIGN KEY ([ReviewedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_ReviewedByAdmin]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD CONSTRAINT [FK_VerificationDocuments_ReviewedByAdmin] FOREIGN KEY ([ReviewedByAdminId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_UserVerificationProfiles]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD CONSTRAINT [FK_VerificationDocuments_UserVerificationProfiles] FOREIGN KEY ([UserVerificationProfileId]) REFERENCES [dbo].[UserVerificationProfiles] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_VerificationDocuments_KycDocumentTypes]...';


GO
ALTER TABLE [dbo].[VerificationDocuments]
    ADD CONSTRAINT [FK_VerificationDocuments_KycDocumentTypes] FOREIGN KEY ([KycDocumentTypeId]) REFERENCES [dbo].[KycDocumentTypes] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_Wallets_Users]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD CONSTRAINT [FK_Wallets_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTopUps_User]...';


GO
ALTER TABLE [dbo].[WalletTopUps]
    ADD CONSTRAINT [FK_WalletTopUps_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTransactions_Wallets]...';


GO
ALTER TABLE [dbo].[WalletTransactions]
    ADD CONSTRAINT [FK_WalletTransactions_Wallets] FOREIGN KEY ([WalletId]) REFERENCES [dbo].[Wallets] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WalletTransactions_Users]...';


GO
ALTER TABLE [dbo].[WalletTransactions]
    ADD CONSTRAINT [FK_WalletTransactions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WishList_Product]...';


GO
ALTER TABLE [dbo].[WishList]
    ADD CONSTRAINT [FK_WishList_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id]);


GO
PRINT N'Creating Foreign Key [dbo].[FK_WishList_User]...';


GO
ALTER TABLE [dbo].[WishList]
    ADD CONSTRAINT [FK_WishList_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);


GO
PRINT N'Creating Check Constraint [dbo].[CK_CartItemInputValues_SensitiveStorage]...';


GO
ALTER TABLE [dbo].[CartItemInputValues]
    ADD CONSTRAINT [CK_CartItemInputValues_SensitiveStorage] CHECK ([IsSensitive]=(0) AND [EncryptedValue] IS NULL OR [IsSensitive]=(1) AND [Value] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_CartItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [CK_CartItems_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Carts_ExactlyOneOwner]...';


GO
ALTER TABLE [dbo].[Carts]
    ADD CONSTRAINT [CK_Carts_ExactlyOneOwner] CHECK ([UserId] IS NOT NULL AND [GuestTokenHash] IS NULL OR [UserId] IS NULL AND [GuestTokenHash] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_DatabaseScriptHistory_Hash]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory]
    ADD CONSTRAINT [CK_DatabaseScriptHistory_Hash] CHECK (len([ScriptHash])=(64) AND NOT [ScriptHash] like '%[^0-9a-f]%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_DatabaseScriptHistory_Success]...';


GO
ALTER TABLE [dbo].[DatabaseScriptHistory]
    ADD CONSTRAINT [CK_DatabaseScriptHistory_Success] CHECK ([Success]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Format]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [CK_FontAssets_Format] CHECK ([FileFormat]='ttf' OR [FileFormat]='woff' OR [FileFormat]='woff2');


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Path]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [CK_FontAssets_Path] CHECK ([IsBuiltIn]=(1) AND [FilePath] IS NULL OR [IsBuiltIn]=(0) AND [FilePath] like '/uploads/fonts/%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_FontAssets_Scope]...';


GO
ALTER TABLE [dbo].[FontAssets]
    ADD CONSTRAINT [CK_FontAssets_Scope] CHECK ([Scope]=(3) OR [Scope]=(2) OR [Scope]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_GiftCodeReservations_Status]...';


GO
ALTER TABLE [dbo].[GiftCodeReservations]
    ADD CONSTRAINT [CK_GiftCodeReservations_Status] CHECK ([Status]>=(0) AND [Status]<=(3));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycDocumentTypes_MaxFileSizeBytes]...';


GO
ALTER TABLE [dbo].[KycDocumentTypes]
    ADD CONSTRAINT [CK_KycDocumentTypes_MaxFileSizeBytes] CHECK ([MaxFileSizeBytes]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyDocumentRequirements_RedactionMode]...';


GO
ALTER TABLE [dbo].[KycPolicyDocumentRequirements]
    ADD CONSTRAINT [CK_KycPolicyDocumentRequirements_RedactionMode] CHECK ([RedactionMode]>=(0) AND [RedactionMode]<=(2));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyVersions_CustomerActionDeadlineHours]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions]
    ADD CONSTRAINT [CK_KycPolicyVersions_CustomerActionDeadlineHours] CHECK ([CustomerActionDeadlineHours] IS NULL OR [CustomerActionDeadlineHours]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_KycPolicyVersions_Status]...';


GO
ALTER TABLE [dbo].[KycPolicyVersions]
    ADD CONSTRAINT [CK_KycPolicyVersions_Status] CHECK ([Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_Destination]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [CK_LegacyRedirects_Destination] CHECK (([StatusCode]=(308) OR [StatusCode]=(301)) AND [DestinationPath] IS NOT NULL AND left([DestinationPath],(1))=N'/' OR [StatusCode]=(410) AND [DestinationPath] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_SourcePath]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [CK_LegacyRedirects_SourcePath] CHECK (left([SourcePath],(1))=N'/' AND NOT [SourcePath] like N'%?%' AND NOT [SourcePath] like N'%#%');


GO
PRINT N'Creating Check Constraint [dbo].[CK_LegacyRedirects_StatusCode]...';


GO
ALTER TABLE [dbo].[LegacyRedirects]
    ADD CONSTRAINT [CK_LegacyRedirects_StatusCode] CHECK ([StatusCode]=(410) OR [StatusCode]=(308) OR [StatusCode]=(301));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_Status]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [CK_NotificationBroadcasts_Status] CHECK ([Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_RecipientCount]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [CK_NotificationBroadcasts_RecipientCount] CHECK ([RecipientCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_NotificationBroadcasts_AudienceType]...';


GO
ALTER TABLE [dbo].[NotificationBroadcasts]
    ADD CONSTRAINT [CK_NotificationBroadcasts_AudienceType] CHECK ([AudienceType]=(2) OR [AudienceType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemDeliveries_ManualKey]...';


GO
ALTER TABLE [dbo].[OrderItemDeliveries]
    ADD CONSTRAINT [CK_OrderItemDeliveries_ManualKey] CHECK (([DeliveryType]=(3) OR [DeliveryType]=(2)) AND [ManualDeliveryItemKey]=[OrderItemId] OR NOT ([DeliveryType]=(3) OR [DeliveryType]=(2)) AND [ManualDeliveryItemKey] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemInputValues_SensitiveStorage]...';


GO
ALTER TABLE [dbo].[OrderItemInputValues]
    ADD CONSTRAINT [CK_OrderItemInputValues_SensitiveStorage] CHECK ([IsSensitive]=(0) AND [EncryptedValue] IS NULL OR [IsSensitive]=(1) AND [Value] IS NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemKycFinanceResolutions_Status]...';


GO
ALTER TABLE [dbo].[OrderItemKycFinanceResolutions]
    ADD CONSTRAINT [CK_OrderItemKycFinanceResolutions_Status] CHECK ([Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItemKycStates_Status]...';


GO
ALTER TABLE [dbo].[OrderItemKycStates]
    ADD CONSTRAINT [CK_OrderItemKycStates_Status] CHECK ([Status]=(7) OR [Status]=(6) OR [Status]=(5) OR [Status]=(4) OR [Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_KycCustomerActionDeadlineHours]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [CK_OrderItems_KycCustomerActionDeadlineHours] CHECK ([KycCustomerActionDeadlineHours] IS NULL OR [KycCustomerActionDeadlineHours]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_CurrencyType]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [CK_OrderItems_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_Quantity]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_Prices]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [CK_OrderItems_Prices] CHECK ([UnitPrice]>=(0) AND [TotalPrice]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OrderItems_KycSnapshot]...';


GO
ALTER TABLE [dbo].[OrderItems]
    ADD CONSTRAINT [CK_OrderItems_KycSnapshot] CHECK (([KycRequirementMode]=(2) OR [KycRequirementMode]=(1) OR [KycRequirementMode]=(0)) AND [KycEvaluatedAmount]>=(0) AND ([KycRequirementMode]=(0) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NULL OR [KycRequirementMode]=(1) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NOT NULL OR [KycRequirementMode]=(2) AND [KycThresholdAmount]>(0) AND [KycPolicyVersionId] IS NOT NULL));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_CurrencyType]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [CK_Orders_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_VatSnapshot]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [CK_Orders_VatSnapshot] CHECK ([VatRatePercent]>=(0) AND [VatRatePercent]<=(100) AND [VatAmount]>=(0) AND [VatTaxableAmount]>=(0) AND ([VatCalculationMode]=(2) OR [VatCalculationMode]=(1)) AND ([VatEnabled]=(1) OR [VatAmount]=(0) AND [VatRatePercent]=(0)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Orders_Amounts]...';


GO
ALTER TABLE [dbo].[Orders]
    ADD CONSTRAINT [CK_Orders_Amounts] CHECK ([SubtotalAmount]>=(0) AND [DiscountAmount]>=(0) AND [FinalAmount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OtpCodes_AttemptCount]...';


GO
ALTER TABLE [dbo].[OtpCodes]
    ADD CONSTRAINT [CK_OtpCodes_AttemptCount] CHECK ([AttemptCount]>=(0) AND [MaxAttempt]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OutboxMessages_RetryCount]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD CONSTRAINT [CK_OutboxMessages_RetryCount] CHECK ([RetryCount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_OutboxMessages_Status]...';


GO
ALTER TABLE [dbo].[OutboxMessages]
    ADD CONSTRAINT [CK_OutboxMessages_Status] CHECK ([Status]>=(0) AND [Status]<=(3));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Method]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [CK_PaymentRefunds_Method] CHECK ([Method]=(2) OR [Method]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Amount]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [CK_PaymentRefunds_Amount] CHECK ([Amount]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_PaymentRefunds_Status]...';


GO
ALTER TABLE [dbo].[PaymentRefunds]
    ADD CONSTRAINT [CK_PaymentRefunds_Status] CHECK ([Status]=(4) OR [Status]=(3) OR [Status]=(2) OR [Status]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Payments_Amount]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD CONSTRAINT [CK_Payments_Amount] CHECK ([Amount]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Payments_CurrencyType]...';


GO
ALTER TABLE [dbo].[Payments]
    ADD CONSTRAINT [CK_Payments_CurrencyType] CHECK ([CurrencyType]=(2) OR [CurrencyType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductFeatures_Title_NotBlank]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [CK_ProductFeatures_Title_NotBlank] CHECK (len(ltrim(rtrim([Title])))>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductFeatures_Value_NotBlank]...';


GO
ALTER TABLE [dbo].[ProductFeatures]
    ADD CONSTRAINT [CK_ProductFeatures_Value_NotBlank] CHECK (len(ltrim(rtrim([Value])))>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Stage]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [CK_ProductInputFields_Stage] CHECK ([DisplayStage]=(2) OR [DisplayStage]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Type]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [CK_ProductInputFields_Type] CHECK ([FieldType]>=(1) AND [FieldType]<=(12));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductInputFields_Length]...';


GO
ALTER TABLE [dbo].[ProductInputFields]
    ADD CONSTRAINT [CK_ProductInputFields_Length] CHECK ([MinLength] IS NULL OR [MinLength]>=(0) AND ([MaxLength] IS NULL OR [MaxLength]>=[MinLength]));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductReviews_Rating]...';


GO
ALTER TABLE [dbo].[ProductReviews]
    ADD CONSTRAINT [CK_ProductReviews_Rating] CHECK ([Rating]>=(1) AND [Rating]<=(5));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductReviewVotes_VoteType]...';


GO
ALTER TABLE [dbo].[ProductReviewVotes]
    ADD CONSTRAINT [CK_ProductReviewVotes_VoteType] CHECK ([VoteType]=(2) OR [VoteType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Products_KycConfiguration]...';


GO
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [CK_Products_KycConfiguration] CHECK ([KycRequirementMode]=(0) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NULL OR [KycRequirementMode]=(1) AND [KycThresholdAmount] IS NULL AND [KycPolicyVersionId] IS NOT NULL OR [KycRequirementMode]=(2) AND [KycThresholdAmount]>(0) AND [KycPolicyVersionId] IS NOT NULL);


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductVariants_StockQuantity_NonNegative]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD CONSTRAINT [CK_ProductVariants_StockQuantity_NonNegative] CHECK ([StockQuantity]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_ProductVariants_Prices]...';


GO
ALTER TABLE [dbo].[ProductVariants]
    ADD CONSTRAINT [CK_ProductVariants_Prices] CHECK ([Price]>=(0) AND ([DiscountPrice] IS NULL OR [DiscountPrice]>=(0)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessageAttempts_Status]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [CK_SmsMessageAttempts_Status] CHECK ([Status]>=(0) AND [Status]<=(7));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessageAttempts_Number]...';


GO
ALTER TABLE [dbo].[SmsMessageAttempts]
    ADD CONSTRAINT [CK_SmsMessageAttempts_Number] CHECK ([AttemptNumber]>(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_SendType]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [CK_SmsMessages_SendType] CHECK ([SendType]=(3) OR [SendType]=(2) OR [SendType]=(1));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_RetryCount]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [CK_SmsMessages_RetryCount] CHECK ([RetryCount]>=(0) AND ([MaxRetryCount]>=(1) AND [MaxRetryCount]<=(10)));


GO
PRINT N'Creating Check Constraint [dbo].[CK_SmsMessages_Status]...';


GO
ALTER TABLE [dbo].[SmsMessages]
    ADD CONSTRAINT [CK_SmsMessages_Status] CHECK ([Status]>=(0) AND [Status]<=(7));


GO
PRINT N'Creating Check Constraint [dbo].[CK_Wallets_Balance]...';


GO
ALTER TABLE [dbo].[Wallets]
    ADD CONSTRAINT [CK_Wallets_Balance] CHECK ([Balance]>=(0));


GO
PRINT N'Creating Check Constraint [dbo].[CK_WalletTransactions_Amount]...';


GO
ALTER TABLE [dbo].[WalletTransactions]
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
PRINT N'Update complete.';


GO

PRINT N'Seeding dbo.Roles (4 row(s))...';
GO
INSERT INTO dbo.[Roles] ([Id], [Name], [DisplayName], [CreatedAt]) VALUES
  ('4069d4b8-3162-4111-bfd2-0e79a427f6e3', N'Customer', N'مشتری', CONVERT(datetime2, '2026-08-18T08:27:29.8889787', 126)),
  ('7616f4e3-4e4c-46c8-81ad-52aff16c3cad', N'Admin', N'مدیر فروشگاه', CONVERT(datetime2, '2026-08-18T08:27:29.8889787', 126)),
  ('a1520f4e-a698-4bc4-ae24-8b2093b8c1f4', N'SuperAdmin', N'مدیر کل', CONVERT(datetime2, '2026-08-18T08:27:29.8889787', 126)),
  ('e13b6a0d-8e8f-40ac-a12d-bbb557870062', N'Support', N'پشتیبان', CONVERT(datetime2, '2026-08-18T08:27:29.8889787', 126));
GO

PRINT N'Seeding dbo.Settings (163 row(s))...';
GO
INSERT INTO dbo.[Settings] ([Id], [Key], [Value], [GroupName], [ValueType], [Description], [UpdatedAt]) VALUES
  ('5d4e9286-4903-4709-86eb-009e3110b294', N'WorkingHours', N'شنبه تا پنجشنبه، ۹ تا ۱۸', N'Contact', N'string', N'ساعات کاری', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('3a899f7b-8aaf-49f0-b971-010db84273f0', N'TrustSeal.Samandehi.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی samandehi.ir', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('78273b93-b30d-4e63-a48c-01be2484b656', N'Sms.OrderPaidTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('c6e88b74-589e-414a-9a5c-023c0e61e60e', N'LoadingMediaPath', N'', N'Logos', N'image', N'تصویر یا GIF بارگذاری اولیه (خالی = لودر پیش‌فرض ویتورایز)', CONVERT(datetime2, '2026-08-18T08:27:37.7990014', 126)),
  ('65369ed3-1c2a-452a-9d55-03a274eed7cb', N'TrustSeal.Ecunion.Title', N'اتحادیه کسب‌وکارهای مجازی', N'TrustSeals', N'string', N'عنوان مجوز', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('d6759c1e-0ed6-44e4-b794-04d7dd0b7286', N'Branding.AssetVersion', N'1', N'Branding', N'string', N'نسخه کش دارایی‌های برند', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('94c77d3e-fca4-468b-a87c-051fe7ad3570', N'Security.AuditRetentionDays', N'730', N'Security', N'int', N'مدت نگهداری رویدادهای ممیزی', NULL),
  ('ac9d99cd-208f-4ad3-bfd0-08d3c5dbdffc', N'Typography.MaxUploadMb', N'5', N'Typography', N'int', N'حداکثر حجم فونت', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('49f2d3cb-a3bd-4d9d-be0a-08f41203053f', N'Error500Text', N'مشکلی در سرور رخ داد. تیم ما در حال بررسی است.', N'Errors', N'string', N'متن ۵۰۰', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ca116290-14e3-4712-af96-0aaccf284acd', N'Typography.FontPath', N'', N'Typography', N'string', N'مسیر فایل فونت فعال', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('43b99451-f382-4431-9de8-0d492db71f9c', N'Sms.LogSensitiveData', N'false', N'SMS', N'bool', N'لاگ‌کردن داده حساس (فقط توسعه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('fd703c3c-d007-498b-9696-0e7bf71ac890', N'TrustSeal.Samandehi.Title', N'نشان ملی ثبت رسانه‌های دیجیتال', N'TrustSeals', N'string', N'عنوان نشان', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('71cafcac-aa2e-48f5-aaf5-11317a45cc2c', N'NewsletterTitle', N'از جدیدترین‌ها باخبر شو', N'Homepage', N'string', N'عنوان خبرنامه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('1d546d21-5bb2-4eac-bb39-12e283cc4187', N'TrustSeal.Samandehi.Alt', N'نشان ساماندهی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('81d37c9a-8281-48ce-9381-15fc36ffa4ec', N'Error400Title', N'درخواست نامعتبر', N'Errors', N'string', N'عنوان ۴۰۰', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0e599888-c1f4-4188-9ced-18913aa03e5f', N'Seo.CanonicalBaseUrl', N'', N'SEO', N'string', N'آدرس پایه HTTPS و میزبان اصلی برای canonical، robots و sitemap', CONVERT(datetime2, '2026-08-18T08:27:30.5789691', 126)),
  ('1995f51a-0775-4f68-949a-18ab12b893f4', N'NewsletterSubtitle', N'با عضویت در خبرنامه، از تخفیف‌ها و محصولات تازه زودتر از همه مطلع شو.', N'Homepage', N'string', N'زیرعنوان خبرنامه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d1c777e9-a428-48fd-b01e-199b13767975', N'FacebookUrl', N'', N'Social', N'string', N'فیسبوک', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('626b9cb4-0564-405d-8a8c-1b8ce49340bd', N'SmtpFromEmail', N'', N'Email', N'string', N'ایمیل فرستنده', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('00714d17-4c62-4fce-a84c-1d2bf81cb8f9', N'TrustSeal.Ecunion.Alt', N'مجوز اتحادیه کسب‌وکارهای مجازی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('e74bd97b-3563-43fe-8381-1db859648772', N'MetaDescription', N'خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.', N'SEO', N'string', N'توضیح متای پیش‌فرض', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('7980358c-1d57-4531-9402-1f05bad18dc9', N'Sms.VerificationRejectedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('24929aea-8e45-4a88-8791-1fd78c635f00', N'Sms.AllowImmediateSend', N'false', N'SMS', N'bool', N'اجازه ارسال فوری به جای صف', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('ebf50cb3-8201-4d58-b5c7-20ee2b93af11', N'Security.RefreshTokenRetentionDays', N'30', N'Security', N'int', N'مدت نگهداری توکن‌های منقضی یا لغوشده', NULL),
  ('67797997-9512-4b58-bf6f-21ae58667507', N'Sms.DailySmsLimitPerMobile', N'30', N'SMS', N'int', N'سقف پیامک روزانه برای هر شماره', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('1cdd5b1a-89ee-44a6-814f-26442819e3fd', N'EmptyWishlistText', N'هنوز محصولی به علاقه‌مندی‌ها اضافه نکرده‌اید.', N'Empty', N'string', N'علاقه‌مندی خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('e6db9900-66d0-4382-96ad-2a0d01a37350', N'EmptyOrdersText', N'هنوز سفارشی ثبت نکرده‌اید.', N'Empty', N'string', N'سفارش‌های خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('015eb450-d018-4ce1-a746-2afeb5ed0e9b', N'Sms.ApiKey', N'', N'SMS', N'secret', N'کلید API پنل SMS.ir (محرمانه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('bc4873c6-614e-40aa-861c-2bd9f8d56e96', N'LinkedInUrl', N'', N'Social', N'string', N'لینکدین', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('e309d810-9e0a-4d15-87bf-2c98d289e658', N'TrustSeal.Enamad.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش Enamad', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('747b851a-289c-4cd7-832c-2e4cc4eeaa10', N'TrustSeal.Enamad.ImagePath', N'', N'TrustSeals', N'image', N'تصویر نماد', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('cb1ca3d8-bfd6-4683-a69a-3090ed5ad26d', N'TwitterImagePath', N'', N'Logos', N'image', N'تصویر توییتر / X', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('c3e4cbc3-59ca-4f1a-b52e-311b5f46b6ba', N'EmptySearchText', N'نتیجه‌ای برای جستجوی شما پیدا نشد.', N'Empty', N'string', N'جستجوی بدون نتیجه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('b561c766-5de3-45a0-8d07-35b45cf9f067', N'StorefrontEnglishFont', N'Funnel Display', N'Typography', N'font', N'Default English storefront font.', CONVERT(datetime2, '2026-08-18T08:27:32.6846392', 126)),
  ('db309f0a-9a6c-4736-8897-36643b091768', N'WhatsAppUrl', N'', N'Social', N'string', N'واتساپ', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('098434a9-9576-42ed-92c2-37210eedcd87', N'YouTubeUrl', N'', N'Social', N'string', N'یوتیوب', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('b6bdddbe-88e4-4faf-a6f5-37a1ab5f1646', N'Typography.FontFamily', N'Vazirmatn', N'Typography', N'string', N'نام فونت فعال؛ پیش‌فرض Vazirmatn', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('87cba870-ac2b-494f-ba1d-3970a0c2c2d1', N'TrustSeal.Ecunion.SortOrder', N'20', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('2192bc91-a625-49a5-beba-39d8425b3e11', N'Error400Text', N'درخواست شما معتبر نیست. لطفاً دوباره تلاش کنید.', N'Errors', N'string', N'متن ۴۰۰', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('21598734-e85c-41ec-872a-3d4d2918bd6c', N'Sms.MaxRetryCount', N'5', N'SMS', N'int', N'حداکثر تعداد بازتلاش ارسال', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('943ee88d-a03e-4d95-971c-3dba8500ac90', N'MetaKeywords', N'گیفت کارت, اشتراک, خدمات دیجیتال, بازی, گیمینگ, ویتورایز', N'SEO', N'string', N'کلمات کلیدی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ded4422a-8752-4f98-827c-3fcfa5b37d9a', N'ContactAddress', N'', N'Contact', N'string', N'آدرس', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('5699c399-71a3-4ecd-aeea-412730a7ca42', N'Sms.AllowAdminViewFullMobile', N'false', N'SMS', N'bool', N'اجازه مشاهده شماره کامل برای مدیر کل', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('04703507-69a2-45b9-b2c7-41e6d6cfbee4', N'Sms.ForgotPasswordTemplateId', N'', N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('89fa5ee9-a60b-4aed-9acf-43441434a56d', N'MinPasswordLength', N'8', N'Security', N'int', N'حداقل طول رمز', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('4eada553-8a30-45ba-bc44-43953721ebe4', N'Sms.DailyOtpLimitPerMobile', N'10', N'SMS', N'int', N'سقف کد روزانه برای هر شماره', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('2eb2bdeb-9217-4134-8a76-440ce6c5be58', N'LogoSmallPath', N'', N'Logos', N'image', N'لوگوی کوچک / آیکون', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('4a056f2b-57f2-4928-99ac-47e246abb639', N'Sms.HistoryRetentionDays', N'180', N'SMS', N'int', N'مدت نگهداری تاریخچه پیامک بر حسب روز', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('5029a817-93a2-4f95-b0bc-48e84c2b9295', N'SocialPreviewImagePath', N'', N'Logos', N'image', N'تصویر پیش‌نمایش شبکه‌های اجتماعی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ad2e1519-9147-4b78-9527-4a437d893282', N'Typography.Version', N'1', N'Typography', N'string', N'نسخه کش فونت', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('025fa8ac-3133-4a8d-970e-4ede0c1b2138', N'BrandPrimaryColor', N'', N'Branding', N'color', N'رنگ اصلی برند', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('f5c6b4a5-3e03-4f43-893f-4f9bacdbf733', N'Sms.DefaultLineNumber', N'', N'SMS', N'string', N'شماره خط اختصاصی برای پیامک متنی (محرمانه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('1a19510f-c68c-4042-bcc2-52ec8921de55', N'Security.OutboxLockTimeoutMinutes', N'5', N'Security', N'int', N'زمان بازیابی پیام Outbox قفل‌شده', NULL),
  ('bde1bab4-a787-4251-988a-53a20027fdf1', N'HeroSecondaryCtaUrl', N'/categories', N'Homepage', N'string', N'لینک دکمه دوم Hero', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d1b91fdd-3725-475c-b760-5751da969d66', N'Sms.LoginOtpTemplateId', N'', N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('5abb1706-4317-462b-a400-590535d12f3d', N'EmptyStateIllustrationPath', N'', N'Logos', N'image', N'تصویر پیش‌فرض حالت خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('2bb688eb-06cf-4a57-9c20-5a70f49e46a7', N'EmptyTicketsText', N'تیکتی ثبت نکرده‌اید.', N'Empty', N'string', N'تیکت خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d042efb2-dc27-408c-9897-5acaf958baa0', N'Sms.TicketReplyTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('b0eb7041-ec57-457b-9d47-5b7c8b64ec00', N'TrustBadgesJson', N'[{"icon":"shield-check","title":"تضمین اصالت","text":"محصولات رسمی و اورجینال"},{"icon":"zap","title":"تحویل آنی","text":"سریع و بدون انتظار"},{"icon":"headphones","title":"پشتیبانی ۲۴/۷","text":"همیشه کنار شما"},{"icon":"lock","title":"پرداخت امن","text":"درگاه‌های معتبر"}]', N'Trust', N'json', N'نشان‌های اعتماد', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('2d7dabc5-5cfb-4c9b-ba29-5dc740bfc385', N'SmtpHost', N'', N'Email', N'string', N'میزبان SMTP', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('273f48e9-0d8f-4562-9467-60a2ac41abe2', N'Sms.IsEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی سرویس پیامک SMS.ir', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('c7b16c09-1833-4737-9798-60c7ad69a305', N'Sms.CustomTextEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی پیامک متنی سفارشی', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('2c06ac58-d9f9-42c2-81c9-60e31636db7d', N'Security.OtpRetentionDays', N'7', N'Security', N'int', N'مدت نگهداری سوابق کد یکبار مصرف', NULL),
  ('a49f72bf-af13-4cc7-9585-619bdf5e6d13', N'HomeFeaturesJson', N'[{"icon":"layout-grid","title":"انتخاب محصول","text":"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن."},{"icon":"credit-card","title":"پرداخت امن","text":"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده."},{"icon":"zap","title":"تحویل آنی","text":"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود."}]', N'Trust', N'json', N'مراحل صفحه اول', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('792ef9ad-7984-4647-874f-679413f12164', N'EmptyCartText', N'سبد خرید شما خالی است.', N'Empty', N'string', N'سبد خرید خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('b5a2be2f-7fe3-44c4-b150-69baa5246368', N'EmptyNotificationsText', N'اعلان جدیدی ندارید.', N'Empty', N'string', N'اعلان خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('423712b5-7064-44e2-84de-69cce76d3851', N'Sms.MaxCustomTextLength', N'500', N'SMS', N'int', N'حداکثر طول پیامک متنی سفارشی', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('003ae0bb-1167-4169-a11a-69ddc35f686c', N'Error503Title', N'در حال به‌روزرسانی', N'Errors', N'string', N'عنوان ۵۰۳', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('20cdd84c-8621-4f3b-8518-6b1fd20219a8', N'RequireEmailConfirmation', N'false', N'Security', N'bool', N'الزام تأیید ایمیل', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('52cdeacc-f2e9-4218-a848-6c148275f72d', N'TrustSeal.Enamad.Title', N'نماد اعتماد الکترونیکی', N'TrustSeals', N'string', N'عنوان نماد', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('fe78fdfd-6b0e-440d-811f-6d1af54f8894', N'HeroSecondaryCtaText', N'دسته‌بندی‌ها', N'Homepage', N'string', N'متن دکمه دوم Hero', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('dd3d52ca-85d0-4d5e-ab9a-6fe6893c95c5', N'Error401Title', N'نیاز به ورود', N'Errors', N'string', N'عنوان ۴۰۱', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0887598b-f2a1-46a2-966c-71cf0e0c879b', N'OgImagePath', N'', N'Logos', N'image', N'تصویر OpenGraph', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('44f7f838-534c-4cab-95f8-720bdb0d8b39', N'OfflineText', N'به نظر می‌رسد اینترنت شما قطع شده است.', N'Errors', N'string', N'متن آفلاین', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('8f0b7646-36bc-486c-a80b-7364b6ebfa6a', N'SessionExpiredText', N'برای ادامه دوباره وارد شوید.', N'Errors', N'string', N'متن نشست منقضی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('f3d66a98-d6b7-4509-ab33-73895f63c99e', N'SmtpPort', N'587', N'Email', N'int', N'پورت SMTP', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('853273e1-bb8d-4139-8961-75fb3ea44b0f', N'NetworkErrorText', N'ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید.', N'Errors', N'string', N'متن خطای شبکه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('b8462382-2d1b-47b7-87cf-762ac9cefb42', N'Error500Title', N'خطای غیرمنتظره', N'Errors', N'string', N'عنوان ۵۰۰', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d80b8900-1b7d-4f34-9996-7998deec7935', N'Sms.GiftCodeDeliveredTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('af0f0d25-b788-42cd-a460-7a5352d94028', N'Error404Title', N'صفحه پیدا نشد', N'Errors', N'string', N'عنوان ۴۰۴', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ae80cd5b-8cbf-4f32-b519-7b1a933d1d16', N'MaintenanceIllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه تعمیر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('dedf0e8a-e326-43cd-a243-7c799329f8b1', N'Typography.FontFormat', N'woff2', N'Typography', N'string', N'فرمت فایل فونت فعال', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('059b491d-05a3-4f6c-b85a-7cfbb751ca8b', N'TrustSeal.Enamad.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی enamad.ir', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('25547d10-3cf3-4012-b2d4-7cfc71a8371e', N'Error500IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۵۰۰', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('f25a8a2f-f8d7-40cb-b061-7e3f93b5e258', N'PageRemovedText', N'محتوایی که دنبال آن بودید دیگر در دسترس نیست.', N'Errors', N'string', N'متن صفحه حذف‌شده', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d492c116-8481-466a-a1a3-7e565872b901', N'TrustSeal.Enamad.SortOrder', N'10', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('c61a6416-f230-4489-afd6-7f62c05a31a9', N'Error404Text', N'صفحه‌ای که دنبال آن هستید وجود ندارد یا منتقل شده است.', N'Errors', N'string', N'متن ۴۰۴', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('61a6abbf-ba03-47a6-9891-80f908d49a4b', N'TrustSeal.Samandehi.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('22615ff7-9e41-456b-bb82-862e41573223', N'FaviconPath', N'', N'Logos', N'image', N'فاوآیکون سایت', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('fe472223-1fa2-4e03-9a34-86bb6818e2b7', N'Sms.OtpResendCooldownSeconds', N'90', N'SMS', N'int', N'فاصله ارسال مجدد کد (ثانیه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('fe3c8dc7-95d3-41ec-adf8-86c1648ed6ea', N'Sms.VerificationApprovedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('34096e33-e155-435b-ad03-8a212d91d3e6', N'LogoDarkPath', N'', N'Logos', N'image', N'لوگوی تم تیره', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('501c1983-56db-42be-87e9-8aeaedc1390b', N'Sms.OrderCompletedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('30ce6fb5-ad51-4be4-830e-8b36219e319f', N'XUrl', N'', N'Social', N'string', N'X (توییتر)', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('77b0f20b-f51e-48b5-9b2e-8c325e3304f1', N'HomeFeaturesKicker', N'چرا ویتورایز؟', N'Trust', N'string', N'برچسب بخش چرا ما', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('2b877751-787f-420f-96c4-8f76470bfec4', N'Security.KycRejectedRetentionDays', N'90', N'Security', N'int', N'مدت نگهداری مدارک ردشده احراز هویت', NULL),
  ('005cf3d2-41cb-4294-84bf-8fec5f6de1ef', N'FooterLogoPath', N'', N'Logos', N'image', N'لوگوی فوتر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('17071c07-26d8-47ff-9fd0-8ffe135b1a8d', N'StorefrontPersianFont', N'Peyda', N'Typography', N'font', N'Default Persian storefront font.', CONVERT(datetime2, '2026-08-18T08:27:32.6846392', 126)),
  ('ac785021-d12b-4276-9b53-90c63a8e6d56', N'MetaTitle', N'ویتورایز | بازارگاه دیجیتال گیمینگ و خدمات آنلاین', N'SEO', N'string', N'عنوان متای پیش‌فرض', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('d1218e36-6d5c-47d4-af72-914ab8e22088', N'OfflineTitle', N'اتصال اینترنت قطع است', N'Errors', N'string', N'عنوان آفلاین', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('37fd6db5-451d-4a87-9e46-92515989db39', N'MaxUploadSizeMb', N'2', N'Uploads', N'int', N'حداکثر حجم آپلود (مگابایت)', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('1ce38626-ef7c-43d4-b72c-936421406483', N'NetworkErrorTitle', N'خطای ارتباط', N'Errors', N'string', N'عنوان خطای شبکه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('f1127236-88c7-4a6a-9654-94f3b9903f84', N'PageRemovedTitle', N'این صفحه حذف شده است', N'Errors', N'string', N'عنوان صفحه حذف‌شده', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('4d2f4fd8-496c-4f6f-826a-957369077494', N'VatRatePercent', N'0', N'Tax', N'decimal', N'نرخ مالیات بر ارزش افزوده (درصد)', CONVERT(datetime2, '2026-08-18T08:27:36.2068292', 126)),
  ('f14fdaca-a7df-47ad-97f8-95ba7702a46b', N'FooterText', N'', N'Footer', N'string', N'متن آزاد فوتر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0fb63460-c820-4422-9845-97e41c5d01cd', N'EmptyReviewsText', N'هنوز نظری ثبت نشده است.', N'Empty', N'string', N'نظرات خالی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('35323891-d4a5-46a6-887c-98f0d9e3e206', N'Error403Text', N'شما اجازه دسترسی به این بخش را ندارید.', N'Errors', N'string', N'متن ۴۰۳', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('fd0937c8-1c1f-412f-b99b-9b74e74c5c37', N'Error403Title', N'دسترسی مجاز نیست', N'Errors', N'string', N'عنوان ۴۰۳', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('f79b2bee-8221-4429-8314-9b803a9aa853', N'HeroCtaUrl', N'/shop', N'Homepage', N'string', N'لینک دکمه اصلی Hero', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('e6c1064e-069c-4e0f-a609-9dba4aaa1b1e', N'HomeFeaturesTitle', N'خرید دیجیتال، ساده و مطمئن', N'Trust', N'string', N'عنوان بخش چرا ما', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('cfc93eb1-9f5d-41fe-af0f-a460e1b4668a', N'NoProductsText', N'محصولی برای نمایش وجود ندارد.', N'Empty', N'string', N'نبود محصول', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('3e61bde9-c818-4b49-9eba-a4ad015bb04c', N'SeoTitleTemplate', N'{page} | {site}', N'SEO', N'string', N'قالب عنوان صفحات', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('275d7662-7f75-4260-b6bf-a692ce574461', N'Typography.Scope', N'3', N'Typography', N'int', N'محدوده اعمال: ۱ فروشگاه، ۲ مدیریت، ۳ کل برنامه', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('f401025e-1985-40b0-9910-a8422ffea7d8', N'SmtpUsername', N'', N'Email', N'string', N'نام کاربری SMTP', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0a979487-376b-4317-903e-aa953762041b', N'AboutText', N'ویتورایز بازارگاهی دیجیتال برای خرید امن و آنی گیفت کارت، اشتراک و خدمات آنلاین است.', N'About', N'string', N'متن درباره ما', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('27f7882b-1ad8-43c2-8bbd-ac8ccb58f582', N'MaxLoginAttempts', N'5', N'Security', N'int', N'حداکثر تلاش ورود', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('84e4a29e-5929-4c91-8372-ac8ff73ec64c', N'SessionExpiredTitle', N'نشست شما منقضی شد', N'Errors', N'string', N'عنوان نشست منقضی', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('666ad604-6d1c-4d81-a1d8-afdbd750bd62', N'TrustSeal.Samandehi.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش ساماندهی', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('285fd097-b207-4e23-ba09-b1ce838a9c1e', N'Sms.NotificationTemplateId', N'', N'SMS', N'int', N'شناسه قالب اطلاع‌رسانی عمومی', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('4245906b-885e-4fa3-a2e6-b5a2b0fa955f', N'NewsletterCtaText', N'عضویت', N'Homepage', N'string', N'متن دکمه خبرنامه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('9aecf9ed-beff-4c40-977e-b5e6371719c6', N'AllowedImageFormats', N'jpg,jpeg,png,webp', N'Uploads', N'string', N'فرمت‌های مجاز تصویر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ef793620-161b-4fc8-8c5e-b699713fa0aa', N'Sms.RegisterOtpTemplateId', N'', N'SMS', N'int', N'کلید سازگاری OTP؛ همگام با Sms.OtpTemplateId (CODE, EXPIRE)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('3102dddd-caa9-4fc0-bc89-b758822591fb', N'Sms.OtpExpiryMinutes', N'3', N'SMS', N'int', N'مدت اعتبار کد یکبار‌مصرف (دقیقه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('35dcfc61-beb6-4a8e-aacb-bfe128d8d7d6', N'HeroBackgroundPath', N'', N'Logos', N'image', N'پس‌زمینه Hero', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('8bfd909a-2ac5-4d80-bb09-bff440ee85dd', N'AboutTitle', N'درباره ویتورایز', N'About', N'string', N'عنوان درباره ما', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0311e1ca-39a6-4135-8536-c17efe3e85aa', N'DiscordUrl', N'', N'Social', N'string', N'دیسکورد', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('4af0b182-29bf-4b70-bb8c-c2e44a10cc79', N'NewsletterPlaceholder', N'ایمیل خود را وارد کنید', N'Homepage', N'string', N'راهنمای ورودی خبرنامه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0d95faed-95db-4bae-a040-c66c28fd9eb9', N'SmtpFromName', N'ویتورایز', N'Email', N'string', N'نام فرستنده', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('37d5a968-abd7-4bdc-93a5-c7db7934bfbd', N'Sms.OrderStatusChangedTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('71c5497b-f522-469a-a72d-c8f45199543b', N'Sms.AllowRetryFailed', N'true', N'SMS', N'bool', N'اجازه بازتلاش امن پیامک ناموفق', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('425218b1-1cac-4102-b3f5-ca54425df8d1', N'Sms.OtpTemplateId', N'', N'SMS', N'int', N'شناسه قالب کد یکبار مصرف', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('d4e7320c-f705-4b60-9972-caef4e0dde0f', N'HeaderLogoPath', N'', N'Logos', N'image', N'لوگوی هدر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('9ac077c2-ef70-433d-a307-cc24cbedc8c2', N'VatCalculationMode', N'BeforeDiscount', N'Tax', N'vatmode', N'نحوه محاسبه مالیات بر ارزش افزوده', CONVERT(datetime2, '2026-08-18T08:27:36.2068292', 126)),
  ('efb60c5e-bde2-4450-887b-cfcd7b578415', N'Sms.SenderName', N'ویتورایز', N'SMS', N'string', N'نام فرستنده', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('000e2a57-0366-4dfc-82bc-d0cc247b305e', N'CustomHeadHtml', N'', N'Scripts', N'string', N'کد سفارشی <head>', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0e17593c-d3b0-457c-bb9b-d47d50dd13d6', N'Error401Text', N'برای مشاهده این صفحه ابتدا وارد حساب کاربری شوید.', N'Errors', N'string', N'متن ۴۰۱', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('4e2c8bda-b4d8-4cc6-87be-d6d9ed4d18e4', N'Sms.Provider', N'SMS.ir', N'SMS', N'string', N'ارائه‌دهنده پیامک', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('ff4a7b53-ad19-4e10-ba34-d6dffb7f1494', N'TrustSeal.Ecunion.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('ae4844f3-75fe-4837-ab68-d9816ab64eae', N'LogoPath', N'', N'Logos', N'image', N'لوگوی اصلی (تم روشن)', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('a7a2ab5d-6777-4443-8072-da118150dd61', N'TrustSeal.Enamad.NewTab', N'true', N'TrustSeals', N'bool', N'باز شدن در زبانه جدید', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('a8c2ae65-2eec-4165-ab5e-dac1dd33e340', N'CustomFooterHtml', N'', N'Scripts', N'string', N'کد سفارشی انتهای صفحه', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ca0b2248-159d-4d56-b917-db829bd581fd', N'TrustSeal.Ecunion.Enabled', N'false', N'TrustSeals', N'bool', N'نمایش ecunion', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('3b5aa62f-39e3-40df-bf76-dc1791781f62', N'Error503Text', N'سایت موقتاً در دسترس نیست. به‌زودی برمی‌گردیم.', N'Errors', N'string', N'متن ۵۰۳', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('ff8e2a40-b513-4005-8324-de76a5954cb6', N'TrustSeal.Ecunion.Url', N'', N'TrustSeals', N'string', N'نشانی HTTPS رسمی ecunion.ir', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('86fb51b6-495a-4a20-93e5-e0c284920833', N'TrustSeal.Ecunion.ImagePath', N'', N'TrustSeals', N'image', N'تصویر مجوز', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('0671021b-4915-4093-a013-e11d4280022f', N'GoogleAnalyticsId', N'', N'SEO', N'string', N'شناسه Google Analytics', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('0d2a0dc2-5799-445e-9e3d-e22f776c834b', N'Sms.RetryDelaySeconds', N'30', N'SMS', N'int', N'پایه تأخیر بازتلاش (ثانیه)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('87a325e1-e2d5-4ba7-ac60-e525e8785c16', N'MaintenanceMode', N'false', N'General', N'bool', N'حالت تعمیر و نگهداری', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('466d1733-af5c-4356-9ecd-e5c3a8560e0c', N'Sms.MaskMobileInAdmin', N'true', N'SMS', N'bool', N'پنهان‌سازی شماره موبایل در تاریخچه مدیر', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('fe8cb7a3-7263-4e9c-a23b-e69269f589a2', N'TrustSeal.Enamad.Alt', N'نماد اعتماد الکترونیکی', N'TrustSeals', N'string', N'متن جایگزین', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('bb48d48b-f95d-4909-9f40-ea5bcfc56ea6', N'Sms.UseOutbox', N'true', N'SMS', N'bool', N'ارسال پیامک رویدادهای تجاری از طریق Outbox', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('115b0678-39b6-4c26-8e83-ead178b5fb94', N'Sms.MaxCustomRecipients', N'1', N'SMS', N'int', N'حداکثر گیرنده در هر ارسال سفارشی', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('3b925e7f-434f-41ca-859c-ec7ced9b98f5', N'Sms.RequireConfirmation', N'true', N'SMS', N'bool', N'نیاز به تایید نهایی پیش از ارسال سفارشی', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('e087b966-d5a4-4a09-9e65-ecdf721c3f10', N'Sms.WalletTopUpSuccessTemplateId', N'', N'SMS', N'int', N'کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('11a5fe14-971b-486a-a343-ece3fb557263', N'VatEnabled', N'false', N'Tax', N'bool', N'فعال بودن مالیات بر ارزش افزوده', CONVERT(datetime2, '2026-08-18T08:27:36.2068292', 126)),
  ('0a9714ce-2207-4523-ad6f-ee0ab6411a79', N'Error404IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۴۰۴', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('289cbc35-6192-46c9-b8a3-f1f340a9ccbf', N'Sms.OtpMaxAttempts', N'5', N'SMS', N'int', N'حداکثر تلاش مجاز برای هر کد', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('3b41a463-fec3-424a-a8e0-f2f7fa99090b', N'Sms.CustomSendEnabled', N'false', N'SMS', N'bool', N'فعال‌سازی ارسال پیامک سفارشی توسط مدیر', CONVERT(datetime2, '2026-08-18T08:27:31.9722233', 126)),
  ('b28227d9-c7d3-4124-8c0d-f77984493e4b', N'SmtpEnableSsl', N'true', N'Email', N'bool', N'استفاده از SSL', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('9a9a4aaf-49af-4966-8be5-f9de959e564b', N'AppleTouchIconPath', N'', N'Logos', N'image', N'آیکون Apple Touch', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('5d932fad-81b6-4f76-9228-fb7afd18ab7e', N'MaintenanceMessage', N'به‌زودی با نسخه‌ای بهتر برمی‌گردیم. از صبوری شما سپاسگزاریم.', N'General', N'string', N'پیام صفحه حالت تعمیر', CONVERT(datetime2, '2026-08-18T08:27:31.6104693', 126)),
  ('8aa88c32-5e7c-4c8c-a9b7-fd905fc70dcc', N'TrustSeal.Samandehi.ImagePath', N'', N'TrustSeals', N'image', N'تصویر نشان', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126)),
  ('ee897f06-39d8-474c-a4f7-fdc981c51273', N'TrustSeal.Samandehi.SortOrder', N'30', N'TrustSeals', N'int', N'ترتیب نمایش', CONVERT(datetime2, '2026-08-18T08:27:32.3131451', 126));
GO

PRINT N'Seeding dbo.Pages (4 row(s))...';
GO
INSERT INTO dbo.[Pages] ([Id], [Title], [Slug], [ContentHtml], [SeoTitle], [SeoDescription], [IsPublished], [CreatedAt], [UpdatedAt], [FocusKeyword], [IsSystem]) VALUES
  ('c88bd77f-ac94-4d1d-aecb-54456ff9b65b', N'قوانین و مقررات', N'terms', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'قوانین و مقررات', N'قوانین و مقررات استفاده از فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-18T08:27:36.5986120', 126), NULL, NULL, 1),
  ('e36bf30d-f2e1-49db-92c8-81b9de909d89', N'تماس با ما', N'contact', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'تماس با ما', N'راه‌های ارتباط با پشتیبانی ویتورایز', 0, CONVERT(datetime2, '2026-08-18T08:27:36.5986120', 126), NULL, NULL, 1),
  ('40461ded-16eb-4543-8725-a307cd3becd2', N'درباره ما', N'about', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'درباره ما', N'معرفی فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-18T08:27:36.5986120', 126), NULL, NULL, 1),
  ('58db523d-312e-4bff-a530-ce4bd069dc4b', N'حریم خصوصی', N'privacy', N'<p>محتوای این صفحه هنوز تکمیل نشده است.</p>', N'حریم خصوصی', N'سیاست حریم خصوصی فروشگاه ویتورایز', 0, CONVERT(datetime2, '2026-08-18T08:27:36.5986120', 126), NULL, NULL, 1);
GO

PRINT N'Seeding dbo.FontAssets (1 row(s))...';
GO
INSERT INTO dbo.[FontAssets] ([Id], [FamilyName], [FilePath], [FileFormat], [MimeType], [SizeBytes], [IsBuiltIn], [IsActive], [Scope], [CreatedByUserId], [CreatedAt], [UpdatedAt]) VALUES
  ('85099289-0f39-427a-a644-ee3592a3e6ac', N'Vazirmatn', NULL, N'woff2', N'font/woff2', 0, 1, 1, 3, NULL, CONVERT(datetime2, '2026-08-18T08:27:29.6285754', 126), NULL);
GO

PRINT N'Seeding dbo.KycPolicies (1 row(s))...';
GO
INSERT INTO dbo.[KycPolicies] ([Id], [Code], [Name], [IsActive], [CreatedAt], [UpdatedAt]) VALUES
  ('7b232fc1-cb6a-492d-84fa-932ea45808c3', N'legacy-profile-verification', N'احراز هویت پروفایل (سیاست انتقالی)', 1, CONVERT(datetime2, '2026-08-18T08:27:33.6729552', 126), NULL);
GO

PRINT N'Seeding dbo.KycPolicyVersions (1 row(s))...';
GO
INSERT INTO dbo.[KycPolicyVersions] ([Id], [KycPolicyId], [Version], [Status], [CustomerTitle], [CustomerInstructions], [CreatedAt], [PublishedAt], [CustomerActionDeadlineHours]) VALUES
  ('d5220ecd-8d05-40c8-b770-49a98ee7bc8e', '7b232fc1-cb6a-492d-84fa-932ea45808c3', 1, 2, N'احراز هویت لازم است', N'برای ادامه خرید، تأیید شماره همراه و احراز هویت حساب خود را تکمیل کنید.', CONVERT(datetime2, '2026-08-18T08:27:33.6738691', 126), CONVERT(datetime2, '2026-08-18T08:27:33.6738691', 126), NULL);
GO


/*----------------------------------------------------------------------------
  Migration ledger: records every script represented by this bootstrap so a
  future versioned deployment applies only newer scripts. Checksums are the
  real canonical values from the deployment manifest.
----------------------------------------------------------------------------*/
PRINT N'Recording deployment history (26 script(s))...';
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
  (N'V0021__default_variants_for_managed_products.sql', N'V0021', N'e0640d9ca8c6292bd69d7b53672f6b9cb20f1c832dee1c755d1239ffdb2cd587', SYSUTCDATETIME(), SUSER_SNAME(), N'Production', 1, N'Canonical deployment chain');
GO

PRINT N'Vitorize fresh install completed successfully.';
PRINT N'Next: deploy the API and Web packages, then configure the first administrator.';
GO

SET NOEXEC OFF;
GO