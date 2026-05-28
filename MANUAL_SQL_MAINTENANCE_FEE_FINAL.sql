-- TramSac99 - bổ sung trường phí duy trì trạm sạc
-- Chạy file này nếu Program.cs chưa tự cập nhật được database.

IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_status') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_ChargingStations_maintenance_fee_status] DEFAULT (N'active');

IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_due_date') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_due_date] DATETIME2 NULL;

IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_paid_at') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_paid_at] DATETIME2 NULL;

IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_grace_until') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_grace_until] DATETIME2 NULL;

IF COL_LENGTH('dbo.ChargingStations', 'is_visible') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [is_visible] BIT NOT NULL CONSTRAINT [DF_ChargingStations_is_visible] DEFAULT ((1));

IF COL_LENGTH('dbo.ChargingStations', 'hidden_reason') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [hidden_reason] NVARCHAR(300) NULL;

IF COL_LENGTH('dbo.ChargingStations', 'last_maintenance_payment_id') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [last_maintenance_payment_id] INT NULL;

IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChargingStations]
    SET [maintenance_fee_due_date] = ISNULL([maintenance_fee_due_date], [MaintenancePaidUntil]),
        [maintenance_fee_paid_at] = ISNULL([maintenance_fee_paid_at], [LastMaintenancePaidAt]),
        [maintenance_fee_status] = CASE
            WHEN [OwnerUserId] IS NULL THEN N'active'
            WHEN [maintenance_fee_due_date] IS NULL OR [maintenance_fee_due_date] <= GETDATE() THEN N'maintenance_unpaid'
            WHEN DATEDIFF(DAY, GETDATE(), [maintenance_fee_due_date]) <= 7 THEN N'expiring_soon'
            ELSE N'active'
        END
    WHERE [OwnerUserId] IS NULL OR [maintenance_fee_due_date] IS NULL OR [maintenance_fee_paid_at] IS NULL;
END
