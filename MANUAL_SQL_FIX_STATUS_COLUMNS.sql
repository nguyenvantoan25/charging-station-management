IF COL_LENGTH('dbo.ChargingStations', 'system_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChargingStations]
    ADD [system_status] NVARCHAR(30) NOT NULL
        CONSTRAINT [DF_ChargingStations_system_status] DEFAULT (N'approved');
END;

IF COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChargingStations]
    ADD [operation_status] NVARCHAR(40) NOT NULL
        CONSTRAINT [DF_ChargingStations_operation_status] DEFAULT (N'active');
END;

IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'system_status') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'Status') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChargingStations]
    SET [system_status] = CASE
            WHEN [system_status] IS NULL OR LTRIM(RTRIM([system_status])) = N'' THEN N'approved'
            ELSE [system_status]
        END,
        [operation_status] = CASE
            WHEN [operation_status] IS NOT NULL AND LTRIM(RTRIM([operation_status])) <> N'' THEN [operation_status]
            WHEN [Status] IN (N'Hoạt động', N'active', N'Active') THEN N'active'
            WHEN [Status] IN (N'Bảo trì', N'Đang bảo trì', N'maintenance', N'Maintenance') THEN N'maintenance'
            WHEN [Status] IN (N'Không hoạt động', N'inactive', N'Inactive') THEN N'inactive'
            WHEN [Status] IN (N'Lỗi', N'Lỗi kỹ thuật', N'error', N'technical_error') THEN N'technical_error'
            ELSE N'active'
        END
    WHERE [system_status] IS NULL
       OR LTRIM(RTRIM([system_status])) = N''
       OR [operation_status] IS NULL
       OR LTRIM(RTRIM([operation_status])) = N'';
END;

IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'system_status') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_status') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingStations', 'is_visible') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_ChargingStations_PublicBusinessStatus'
          AND object_id = OBJECT_ID(N'[dbo].[ChargingStations]')
   )
BEGIN
    CREATE INDEX [IX_ChargingStations_PublicBusinessStatus]
        ON [dbo].[ChargingStations]([system_status], [operation_status], [maintenance_fee_status], [is_visible]);
END;
