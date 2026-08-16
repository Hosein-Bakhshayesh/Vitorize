/*
  FIX-15 (Client Issue #15): admin group/broadcast in-app announcements.

  Delivery continues to use the existing one-row-per-user dbo.Notifications model; this migration
  only adds the broadcast header record and the link back to it. Existing notifications are
  untouched and keep BroadcastId NULL.

  The filtered unique index is the structural guarantee that one customer can never receive the
  same announcement twice, independently of request-level idempotency.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.NotificationBroadcasts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationBroadcasts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_NotificationBroadcasts_Id DEFAULT NEWSEQUENTIALID(),
        Title nvarchar(250) NOT NULL,
        Message nvarchar(max) NOT NULL,
        AudienceType tinyint NOT NULL,
        RecipientCount int NOT NULL CONSTRAINT DF_NotificationBroadcasts_RecipientCount DEFAULT (0),
        Status tinyint NOT NULL CONSTRAINT DF_NotificationBroadcasts_Status DEFAULT (1),
        ActionUrl nvarchar(500) NULL,
        CreatedByUserId uniqueidentifier NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_NotificationBroadcasts_CreatedAt DEFAULT SYSUTCDATETIME(),
        SentAt datetime2(7) NULL,
        CONSTRAINT PK_NotificationBroadcasts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_NotificationBroadcasts_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_NotificationBroadcasts_AudienceType CHECK (AudienceType IN (1, 2)),
        CONSTRAINT CK_NotificationBroadcasts_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_NotificationBroadcasts_RecipientCount CHECK (RecipientCount >= 0)
    );
    CREATE INDEX IX_NotificationBroadcasts_CreatedAt ON dbo.NotificationBroadcasts(CreatedAt DESC);
END;

IF COL_LENGTH(N'dbo.Notifications', N'BroadcastId') IS NULL
    ALTER TABLE dbo.Notifications ADD BroadcastId uniqueidentifier NULL;

-- SQL Server binds names for the whole batch before executing ALTER TABLE. Start a new batch so
-- the constraint and indexes below can reference the column added above.
GO

-- NO ACTION: deleting a broadcast header must never erase notifications already delivered to
-- customers. Sent broadcasts are immutable and are not offered for deletion.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Notifications_NotificationBroadcasts')
    ALTER TABLE dbo.Notifications ADD CONSTRAINT FK_Notifications_NotificationBroadcasts
        FOREIGN KEY (BroadcastId) REFERENCES dbo.NotificationBroadcasts(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Notifications') AND name = N'IX_Notifications_BroadcastId')
    CREATE INDEX IX_Notifications_BroadcastId ON dbo.Notifications(BroadcastId) WHERE BroadcastId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Notifications') AND name = N'UX_Notifications_Broadcast_User')
    CREATE UNIQUE INDEX UX_Notifications_Broadcast_User ON dbo.Notifications(BroadcastId, UserId) WHERE BroadcastId IS NOT NULL;

COMMIT TRANSACTION;
