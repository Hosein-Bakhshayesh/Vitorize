/*
  Vitorize production maintenance: transaction log full (LOG_BACKUP)

  Symptom (API log 2026-09-13, from 19:14 Tehran time): every database WRITE fails with
    "The transaction log for database 'VitorizeDb' is full due to 'LOG_BACKUP'."
  Reads still work, so pages open and product lists load, but login, refresh-token,
  register, checkout, audit logging and error logging all return HTTP 500.
  Admin login writes UserRefreshTokens / LastLoginAt / AuditLogs, so it cannot succeed.

  Cause: the database runs in FULL recovery model and no transaction-log backup has ever
  been taken (log_reuse_wait = LOG_BACKUP). SQL Server can therefore never reuse log
  space; the .ldf grew until it hit its maximum size or filled the disk.

  PERMISSIONS: the fix needs ALTER on the database (db_owner membership or sysadmin).
  The application login (AdminVitorize) does NOT have it. Connect with the SQL Server
  administrator login (sa, or the "database server" admin login shown in Plesk) and run
  this file; or ask the hosting provider to run the block "FOR THE SERVER ADMINISTRATOR"
  at the end of this file. Section 0 prints exactly which permissions the current login has.

  Run this file in SSMS after selecting the Vitorize production database.
  No USE statement, SQLCMD mode, external file, or parameter is required.

  Section 0 reports the current login's permissions.
  Section 1 prints the diagnosis (read-only; parts that need extra permissions are skipped).
  Section 2 applies the fix when @ApplyFix = 1 (default):
    - switches the database to SIMPLE recovery so log space is reused automatically,
    - CHECKPOINT so the log becomes reusable immediately (if the log is 100 % full it is
      grown once by 512 MB so the checkpoint has room),
    - shrinks the log file(s) to @ShrinkToMb,
    - sets a fixed 256 MB autogrowth so future growth is predictable.
  Point-in-time restore was never possible without log backups, so nothing is lost;
  full backups keep working exactly as before. To stay in FULL recovery instead, set
  @ApplyFix = 0 and use the alternative at the end of this file.

  No application restart is needed; the API reconnects per request.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @ApplyFix bit = 1;          -- 0 = diagnosis only
DECLARE @ShrinkToMb int = 1024;     -- target size of each log file after the fix
DECLARE @Db sysname = DB_NAME();
DECLARE @Sql nvarchar(max);

IF @Db IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51380, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
    THROW 51381, N'این دیتابیس Vitorize نیست (جدول DatabaseScriptHistory وجود ندارد).', 1;

/* ---------------------------------------------------------------- */
/* 0. Who am I and what may I do                                     */
/* ---------------------------------------------------------------- */
PRINT N'== 0. دسترسی‌های لاگین فعلی: ' + ISNULL(SUSER_SNAME(), N'?') + N' (کاربر دیتابیس: ' + ISNULL(USER_NAME(), N'?') + N') ==';

DECLARE @IsSysadmin bit = CASE WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1 ELSE 0 END;
DECLARE @IsDbOwner  bit = CASE WHEN IS_MEMBER('db_owner') = 1 THEN 1 ELSE 0 END;
DECLARE @CanAlterDb bit = CASE WHEN HAS_PERMS_BY_NAME(@Db, 'DATABASE', 'ALTER') = 1 THEN 1 ELSE 0 END;
DECLARE @CanViewDbState bit = CASE WHEN HAS_PERMS_BY_NAME(@Db, 'DATABASE', 'VIEW DATABASE STATE') = 1 THEN 1 ELSE 0 END;
DECLARE @CanViewServerState bit = CASE WHEN HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE') = 1 THEN 1 ELSE 0 END;
DECLARE @CanFix bit = CASE WHEN @IsSysadmin = 1 OR @IsDbOwner = 1 OR @CanAlterDb = 1 THEN 1 ELSE 0 END;

