SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Disposable FIX-03 closure fixture. It uses the real cart schema and the existing
-- Testing-only customer; the database containing it is dropped after the suite.
DECLARE @UserId uniqueidentifier = '31000000-0000-0000-0000-000000000021';
DECLARE @CartId uniqueidentifier = '31000000-0000-0000-0000-000000000050';
DECLARE @ProductId uniqueidentifier = '31000000-0000-0000-0000-000000000002';
DECLARE @RelatedProductId uniqueidentifier = '31000000-0000-0000-0000-000000000011';
DECLARE @VariantId uniqueidentifier = '31000000-0000-0000-0000-000000000007';
DECLARE @FieldId uniqueidentifier = '31000000-0000-0000-0000-000000000009';

DELETE FROM dbo.CartItemInputValues WHERE CartItemId IN (SELECT Id FROM dbo.CartItems WHERE CartId = @CartId);
DELETE FROM dbo.CartItems WHERE CartId = @CartId;
DELETE FROM dbo.Carts WHERE Id = @CartId OR UserId = @UserId;

INSERT dbo.Carts (Id, UserId, GuestTokenHash, CreatedAt, LastActivityAt)
VALUES (@CartId, @UserId, NULL, SYSUTCDATETIME(), NULL);

DECLARE @MatchItem uniqueidentifier = '31000000-0000-0000-0000-000000000051';
DECLARE @UserXItem uniqueidentifier = '31000000-0000-0000-0000-000000000052';
DECLARE @RelatedItem uniqueidentifier = '31000000-0000-0000-0000-000000000053';
DECLARE @Match nvarchar(2000) = N'merge-match@example.test';
DECLARE @UserX nvarchar(2000) = N'merge-user-x@example.test';

INSERT dbo.CartItems (Id, CartId, ProductId, ProductVariantId, InputFingerprint, Quantity, UnitPrice, CurrencyType, CreatedAt)
VALUES
 (@MatchItem, @CartId, @ProductId, @VariantId, 'F7C42845F72920FAE933856565DC4F6EF63B5C78F43444DF835D3AB2BE1FC943', 2, 140000, 2, SYSUTCDATETIME()),
 (@UserXItem, @CartId, @ProductId, @VariantId, '2E76680385B7E3D3A9A76769A241EED1969D4BF682B9DFCA4EFC8352B6233E6E', 1, 140000, 2, SYSUTCDATETIME()),
 (@RelatedItem, @CartId, @RelatedProductId, NULL, 'NONE', 1, 50000, 2, SYSUTCDATETIME());

INSERT dbo.CartItemInputValues (Id, CartItemId, ProductInputFieldId, FieldKey, FieldLabel, FieldType, Value, IsSensitive, CreatedAt)
VALUES
 ('31000000-0000-0000-0000-000000000054', @MatchItem, @FieldId, 'account_email', N'Account Email', 2, @Match, 0, SYSUTCDATETIME()),
 ('31000000-0000-0000-0000-000000000055', @UserXItem, @FieldId, 'account_email', N'Account Email', 2, @UserX, 0, SYSUTCDATETIME());
