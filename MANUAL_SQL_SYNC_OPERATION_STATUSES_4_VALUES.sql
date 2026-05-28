-- Đồng bộ trạng thái vận hành trạm/trụ về đúng 4 trạng thái:
-- Đang hoạt động, Lỗi, Bảo trì, Không hoạt động

IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.ChargingStations', 'Status') IS NOT NULL
    BEGIN
        UPDATE [dbo].[ChargingStations]
        SET [Status] = CASE
            WHEN [Status] IN (N'Hoạt động', N'Đang hoạt động', N'active', N'Active') THEN N'Đang hoạt động'
            WHEN [Status] IN (N'Lỗi', N'Lỗi kỹ thuật', N'Quá tải', N'error', N'technical_error', N'overloaded') THEN N'Lỗi'
            WHEN [Status] IN (N'Bảo trì', N'Đang bảo trì', N'maintenance', N'Maintenance') THEN N'Bảo trì'
            WHEN [Status] IN (N'Không hoạt động', N'Tạm ngừng', N'Tạm ngừng hoạt động', N'inactive', N'Inactive') THEN N'Không hoạt động'
            ELSE N'Đang hoạt động'
        END;
    END

    IF COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NOT NULL
    BEGIN
        UPDATE [dbo].[ChargingStations]
        SET [operation_status] = CASE
            WHEN [operation_status] IN (N'active', N'Hoạt động', N'Đang hoạt động') THEN N'active'
            WHEN [operation_status] IN (N'error', N'technical_error', N'overloaded', N'Lỗi', N'Lỗi kỹ thuật', N'Quá tải') THEN N'error'
            WHEN [operation_status] IN (N'maintenance', N'Bảo trì', N'Đang bảo trì') THEN N'maintenance'
            WHEN [operation_status] IN (N'inactive', N'Không hoạt động', N'Tạm ngừng', N'Tạm ngừng hoạt động') THEN N'inactive'
            ELSE N'active'
        END;
    END
END
GO

IF OBJECT_ID(N'[dbo].[ChargingPoles]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.ChargingPoles', 'Status') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChargingPoles]
    SET [Status] = CASE
        WHEN [Status] IN (N'Hoạt động', N'Đang hoạt động', N'active', N'Active') THEN N'Đang hoạt động'
        WHEN [Status] IN (N'Lỗi', N'Lỗi kỹ thuật', N'Quá tải', N'error', N'technical_error', N'overloaded') THEN N'Lỗi'
        WHEN [Status] IN (N'Bảo trì', N'Đang bảo trì', N'maintenance', N'Maintenance') THEN N'Bảo trì'
        WHEN [Status] IN (N'Không hoạt động', N'Tạm ngừng', N'Tạm ngừng hoạt động', N'inactive', N'Inactive') THEN N'Không hoạt động'
        ELSE N'Đang hoạt động'
    END;
END
GO
