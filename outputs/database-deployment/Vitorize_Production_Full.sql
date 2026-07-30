/* SQLCMD mode is required. Canonical database name: VitorizeDb. */
:setvar DatabaseName "VitorizeDb"
:setvar EnvironmentName "Production"
:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
/* 00 Safety checks / 01 database creation. Never recreates an existing database. */
USE [master];
GO
IF N'$(DatabaseName)' IN (N'master',N'model',N'msdb',N'tempdb') OR N'$(DatabaseName)' LIKE N'%[^0-9A-Za-z_-]%'
    THROW 53000, 'DatabaseName is invalid or a system database.', 1;
IF CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 16
    THROW 53001, 'Vitorize requires SQL Server 2022 (major version 16) or later.', 1;
IF DB_ID(N'$(DatabaseName)') IS NOT NULL
    THROW 53002, 'Refusing to recreate or modify an existing database. Use the versioned deployment runner for upgrades.', 1;
DECLARE @create nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(N'$(DatabaseName)')+N';';
EXEC(@create);
GO
ALTER DATABASE [$(DatabaseName)] SET COMPATIBILITY_LEVEL = 160;
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON;
GO
USE [$(DatabaseName)];
GO
/* 03-10 Exact schema generated from the current DACPAC + required manifest chain. */
:r .\Vitorize_Production_Schema.sql
GO
/* 11-15 Required non-secret reference data and deployment ledger. */
:r .\Vitorize_Production_Seed.sql
GO
/* 16 Post-deployment verification. */
:r .\Vitorize_Production_Verification.sql
GO
PRINT N'Vitorize production database deployment completed. Configure secrets outside SQL, then use the one-time BootstrapAdmin environment variables.';
