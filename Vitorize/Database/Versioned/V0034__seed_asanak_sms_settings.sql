/*
  Adds empty, provider-specific Asanak connection settings.
  This is deliberately additive: it neither switches Sms.Provider nor alters
  the existing SMS.ir API key, templates, sender line, history, or outbox.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.Settings', N'U') IS NULL
    THROW 51034, N'dbo.Settings is required before V0034 can run.', 1;

BEGIN TRANSACTION;

DECLARE @Settings TABLE
(
    [Key] nvarchar(200) NOT NULL PRIMARY KEY,
    [Value] nvarchar(max) NULL,
    GroupName nvarchar(100) NOT NULL,
    ValueType nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL
);

INSERT @Settings ([Key], [Value], GroupName, ValueType, [Description]) VALUES
    (N'Sms.AsanakUsername', N'', N'SMS', N'secret', N'نام کاربری وب سرویس آسانک (محرمانه)'),
    (N'Sms.AsanakPassword', N'', N'SMS', N'secret', N'رمز وب سرویس آسانک (محرمانه)'),
    (N'Sms.AsanakSource', N'', N'SMS', N'secret', N'شماره مبدأ پیامک آسانک (محرمانه)');

INSERT dbo.Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), source.[Key], source.[Value], source.GroupName, source.ValueType, source.[Description], SYSUTCDATETIME()
FROM @Settings source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings target WHERE target.[Key] = source.[Key]);

COMMIT TRANSACTION;
