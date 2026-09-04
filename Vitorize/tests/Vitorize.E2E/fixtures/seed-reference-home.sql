/*
   Customer-facing reference-home fixture.
   Idempotent and restricted to the disposable Vitorize_Phase3_Verification E2E database.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @CategoryData TABLE (Id uniqueidentifier, Title nvarchar(150), Slug nvarchar(180), Icon nvarchar(100), SortOrder int);
INSERT @CategoryData VALUES
 ('52000000-0000-0000-0000-000000000001', N'محصولات تلگرام', N'ref-telegram', N'send', 10),
 ('52000000-0000-0000-0000-000000000002', N'هوش مصنوعی', N'ref-ai', N'sparkles', 20),
 ('52000000-0000-0000-0000-000000000003', N'پلی استیشن', N'ref-playstation', N'gamepad-2', 30),
 ('52000000-0000-0000-0000-000000000004', N'ایکس باکس', N'ref-xbox', N'joystick', 40),
 ('52000000-0000-0000-0000-000000000005', N'کارت‌های مجازی', N'ref-cards', N'credit-card', 50),
 ('52000000-0000-0000-0000-000000000006', N'شماره مجازی', N'ref-numbers', N'smartphone', 60);

INSERT dbo.Categories (Id, Title, Slug, Icon, SortOrder, IsActive, IsDeleted, CreatedAt)
SELECT Id, Title, Slug, Icon, SortOrder, 1, 0, @Now
FROM @CategoryData source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories target WHERE target.Id = source.Id);
UPDATE target SET Title=source.Title, Icon=source.Icon, SortOrder=source.SortOrder, IsActive=1, IsDeleted=0
FROM dbo.Categories target JOIN @CategoryData source ON source.Id=target.Id;

DECLARE @BrandData TABLE (Id uniqueidentifier, Title nvarchar(150), Slug nvarchar(180));
INSERT @BrandData VALUES
 ('52000000-0000-0000-0000-000000000021', N'Steam', N'ref-steam'),
 ('52000000-0000-0000-0000-000000000022', N'PlayStation', N'ref-playstation-brand'),
 ('52000000-0000-0000-0000-000000000023', N'Xbox', N'ref-xbox-brand');

INSERT dbo.Brands (Id, Title, Slug, IsActive, CreatedAt)
SELECT Id, Title, Slug, 1, @Now FROM @BrandData source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brands target WHERE target.Id=source.Id);
UPDATE target SET Title=source.Title, IsActive=1 FROM dbo.Brands target JOIN @BrandData source ON source.Id=target.Id;

DECLARE @ProductData TABLE (
 Id uniqueidentifier, CategoryId uniqueidentifier, BrandId uniqueidentifier, Title nvarchar(250), Slug nvarchar(300),
 Price decimal(18,2), Discount decimal(18,2) NULL, ImagePath nvarchar(1000), IsFeatured bit, SortOrder int);
INSERT @ProductData VALUES
 ('52000000-0000-0000-0000-000000000101','52000000-0000-0000-0000-000000000001','52000000-0000-0000-0000-000000000021',N'اشتراک تلگرام پریمیوم ۳ ماهه',N'ref-telegram-premium-3m',389000,349000,N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',1,10),
 ('52000000-0000-0000-0000-000000000102','52000000-0000-0000-0000-000000000002','52000000-0000-0000-0000-000000000021',N'اشتراک ChatGPT Plus یک‌ماهه',N'ref-chatgpt-plus-1m',790000,740000,N'/uploads/products/2df0873eb0af439eb4004bfb99b9ecb7.jpg',1,20),
 ('52000000-0000-0000-0000-000000000103','52000000-0000-0000-0000-000000000003','52000000-0000-0000-0000-000000000022',N'گیفت کارت پلی‌استیشن ۲۰ دلاری',N'ref-psn-20',1650000,1590000,N'/uploads/products/7831c6a9c7d0486ebfd5fb4a7f0bd28d.jpg',1,30),
 ('52000000-0000-0000-0000-000000000104','52000000-0000-0000-0000-000000000004','52000000-0000-0000-0000-000000000023',N'گیفت کارت ایکس‌باکس ۲۵ دلاری',N'ref-xbox-25',2050000,0,N'/uploads/products/d7583d037d164dfa9e8e5f9effc125d4.jpg',1,40),
 ('52000000-0000-0000-0000-000000000105','52000000-0000-0000-0000-000000000005','52000000-0000-0000-0000-000000000021',N'کارت مجازی بین‌المللی ۱۰ دلاری',N'ref-virtual-card-10',920000,870000,N'/uploads/products/3194fe500a6e44e69815f79e804db36c.jpg',1,50),
 ('52000000-0000-0000-0000-000000000106','52000000-0000-0000-0000-000000000006','52000000-0000-0000-0000-000000000021',N'شماره مجازی ترکیه',N'ref-virtual-number-tr',145000,0,N'/uploads/products/4677893cd4544805b54e9ef85ccf30da.jpg',1,60),
 ('52000000-0000-0000-0000-000000000107','52000000-0000-0000-0000-000000000002','52000000-0000-0000-0000-000000000021',N'اشتراک Claude Pro یک‌ماهه',N'ref-claude-pro-1m',890000,820000,N'/uploads/products/e243b894481d43ddab8362ab972fb39e.jpg',1,70),
 ('52000000-0000-0000-0000-000000000108','52000000-0000-0000-0000-000000000003','52000000-0000-0000-0000-000000000022',N'گیفت کارت پلی‌استیشن ۵۰ دلاری',N'ref-psn-50',3950000,3750000,N'/uploads/products/96cb62c95f9c45bf801d03e11b20a837.jpg',1,80),
 ('52000000-0000-0000-0000-000000000109','52000000-0000-0000-0000-000000000004','52000000-0000-0000-0000-000000000023',N'اشتراک Game Pass Ultimate',N'ref-gamepass-ultimate',970000,910000,N'/uploads/products/5048378b897b4c0ab39c1145ca3731c7.jpg',0,90),
 ('52000000-0000-0000-0000-000000000110','52000000-0000-0000-0000-000000000005','52000000-0000-0000-0000-000000000021',N'کارت مجازی اروپا ۲۰ دلاری',N'ref-virtual-card-20',1750000,0,N'/uploads/products/3ac53ea89a254016af9991ee71493a90.jpg',0,100);

INSERT dbo.Products (Id,CategoryId,BrandId,Title,Slug,ShortDescription,FullDescription,ProductType,DeliveryType,BasePrice,DiscountPrice,CurrencyType,MinOrderQuantity,IsFeatured,IsActive,IsDeleted,ThumbnailImagePath,ThumbnailAltText,SortOrder,CreatedAt)
SELECT Id,CategoryId,BrandId,Title,Slug,N'تحویل سریع و مطمئن از ویتورایز.',N'<p>محصول دیجیتال با تحویل سریع، پشتیبانی و تضمین اصالت.</p>',1,2,Price,NULLIF(Discount,0),2,1,IsFeatured,1,0,ImagePath,Title,SortOrder,@Now
FROM @ProductData source WHERE NOT EXISTS (SELECT 1 FROM dbo.Products target WHERE target.Id=source.Id);
UPDATE target SET Title=source.Title,BasePrice=source.Price,DiscountPrice=NULLIF(source.Discount,0),ThumbnailImagePath=source.ImagePath,ThumbnailAltText=source.Title,IsFeatured=source.IsFeatured,IsActive=1,IsDeleted=0,SortOrder=source.SortOrder
FROM dbo.Products target JOIN @ProductData source ON source.Id=target.Id;

INSERT dbo.ProductVariants (Id,ProductId,Title,Sku,Price,DiscountPrice,Value,StockMode,StockQuantity,IsDefault,IsActive,SortOrder,CreatedAt)
SELECT NEWID(),Id,N'تحویل استاندارد',N'REF-'+RIGHT(REPLACE(CONVERT(nvarchar(36),Id),'-',''),8),Price,NULLIF(Discount,0),N'standard',2,100,1,1,1,@Now
FROM @ProductData source WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductVariants target WHERE target.ProductId=source.Id);
UPDATE target SET StockMode=2,StockQuantity=100,IsDefault=1,IsActive=1
FROM dbo.ProductVariants target JOIN @ProductData source ON source.Id=target.ProductId;

DECLARE @BannerData TABLE (Id uniqueidentifier,Title nvarchar(250),ImagePath nvarchar(1000),Position nvarchar(100),SortOrder int,LinkUrl nvarchar(1000));
INSERT @BannerData VALUES
 ('52000000-0000-0000-0000-000000000201',N'دنیای دیجیتال با ویتورایز',N'/uploads/products/95f7a15fd1a443d7abf1ad2ff22efbd7.png',N'home-hero',10,N'/shop'),
 ('52000000-0000-0000-0000-000000000202',N'پیشنهادهای ویژهٔ ویتورایز',N'/uploads/products/2df0873eb0af439eb4004bfb99b9ecb7.jpg',N'home-secondary',10,N'/shop');
INSERT dbo.Banners (Id,Title,ImagePath,Position,SortOrder,LinkUrl,IsActive,CreatedAt,AltText)
SELECT Id,Title,ImagePath,Position,SortOrder,LinkUrl,1,@Now,Title FROM @BannerData source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Banners target WHERE target.Id=source.Id);
UPDATE target SET Title=source.Title,ImagePath=source.ImagePath,Position=source.Position,SortOrder=source.SortOrder,LinkUrl=source.LinkUrl,IsActive=1,AltText=source.Title
FROM dbo.Banners target JOIN @BannerData source ON source.Id=target.Id;

DECLARE @BlogData TABLE (Id uniqueidentifier,Title nvarchar(250),Slug nvarchar(250),ImagePath nvarchar(1000),SortOrder int);
INSERT @BlogData VALUES
 ('52000000-0000-0000-0000-000000000301',N'راهنمای انتخاب گیفت کارت مناسب',N'ref-gift-card-guide',N'/uploads/products/3194fe500a6e44e69815f79e804db36c.jpg',1),
 ('52000000-0000-0000-0000-000000000302',N'چطور اشتراک هوش مصنوعی بخریم؟',N'ref-ai-subscription-guide',N'/uploads/products/e243b894481d43ddab8362ab972fb39e.jpg',2),
 ('52000000-0000-0000-0000-000000000303',N'همه چیز دربارهٔ گیم پس',N'ref-game-pass-guide',N'/uploads/products/d7583d037d164dfa9e8e5f9effc125d4.jpg',3);
INSERT dbo.BlogPosts (Id,Title,Slug,Summary,ContentHtml,CoverImagePath,IsPublished,PublishedAt,CreatedAt,CoverImageAltText)
SELECT Id,Title,Slug,N'راهنمای کوتاه و کاربردی ویتورایز.',N'<p>محتوای آموزشی برای انتخاب و خرید بهتر محصولات دیجیتال.</p>',ImagePath,1,@Now,@Now,Title
FROM @BlogData source WHERE NOT EXISTS (SELECT 1 FROM dbo.BlogPosts target WHERE target.Id=source.Id);
UPDATE target SET Title=source.Title,IsPublished=1,CoverImagePath=source.ImagePath,PublishedAt=@Now FROM dbo.BlogPosts target JOIN @BlogData source ON source.Id=target.Id;

DECLARE @FaqData TABLE (Id uniqueidentifier,Question nvarchar(500),Answer nvarchar(max),SortOrder int);
INSERT @FaqData VALUES
 ('52000000-0000-0000-0000-000000000401',N'بعد از خرید چه زمانی محصول را دریافت می‌کنم؟',N'محصولات دیجیتال بلافاصله پس از تکمیل پرداخت در حساب کاربری شما نمایش داده می‌شوند.',10),
 ('52000000-0000-0000-0000-000000000402',N'آیا پرداخت در ویتورایز امن است؟',N'بله، پرداخت از طریق درگاه امن انجام می‌شود و اطلاعات سفارش در حساب شما ثبت خواهد شد.',20),
 ('52000000-0000-0000-0000-000000000403',N'اگر در دریافت سفارش مشکل داشتم چه کنم؟',N'از بخش پشتیبانی تیکت ثبت کنید تا تیم پشتیبانی سفارش شما را بررسی کند.',30),
 ('52000000-0000-0000-0000-000000000404',N'آیا امکان بازگشت وجه وجود دارد؟',N'شرایط بازگشت وجه بر اساس نوع محصول و وضعیت تحویل در قوانین سایت توضیح داده شده است.',40),
 ('52000000-0000-0000-0000-000000000405',N'چطور با پشتیبانی تماس بگیرم؟',N'از بخش تماس با ما، تیکت پشتیبانی یا اطلاعات تماس موجود در فوتر استفاده کنید.',50);
INSERT dbo.FAQs (Id,Question,Answer,SortOrder,IsActive,CreatedAt)
SELECT Id,Question,Answer,SortOrder,1,@Now FROM @FaqData source
WHERE NOT EXISTS (SELECT 1 FROM dbo.FAQs target WHERE target.Id=source.Id);
UPDATE target SET Question=source.Question,Answer=source.Answer,SortOrder=source.SortOrder,IsActive=1 FROM dbo.FAQs target JOIN @FaqData source ON source.Id=target.Id;

DECLARE @ReviewUserId uniqueidentifier='52000000-0000-0000-0000-000000000501';
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id=@ReviewUserId)
 INSERT dbo.Users (Id,FullName,Mobile,PasswordHash,Status,IsMobileConfirmed,CreatedAt) VALUES (@ReviewUserId,N'مشتری ویتورایز',N'09090000009',N'E2E-NOT-A-LOGIN-HASH',1,1,@Now);
DECLARE @ReviewData TABLE (Id uniqueidentifier,ProductId uniqueidentifier,Comment nvarchar(2000),Rating tinyint);
INSERT @ReviewData VALUES
 ('52000000-0000-0000-0000-000000000511','52000000-0000-0000-0000-000000000101',N'تحویل سفارش سریع بود و فرایند خرید بسیار راحت انجام شد.',5),
 ('52000000-0000-0000-0000-000000000512','52000000-0000-0000-0000-000000000102',N'پشتیبانی پاسخ‌گو بود و اشتراک بدون مشکل فعال شد.',5),
 ('52000000-0000-0000-0000-000000000513','52000000-0000-0000-0000-000000000103',N'کد گیفت کارت را سریع دریافت کردم. تجربهٔ خوبی بود.',5),
 ('52000000-0000-0000-0000-000000000514','52000000-0000-0000-0000-000000000104',N'قیمت مناسب و تحویل مطابق توضیحات محصول بود.',4);
INSERT dbo.ProductReviews (Id,ProductId,UserId,Comment,Rating,IsApproved,IsRejected,IsBuyer,CreatedAt)
SELECT Id,ProductId,@ReviewUserId,Comment,Rating,1,0,1,@Now FROM @ReviewData source
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductReviews target WHERE target.Id=source.Id);
UPDATE target SET Comment=source.Comment,Rating=source.Rating,IsApproved=1,IsRejected=0,IsBuyer=1 FROM dbo.ProductReviews target JOIN @ReviewData source ON source.Id=target.Id;

PRINT N'Reference home fixture: 10 products, categories, brands, banners, blog, FAQ and reviews are ready.';
