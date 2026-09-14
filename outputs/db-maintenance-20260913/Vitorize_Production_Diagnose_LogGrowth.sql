/*
  Vitorize log-growth diagnosis (READ-ONLY, changes nothing)

  Run in SSMS with the Vitorize production database selected, ideally with the SQL Server
  administrator login (sections that need VIEW SERVER STATE / msdb access are skipped with a
  note when the login lacks them). It answers "why does the log keep filling":

    0. permissions of the current login
    A. server edition / uptime
    B. log state now (recovery model, size, what blocks truncation)
    C. autogrowth history from the default trace: how many MB the data/log files grew per day
       -> this is the real daily write volume of the database
    D. largest tables
    E. tables with the most inserts/updates/deletes since SQL Server started
       -> which part of the application writes the most
    F. SQL Agent jobs / maintenance plans (a nightly INDEX REBUILD is fully logged and is the
       classic cause of a huge log)
    G. long-running open transactions (block truncation regardless of recovery model)
    H. backup history for the last 30 entries

  Interpretation:
    - FULL recovery + no "Log" rows in H  => the log could never be truncated; it fills no
      matter how little the application writes. That is configuration, not code.
    - C shows the growth rate. A low-traffic shop generates tens of MB per day; hundreds of MB or
      GB per day point to F (index maintenance) or to a bulk job.
    - E shows the writers. AuditLogs near the top is expected: every SaveChanges writes an
      audit row through AuditSaveChangesInterceptor.
*/
SET NOCOUNT ON;
DECLARE @Db sysname = DB_NAME();

IF @Db IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51390, N'ابتدا دیتابیس عملیاتی Vitorize را در SSMS انتخاب کنید؛ اجرای این فایل روی دیتابیس سیستمی مجاز نیست.', 1;

PRINT N'== 0. دسترسی‌های لاگین فعلی ==';
SELECT ISNULL(SUSER_SNAME(), N'?') AS login_name,
       USER_NAME()                 AS db_user,
       CASE WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1 ELSE 0 END               AS is_sysadmin,
       CASE WHEN IS_MEMBER('db_owner') = 1 THEN 1 ELSE 0 END                       AS is_db_owner,
       HAS_PERMS_BY_NAME(@Db, 'DATABASE', 'VIEW DATABASE STATE')                   AS can_view_database_state,
       HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE')                          AS can_view_server_state;

PRINT N'== A. سرور ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT SERVERPROPERTY(''Edition'')        AS edition,
               SERVERPROPERTY(''ProductVersion'') AS product_version,
               SERVERPROPERTY(''MachineName'')    AS machine,
               (SELECT sqlserver_start_time FROM sys.dm_os_sys_info) AS sql_server_started_at;';
END TRY
BEGIN CATCH
    SELECT SERVERPROPERTY('Edition') AS edition, SERVERPROPERTY('ProductVersion') AS product_version, SERVERPROPERTY('MachineName') AS machine;
    PRINT N'(زمان استارت SQL Server نیازمند VIEW SERVER STATE است: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== B. وضعیت لاگ ==';
SELECT d.recovery_model_desc, d.log_reuse_wait_desc, d.state_desc
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

SELECT f.name AS logical_name, f.type_desc,
       CAST(CAST(f.size AS bigint) * 8 / 1024.0 AS decimal(18, 1)) AS size_mb,
       CASE WHEN f.max_size = -1 THEN N'UNLIMITED'
            ELSE CAST(CAST(CAST(f.max_size AS bigint) * 8 / 1024.0 AS decimal(18, 1)) AS nvarchar(30)) END AS max_size_mb,
       CASE WHEN f.is_percent_growth = 1 THEN CAST(f.growth AS nvarchar(10)) + N'%'
            ELSE CAST(CAST(f.growth AS bigint) * 8 / 1024 AS nvarchar(20)) + N' MB' END AS growth,
       f.physical_name
FROM sys.database_files AS f;

BEGIN TRY
    EXEC sp_executesql N'
        SELECT CAST(total_log_size_mb AS decimal(18,1))            AS total_log_size_mb,
               CAST(active_log_size_mb AS decimal(18,1))           AS active_log_size_mb,
               CAST(log_since_last_checkpoint_mb AS decimal(18,1)) AS log_since_last_checkpoint_mb,
               CAST(log_since_last_log_backup_mb AS decimal(18,1)) AS log_since_last_log_backup_mb,
               total_vlf_count, active_vlf_count,
               log_truncation_holdup_reason, log_backup_time
        FROM sys.dm_db_log_stats(DB_ID());';
