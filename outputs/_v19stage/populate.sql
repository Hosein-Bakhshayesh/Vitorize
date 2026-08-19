SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @U uniqueidentifier = NEWID();
INSERT dbo.Users (Id, Mobile, PasswordHash, FullName, Status, IsMobileConfirmed, CreatedAt)
VALUES (@U, N'09125550001', N'x', N'Upgrade Customer', 1, 1, SYSUTCDATETIME());
DECLARE @Cat uniqueidentifier = NEWID();
INSERT dbo.Categories (Id, Title, Slug, IsActive, CreatedAt) VALUES (@Cat, N'UpCat', N'upcat', 1, SYSUTCDATETIME());
DECLARE @PI uniqueidentifier = NEWID(), @PM uniqueidentifier = NEWID(), @PS uniqueidentifier = NEWID(), @PV uniqueidentifier = NEWID();
INSERT dbo.Products (Id, CategoryId, Title, Slug, DeliveryType, BasePrice, CurrencyType, MinOrderQuantity, IsActive, CreatedAt)
VALUES (@PI,@Cat,N'Up Instant',   N'up-instant',  1,1000,2,1,1,SYSUTCDATETIME()),
       (@PM,@Cat,N'Up Manual',    N'up-manual',   2,1000,2,1,1,SYSUTCDATETIME()),
       (@PS,@Cat,N'Up Support',   N'up-support',  3,1000,2,1,1,SYSUTCDATETIME()),
       (@PV,@Cat,N'Up WithVars',  N'up-withvars', 2,1000,2,1,1,SYSUTCDATETIME());
-- only the last product has real variants; the first three are the broken "variantless" shape
INSERT dbo.ProductVariants (Id, ProductId, Title, Price, StockMode, IsDefault, IsActive, SortOrder, CreatedAt)
VALUES (NEWID(),@PV,N'Real A',1000,3,1,1,0,SYSUTCDATETIME()),
       (NEWID(),@PV,N'Real B',1200,3,0,1,1,SYSUTCDATETIME());
INSERT dbo.GiftCodes (Id, ProductId, EncryptedCode, MaskedCode, Status, CreatedAt)
VALUES (NEWID(),@PI,N'enc-a',N'***a',1,SYSUTCDATETIME()),
       (NEWID(),@PI,N'enc-b',N'***b',1,SYSUTCDATETIME());
DECLARE @O uniqueidentifier = NEWID();
INSERT dbo.Orders (Id, UserId, OrderNumber, Status, PaymentStatus, SubtotalAmount, FinalAmount, CurrencyType, CreatedAt)
VALUES (@O,@U,N'VT-UP-1',2,2,1000,1000,2,SYSUTCDATETIME());
-- historical order item with NULL ProductVariantId: must survive V0021 untouched
INSERT dbo.OrderItems (Id, OrderId, ProductId, ProductVariantId, ProductTitle, Quantity, UnitPrice, TotalPrice, DeliveryType, DeliveryStatus, CreatedAt)
VALUES (NEWID(),@O,@PM,NULL,N'Up Manual',2,1000,2000,2,1,SYSUTCDATETIME());
SELECT 'seeded';
