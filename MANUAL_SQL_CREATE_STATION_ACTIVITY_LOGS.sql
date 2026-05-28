IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[station_activity_logs]
    (
        [id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_station_activity_logs] PRIMARY KEY,
        [station_id] INT NOT NULL,
        [user_id] INT NULL,
        [action_type] NVARCHAR(80) NOT NULL,
        [old_value] NVARCHAR(1000) NULL,
        [new_value] NVARCHAR(1000) NULL,
        [description] NVARCHAR(1000) NULL,
        [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_station_activity_logs_created_at] DEFAULT (GETDATE())
    );
END
GO

IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_station_activity_logs_station_id_created_at'
          AND object_id = OBJECT_ID(N'[dbo].[station_activity_logs]')
   )
BEGIN
    CREATE INDEX [IX_station_activity_logs_station_id_created_at]
    ON [dbo].[station_activity_logs]([station_id], [created_at]);
END
GO

IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_station_activity_logs_user_id_action_type_created_at'
          AND object_id = OBJECT_ID(N'[dbo].[station_activity_logs]')
   )
BEGIN
    CREATE INDEX [IX_station_activity_logs_user_id_action_type_created_at]
    ON [dbo].[station_activity_logs]([user_id], [action_type], [created_at]);
END
GO