END TRY
BEGIN CATCH
    PRINT N'(sys.dm_db_log_stats در دسترس نیست: ' + ERROR_MESSAGE() + N')';
END CATCH;

BEGIN TRY
    EXEC sp_executesql N'
        SELECT CAST(total_log_size_in_bytes / 1048576.0 AS decimal(18, 1)) AS log_size_mb,
               CAST(used_log_space_in_bytes / 1048576.0 AS decimal(18, 1))  AS log_used_mb,
               CAST(used_log_space_in_percent AS decimal(5, 1))             AS log_used_percent
        FROM sys.dm_db_log_space_usage;';
END TRY
BEGIN CATCH
    PRINT N'(مصرف لاگ نیازمند VIEW DATABASE STATE است: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== C. رشد خودکار فایل‌ها به ازای هر روز (default trace؛ فقط چند روز اخیر را نگه می‌دارد) ==';
BEGIN TRY
    EXEC sp_executesql N'
        DECLARE @TracePath nvarchar(260);
        SELECT @TracePath = REVERSE(SUBSTRING(REVERSE(t.path), CHARINDEX(N''\'', REVERSE(t.path)), 260)) + N''log.trc''
        FROM sys.traces AS t
        WHERE t.is_default = 1;

        IF @TracePath IS NULL
            PRINT N''(default trace فعال نیست؛ تاریخچهٔ رشد در دسترس نیست)'';
        ELSE
            SELECT CAST(tr.StartTime AS date)                                      AS day,
                   CASE tr.EventClass WHEN 92 THEN N''DATA'' WHEN 93 THEN N''LOG'' END AS file_type,
                   COUNT(*)                                                        AS growth_events,
                   CAST(SUM(CAST(tr.IntegerData AS bigint)) * 8 / 1024.0 AS decimal(18, 1)) AS grown_mb,
                   MIN(tr.StartTime)                                               AS first_event,
                   MAX(tr.StartTime)                                               AS last_event
            FROM sys.fn_trace_gettable(@TracePath, DEFAULT) AS tr
            WHERE tr.EventClass IN (92, 93)
              AND tr.DatabaseName = @Db
            GROUP BY CAST(tr.StartTime AS date), tr.EventClass
            ORDER BY day DESC, file_type;', N'@Db sysname', @Db;
END TRY
BEGIN CATCH
    PRINT N'(خواندن default trace نیازمند ALTER TRACE / sysadmin است: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== D. بزرگ‌ترین جدول‌ها ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT TOP (20)
               s.name + N''.'' + t.name AS table_name,
               SUM(CASE WHEN i.index_id IN (0, 1) THEN p.row_count ELSE 0 END)      AS row_count,
               CAST(SUM(p.reserved_page_count) * 8 / 1024.0 AS decimal(18, 1))      AS reserved_mb,
               CAST(SUM(p.used_page_count) * 8 / 1024.0 AS decimal(18, 1))          AS used_mb
        FROM sys.dm_db_partition_stats AS p
        JOIN sys.indexes AS i ON i.object_id = p.object_id AND i.index_id = p.index_id
        JOIN sys.tables  AS t ON t.object_id = p.object_id
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        GROUP BY s.name, t.name
        ORDER BY reserved_mb DESC;';
END TRY
BEGIN CATCH
    PRINT N'(اندازهٔ جدول‌ها نیازمند VIEW DATABASE STATE است: ' + ERROR_MESSAGE() + N')';
    -- Fallback that every db user may run: row counts only.
    SELECT TOP (20) s.name + N'.' + t.name AS table_name, SUM(p.rows) AS row_count
    FROM sys.partitions AS p
    JOIN sys.tables  AS t ON t.object_id = p.object_id
    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    WHERE p.index_id IN (0, 1)
    GROUP BY s.name, t.name
    ORDER BY row_count DESC;
END CATCH;

