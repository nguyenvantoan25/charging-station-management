-- Cập nhật nghiệp vụ thực tế: admin chỉ duyệt đăng ký trạm mới,
-- chủ trạm tự quản lý vận hành/trụ, hệ thống quản lý phí duy trì.

IF COL_LENGTH('dbo.ChargingStations', 'system_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChargingStations]
    ADD [system_status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChargingStations_system_status] DEFAULT (N'approved');
END

IF COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChargingStations]
    ADD [operation_status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_ChargingStations_operation_status] DEFAULT (N'active');
END

IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChargingStations]
    ADD [maintenance_fee_status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_ChargingStations_maintenance_fee_status] DEFAULT (N'paid');
END

UPDATE [dbo].[ChargingStations]
SET [system_status] = N'approved'
WHERE [system_status] IS NULL OR LTRIM(RTRIM([system_status])) = N'';

UPDATE [dbo].[ChargingStations]
SET [operation_status] = CASE
        WHEN [Status] = N'Bảo trì' THEN N'maintenance'
        WHEN [Status] = N'Không hoạt động' THEN N'inactive'
        WHEN [Status] = N'Lỗi' OR [Status] = N'Lỗi kỹ thuật' THEN N'technical_error'
        WHEN [Status] = N'Quá tải' THEN N'overloaded'
        ELSE N'active'
    END
WHERE [operation_status] IS NULL OR LTRIM(RTRIM([operation_status])) = N'';

UPDATE [dbo].[ChargingStations]
SET [maintenance_fee_status] = N'paid'
WHERE [maintenance_fee_status] IS NULL OR LTRIM(RTRIM([maintenance_fee_status])) = N'';

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
        [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_station_activity_logs_created_at] DEFAULT (GETDATE()),
        CONSTRAINT [FK_station_activity_logs_ChargingStations_station_id]
            FOREIGN KEY ([station_id]) REFERENCES [dbo].[ChargingStations]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_station_activity_logs_AppUsers_user_id]
            FOREIGN KEY ([user_id]) REFERENCES [dbo].[AppUsers]([Id]) ON DELETE SET NULL
    );
END

IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_ChargingStations_PublicBusinessStatus'
          AND object_id = OBJECT_ID(N'[dbo].[ChargingStations]')
   )
BEGIN
    CREATE INDEX [IX_ChargingStations_PublicBusinessStatus]
    ON [dbo].[ChargingStations]([system_status], [operation_status], [maintenance_fee_status], [is_visible]);
END

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