SELECT ISNULL(SUSER_SNAME(), N'?') AS login_name,
       USER_NAME()                 AS db_user,
       @IsSysadmin                 AS is_sysadmin,
       @IsDbOwner                  AS is_db_owner,
       @CanAlterDb                 AS can_alter_database,
       @CanViewDbState             AS can_view_database_state,
       @CanViewServerState         AS can_view_server_state,
       ISNULL(STUFF((SELECT N', ' + r.name
                     FROM sys.database_role_members AS m
                     JOIN sys.database_principals AS r ON r.principal_id = m.role_principal_id
                     WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID()
                     FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N''), N'(none)') AS db_roles,
       @CanFix                     AS can_apply_fix;

/* ---------------------------------------------------------------- */
/* 1. Diagnosis (read-only)                                          */
/* ---------------------------------------------------------------- */
PRINT N'== 1. وضعیت فعلی دیتابیس ' + @Db + N' ==';

SELECT d.name              AS database_name,
       d.recovery_model_desc,
       d.log_reuse_wait_desc,
       d.state_desc
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

-- sizes are int page counts; multiply as bigint (a 2 TB default max_size overflows int * 8).
SELECT f.name AS logical_name,
       f.type_desc,
       CAST(CAST(f.size AS bigint) * 8 / 1024.0 AS decimal(18, 1)) AS size_mb,
       CASE WHEN f.max_size = -1 THEN N'UNLIMITED'
            ELSE CAST(CAST(CAST(f.max_size AS bigint) * 8 / 1024.0 AS decimal(18, 1)) AS nvarchar(30)) END AS max_size_mb,
       CASE WHEN f.is_percent_growth = 1 THEN CAST(f.growth AS nvarchar(10)) + N'%'
            ELSE CAST(CAST(f.growth AS bigint) * 8 / 1024 AS nvarchar(20)) + N' MB' END AS growth,
       f.physical_name
FROM sys.database_files AS f;

IF @CanViewServerState = 1
BEGIN
    BEGIN TRY
        EXEC sp_executesql N'
            SELECT f.name AS logical_name,
                   CAST(vs.total_bytes / 1073741824.0 AS decimal(18, 1))     AS volume_total_gb,
                   CAST(vs.available_bytes / 1073741824.0 AS decimal(18, 1)) AS volume_free_gb,
                   vs.volume_mount_point
            FROM sys.database_files AS f
            CROSS APPLY sys.dm_os_volume_stats(DB_ID(), f.file_id) AS vs;';
    END TRY
    BEGIN CATCH
        PRINT N'(اطلاعات حجم دیسک در دسترس نیست: ' + ERROR_MESSAGE() + N')';
    END CATCH;
END
ELSE
    PRINT N'(فضای خالی دیسک فقط با مجوز VIEW SERVER STATE قابل نمایش است — رد شد)';

IF @CanViewDbState = 1
BEGIN
    BEGIN TRY
        EXEC sp_executesql N'
            SELECT CAST(total_log_size_in_bytes / 1048576.0 AS decimal(18, 1)) AS log_size_mb,
                   CAST(used_log_space_in_bytes / 1048576.0 AS decimal(18, 1))  AS log_used_mb,
                   CAST(used_log_space_in_percent AS decimal(5, 1))             AS log_used_percent
            FROM sys.dm_db_log_space_usage;';
    END TRY
    BEGIN CATCH
        PRINT N'(مصرف لاگ در دسترس نیست: ' + ERROR_MESSAGE() + N')';
    END CATCH;
END
ELSE
    PRINT N'(درصد مصرف لاگ فقط با مجوز VIEW DATABASE STATE قابل نمایش است — رد شد)';

BEGIN TRY
    EXEC sp_executesql N'
        SELECT CASE b.type WHEN ''D'' THEN N''Full'' WHEN ''I'' THEN N''Differential'' WHEN ''L'' THEN N''Log'' ELSE b.type END AS backup_type,
               MAX(b.backup_finish_date) AS last_backup
        FROM msdb.dbo.backupset AS b
        WHERE b.database_name = @Db
        GROUP BY b.type;', N'@Db sysname', @Db;
END TRY
BEGIN CATCH
    PRINT N'(تاریخچهٔ پشتیبان‌گیری در دسترس نیست: ' + ERROR_MESSAGE() + N')';
END CATCH;

IF @IsSysadmin = 1 OR @IsDbOwner = 1
BEGIN
    BEGIN TRY
        DBCC OPENTRAN WITH NO_INFOMSGS;
    END TRY
    BEGIN CATCH
        PRINT N'(DBCC OPENTRAN ممکن نشد: ' + ERROR_MESSAGE() + N')';
    END CATCH;
END
ELSE
    PRINT N'(DBCC OPENTRAN فقط برای db_owner/sysadmin — رد شد)';

/* ---------------------------------------------------------------- */
/* 2. Fix                                                            */
/* ---------------------------------------------------------------- */
IF @ApplyFix = 0
BEGIN
    PRINT N'@ApplyFix = 0 — فقط تشخیص چاپ شد؛ تغییری اعمال نشد.';
    RETURN;
END;

IF @CanFix = 0
BEGIN
    PRINT N'';
    PRINT N'*** لاگین فعلی (' + ISNULL(SUSER_SNAME(), N'?') + N') اجازهٔ ALTER DATABASE ندارد؛ هیچ تغییری اعمال نشد. ***';
    PRINT N'راه‌حل ۱: در SSMS با لاگین مدیر SQL Server (sa یا لاگین مدیریتی «Database Server» در Plesk) وصل شوید و همین فایل را دوباره اجرا کنید.';
    PRINT N'راه‌حل ۲: از میزبان بخواهید در دیتابیس ' + @Db + N' این دستور را اجرا کند و بعد شما این فایل را دوباره اجرا کنید:';
    PRINT N'          ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(USER_NAME()) + N';';
    PRINT N'راه‌حل ۳: میزبان خودش بلوک «FOR THE SERVER ADMINISTRATOR» در انتهای این فایل را اجرا کند.';
    THROW 51383, N'دسترسی کافی برای اصلاح وجود ندارد (ALTER DATABASE / db_owner). جزئیات در پیام‌های بالا.', 1;
END;

DECLARE @RecoveryModel nvarchar(60), @LogReuseWait nvarchar(60);
SELECT @RecoveryModel = d.recovery_model_desc, @LogReuseWait = d.log_reuse_wait_desc
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

IF @LogReuseWait NOT IN (N'NOTHING', N'LOG_BACKUP', N'CHECKPOINT')
BEGIN
    -- log_reuse_wait_desc is only refreshed at checkpoint and may still show the reason of a
    -- transaction that has already finished; refresh it once before deciding.
    BEGIN TRY
        CHECKPOINT;
    END TRY
    BEGIN CATCH
        PRINT N'(CHECKPOINT اولیه ممکن نشد: ' + ERROR_MESSAGE() + N')';
    END CATCH;
    SELECT @LogReuseWait = d.log_reuse_wait_desc FROM sys.databases AS d WHERE d.database_id = DB_ID();
END;

IF @LogReuseWait NOT IN (N'NOTHING', N'LOG_BACKUP', N'CHECKPOINT')
BEGIN
    DECLARE @Reason nvarchar(400) = N'علت پر بودن لاگ «' + @LogReuseWait
        + N'» است، نه LOG_BACKUP؛ تغییر recovery model آن را حل نمی‌کند. ابتدا این علت را بررسی کنید (تراکنش باز — خروجی DBCC OPENTRAN بالا — یا replication).';
    THROW 51382, @Reason, 1;
END;

PRINT N'== 2. اعمال اصلاح ==';

IF @RecoveryModel <> N'SIMPLE'
BEGIN
    BEGIN TRY
        SET @Sql = N'ALTER DATABASE ' + QUOTENAME(@Db) + N' SET RECOVERY SIMPLE WITH NO_WAIT;';
        EXEC (@Sql);
        PRINT N'recovery model از ' + @RecoveryModel + N' به SIMPLE تغییر کرد.';
    END TRY
    BEGIN CATCH
        DECLARE @AlterError nvarchar(2000) = N'تغییر recovery model ممکن نشد: ' + ERROR_MESSAGE();
        THROW 51384, @AlterError, 1;
    END CATCH;
END
ELSE
    PRINT N'recovery model از قبل SIMPLE بود.';

-- In SIMPLE recovery the inactive part of the log is truncated at checkpoint; the second
-- checkpoint releases the portion that was still active during the first one.
DECLARE @CheckpointOk bit = 0;
BEGIN TRY
    CHECKPOINT;
    CHECKPOINT;
    SET @CheckpointOk = 1;
END TRY
BEGIN CATCH
    PRINT N'CHECKPOINT ممکن نشد (' + ERROR_MESSAGE() + N') — لاگ صددرصد پر است؛ یک بار ۵۱۲ مگابایت بزرگ می‌شود تا جا باز شود.';
END CATCH;

IF @CheckpointOk = 0
BEGIN
    DECLARE @GrowName sysname, @GrowSizeMb int;
    SELECT TOP (1) @GrowName = f.name, @GrowSizeMb = CAST(CAST(f.size AS bigint) * 8 / 1024 AS int)
    FROM sys.database_files AS f
    WHERE f.type = 1
    ORDER BY f.size DESC;

    BEGIN TRY
        SET @Sql = N'ALTER DATABASE ' + QUOTENAME(@Db) + N' MODIFY FILE (NAME = ' + QUOTENAME(@GrowName, '''')
                 + N', SIZE = ' + CAST(@GrowSizeMb + 512 AS nvarchar(20)) + N'MB, MAXSIZE = UNLIMITED);';
        EXEC (@Sql);
        CHECKPOINT;
        CHECKPOINT;
        SET @CheckpointOk = 1;
        PRINT N'فایل لاگ ' + @GrowName + N' موقتاً به ' + CAST(@GrowSizeMb + 512 AS nvarchar(20)) + N' مگابایت بزرگ شد و CHECKPOINT انجام شد.';
    END TRY
    BEGIN CATCH
        DECLARE @GrowError nvarchar(2000) = N'لاگ کاملاً پر است و بزرگ‌کردن آن هم ممکن نشد (احتمالاً درایو فایل لاگ پر است): '
            + ERROR_MESSAGE() + N' — ابتدا روی آن درایو فضا آزاد کنید (لاگ‌های قدیمی، پشتیبان‌های قدیمی، فایل‌های موقت) و این فایل را دوباره اجرا کنید.';
        THROW 51385, @GrowError, 1;
    END CATCH;
END;

DECLARE @LogName sysname;
DECLARE log_files CURSOR LOCAL FAST_FORWARD FOR
    SELECT f.name FROM sys.database_files AS f WHERE f.type = 1;
OPEN log_files;
FETCH NEXT FROM log_files INTO @LogName;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @Sql = N'DBCC SHRINKFILE (' + QUOTENAME(@LogName, '''') + N', ' + CAST(@ShrinkToMb AS nvarchar(10)) + N') WITH NO_INFOMSGS;';
        EXEC (@Sql);
        SET @Sql = N'ALTER DATABASE ' + QUOTENAME(@Db) + N' MODIFY FILE (NAME = ' + QUOTENAME(@LogName, '''') + N', FILEGROWTH = 256MB, MAXSIZE = UNLIMITED);';
        EXEC (@Sql);
        PRINT N'فایل لاگ ' + @LogName + N' به حدود ' + CAST(@ShrinkToMb AS nvarchar(10)) + N' مگابایت کوچک شد و رشد آن روی 256MB ثابت تنظیم شد.';
    END TRY
    BEGIN CATCH
        PRINT N'کوچک‌کردن ' + @LogName + N' ممکن نشد (' + ERROR_MESSAGE() + N'); لاگ دیگر پر نیست و قابل استفاده است، فقط حجم فایل روی دیسک فعلاً بزرگ می‌ماند. بعداً دوباره اجرا کنید.';
    END CATCH;
    FETCH NEXT FROM log_files INTO @LogName;
END;
CLOSE log_files;
DEALLOCATE log_files;

PRINT N'== 3. وضعیت پس از اصلاح ==';
SELECT d.recovery_model_desc, d.log_reuse_wait_desc
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

SELECT f.name AS logical_name, f.type_desc,
       CAST(CAST(f.size AS bigint) * 8 / 1024.0 AS decimal(18, 1)) AS size_mb,
       CASE WHEN f.is_percent_growth = 1 THEN CAST(f.growth AS nvarchar(10)) + N'%'
            ELSE CAST(CAST(f.growth AS bigint) * 8 / 1024 AS nvarchar(20)) + N' MB' END AS growth
FROM sys.database_files AS f;

IF @CanViewDbState = 1
    EXEC sp_executesql N'
        SELECT CAST(total_log_size_in_bytes / 1048576.0 AS decimal(18, 1)) AS log_size_mb,
               CAST(used_log_space_in_bytes / 1048576.0 AS decimal(18, 1))  AS log_used_mb,
               CAST(used_log_space_in_percent AS decimal(5, 1))             AS log_used_percent
        FROM sys.dm_db_log_space_usage;';

PRINT N'انجام شد. ورود و ثبت سفارش بلافاصله و بدون ری‌استارت برنامه باید کار کند.';

/*
  ======================================================================================
  FOR THE SERVER ADMINISTRATOR (sa / hosting support) — run as sysadmin, any database:
  ======================================================================================

    -- Either give the application login the right to run this file itself:
    USE [VitorizeDb];
    ALTER ROLE db_owner ADD MEMBER [AdminVitorize];
    -- (optional, lets the diagnostics show disk space)  GRANT VIEW SERVER STATE TO [AdminVitorize];

    -- Or apply the fix directly:
    ALTER DATABASE [VitorizeDb] SET RECOVERY SIMPLE WITH NO_WAIT;
    USE [VitorizeDb];
    CHECKPOINT; CHECKPOINT;
    SELECT name FROM sys.database_files WHERE type = 1;          -- logical log file name, e.g. VitorizeDb_log
    DBCC SHRINKFILE (N'VitorizeDb_log', 1024);
    ALTER DATABASE [VitorizeDb] MODIFY FILE (NAME = N'VitorizeDb_log', FILEGROWTH = 256MB, MAXSIZE = UNLIMITED);

  Alternative — stay in FULL recovery (only if a regular log-backup job will exist):

    BACKUP LOG [VitorizeDb] TO DISK = N'D:\Backups\VitorizeDb_log.trn' WITH COMPRESSION, INIT;
    DBCC SHRINKFILE (N'VitorizeDb_log', 1024);
    -- then schedule BACKUP LOG every 15 minutes (SQL Agent, or Task Scheduler + sqlcmd on Express).
    -- Without that job the log fills up again.

  If the drive holding the .ldf is (nearly) full, free space there as well; yesterday's
  "connection refused" outage (2026-09-12 14:38) is consistent with SQL Server hitting a full disk.
*/