PRINT N'== E. پرنویس‌ترین جدول‌ها از آخرین استارت SQL Server (سطر) ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT TOP (25)
               s.name + N''.'' + t.name AS table_name,
               SUM(os.leaf_insert_count) AS rows_inserted,
               SUM(os.leaf_update_count) AS rows_updated,
               SUM(os.leaf_delete_count + os.leaf_ghost_count) AS rows_deleted,
               SUM(os.leaf_insert_count + os.leaf_update_count + os.leaf_delete_count + os.leaf_ghost_count) AS total_row_writes
        FROM sys.dm_db_index_operational_stats(DB_ID(), NULL, NULL, NULL) AS os
        JOIN sys.tables  AS t ON t.object_id = os.object_id
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        WHERE os.index_id IN (0, 1)   -- heap or clustered index = one count per table row
        GROUP BY s.name, t.name
        ORDER BY total_row_writes DESC;';
END TRY
BEGIN CATCH
    PRINT N'(آمار نوشتن جدول‌ها نیازمند VIEW DATABASE STATE است: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== F. Jobهای SQL Agent و Maintenance Planها ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT j.name AS job_name, j.enabled, js.step_id, js.step_name, js.subsystem,
               LEFT(js.command, 400) AS command,
               CASE WHEN js.command LIKE N''%REBUILD%'' OR js.command LIKE N''%REORGANIZE%'' THEN N''INDEX MAINTENANCE - fully logged, fills the log''
                    WHEN js.command LIKE N''%BACKUP LOG%''      THEN N''LOG BACKUP''
                    WHEN js.command LIKE N''%BACKUP DATABASE%'' THEN N''FULL/DIFF BACKUP''
                    WHEN js.command LIKE N''%SHRINK%''          THEN N''SHRINK''
                    ELSE N'''' END AS note
        FROM msdb.dbo.sysjobs AS j
        JOIN msdb.dbo.sysjobsteps AS js ON js.job_id = j.job_id
        ORDER BY j.name, js.step_id;';
END TRY
BEGIN CATCH
    PRINT N'(فهرست Jobها در دسترس نیست — دسترسی msdb لازم است یا نسخهٔ Express بدون SQL Agent: ' + ERROR_MESSAGE() + N')';
END CATCH;

BEGIN TRY
    EXEC sp_executesql N'
        SELECT p.name AS maintenance_plan, p.create_date, sp.subplan_name
        FROM msdb.dbo.sysmaintplan_plans AS p
        LEFT JOIN msdb.dbo.sysmaintplan_subplans AS sp ON sp.plan_id = p.id;';
END TRY
BEGIN CATCH
    PRINT N'(Maintenance Plan تعریف نشده یا در دسترس نیست: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== G. تراکنش‌های بازِ طولانی (بیش از ۱ دقیقه) ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT st.session_id,
               at.name AS transaction_name,
               at.transaction_begin_time,
               DATEDIFF(SECOND, at.transaction_begin_time, SYSDATETIME()) AS open_seconds,
               es.program_name, es.host_name, es.login_name, es.status
        FROM sys.dm_tran_session_transactions AS st
        JOIN sys.dm_tran_active_transactions AS at ON at.transaction_id = st.transaction_id
        JOIN sys.dm_exec_sessions AS es ON es.session_id = st.session_id
        WHERE at.transaction_begin_time < DATEADD(MINUTE, -1, SYSDATETIME())
        ORDER BY at.transaction_begin_time;';
END TRY
BEGIN CATCH
    PRINT N'(تراکنش‌های باز نیازمند VIEW SERVER STATE است: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'== H. تاریخچهٔ پشتیبان‌گیری (۳۰ مورد اخیر) ==';
BEGIN TRY
    EXEC sp_executesql N'
        SELECT TOP (30)
               CASE b.type WHEN ''D'' THEN N''Full'' WHEN ''I'' THEN N''Differential'' WHEN ''L'' THEN N''Log'' ELSE b.type END AS backup_type,
               b.backup_start_date, b.backup_finish_date,
               CAST(b.backup_size / 1048576.0 AS decimal(18, 1)) AS size_mb,
               m.physical_device_name
        FROM msdb.dbo.backupset AS b
        LEFT JOIN msdb.dbo.backupmediafamily AS m ON m.media_set_id = b.media_set_id
        WHERE b.database_name = @Db
        ORDER BY b.backup_start_date DESC;', N'@Db sysname', @Db;
END TRY
BEGIN CATCH
    PRINT N'(تاریخچهٔ پشتیبان‌گیری در دسترس نیست: ' + ERROR_MESSAGE() + N')';
END CATCH;

PRINT N'پایان تشخیص؛ هیچ تغییری اعمال نشد.';
