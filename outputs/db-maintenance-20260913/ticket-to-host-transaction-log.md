موضوع: پر شدن Transaction Log دیتابیس VitorizeDb روی SQL Server (193.141.65.146، پورت 2019) و درخواست تغییر Recovery Model به SIMPLE

با سلام و احترام

از ساعت ۱۹:۱۴ امروز ۱۴۰۵/۰۶/۲۲ (2026-09-13) تمام عملیات نوشتن روی دیتابیس VitorizeDb با این خطا متوقف شده است:

    Msg 9002: The transaction log for database 'VitorizeDb' is full due to 'LOG_BACKUP'.

سایت بالاست و خواندن انجام می‌شود، اما ورود کاربران، ثبت سفارش و هر عملیات نوشتنی خطای 500 می‌دهد.

علت: دیتابیس در حالت FULL Recovery است و هیچ Transaction Log Backup برای آن گرفته نمی‌شود (log_reuse_wait_desc = LOG_BACKUP)، بنابراین فایل .ldf هرگز آزاد نمی‌شود و تا پر شدن رشد می‌کند. قطعی دیروز (2026-09-12 ساعت ۱۴:۳۸، رد اتصال به SQL Server) هم به احتمال زیاد از پر شدن فضای دیسک به همین دلیل بوده است.

لاگین ما (AdminVitorize) دسترسی ALTER DATABASE و db_owner ندارد و نمی‌توانیم خودمان اصلاح کنیم. خواهشمندم موارد زیر را با دسترسی sysadmin انجام دهید:

۱) تغییر Recovery Model به SIMPLE و آزادسازی لاگ:

    ALTER DATABASE [VitorizeDb] SET RECOVERY SIMPLE WITH NO_WAIT;
    USE [VitorizeDb];
    CHECKPOINT;
    CHECKPOINT;

۲) کوچک‌کردن فایل لاگ به ۱ گیگابایت و تنظیم رشد ثابت ۲۵۶ مگابایت (نام منطقی فایل لاگ را از دستور اول بگیرید؛ معمولاً VitorizeDb_log است):

    SELECT name, size * 8 / 1024 AS size_mb FROM sys.database_files WHERE type = 1;
    DBCC SHRINKFILE (N'VitorizeDb_log', 1024);
    ALTER DATABASE [VitorizeDb] MODIFY FILE (NAME = N'VitorizeDb_log', FILEGROWTH = 256MB, MAXSIZE = UNLIMITED);

   اگر لاگ صددرصد پر است و CHECKPOINT خودش خطای 9002 می‌دهد، لطفاً ابتدا فایل لاگ را ۵۱۲ مگابایت بزرگ کنید و بعد CHECKPOINT و SHRINK را اجرا کنید:

    ALTER DATABASE [VitorizeDb] MODIFY FILE (NAME = N'VitorizeDb_log', SIZE = <اندازه فعلی + 512>MB, MAXSIZE = UNLIMITED);

۳) بررسی فضای خالی درایوی که فایل‌های .mdf و .ldf روی آن هستند و آزاد کردن فضا در صورت نیاز.

۴) در صورت امکان، تا نیاز به تیکت مجدد نباشد، یکی از این دو دسترسی را به لاگین ما بدهید:

    USE [VitorizeDb];
    ALTER ROLE db_owner ADD MEMBER [AdminVitorize];
    GRANT VIEW SERVER STATE TO [AdminVitorize];   -- اختیاری، فقط برای مشاهدهٔ آمار

توضیح: تغییر به SIMPLE هیچ داده‌ای را از بین نمی‌برد و Full Backup مثل قبل کار می‌کند. Point-in-time restore هم تا امروز بدون Log Backup عملاً وجود نداشته است. اگر سیاست شما نگه‌داشتن FULL Recovery است، لطفاً به‌جای بند ۱ یک Job پشتیبان‌گیری لاگ با فاصلهٔ حداکثر ۱۵ دقیقه تنظیم کنید و بعد از اولین BACKUP LOG، بند ۲ را اجرا کنید؛ بدون این Job لاگ دوباره پر خواهد شد.

نیاز به ری‌استارت برنامه یا IIS نیست.

پس از انجام، لطفاً خروجی این دستور را در تیکت قرار دهید تا از اصلاح مطمئن شویم:

    SELECT name, recovery_model_desc, log_reuse_wait_desc FROM sys.databases WHERE name = 'VitorizeDb';
    USE [VitorizeDb]; SELECT name, type_desc, size * 8 / 1024 AS size_mb, growth * 8 / 1024 AS growth_mb FROM sys.database_files;

با تشکر
