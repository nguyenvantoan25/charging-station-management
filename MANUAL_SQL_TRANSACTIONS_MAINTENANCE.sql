IF OBJECT_ID(N'[dbo].[PaymentTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentTransactions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NOT NULL,
        [StationId] INT NULL,
        [RegistrationRequestId] INT NULL,
        [PaymentType] NVARCHAR(40) NOT NULL DEFAULT (N'Phí kích hoạt trạm'),
        [Status] NVARCHAR(40) NOT NULL DEFAULT (N'Đang chờ thanh toán'),
        [Amount] DECIMAL(18,2) NOT NULL DEFAULT ((0)),
        [PayOsOrderCode] BIGINT NULL,
        [PayOsCheckoutUrl] NVARCHAR(500) NULL,
        [Description] NVARCHAR(500) NULL,
        [Note] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        [PaidAt] DATETIME2 NULL,
        [CancelledAt] DATETIME2 NULL
    );
END;

IF COL_LENGTH('dbo.ChargingStations', 'MonthlyMaintenanceFee') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [MonthlyMaintenanceFee] DECIMAL(18,2) NOT NULL DEFAULT ((10000));

IF COL_LENGTH('dbo.ChargingStations', 'LastMaintenancePaidAt') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [LastMaintenancePaidAt] DATETIME2 NULL;

IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [MaintenancePaidUntil] DATETIME2 NULL;

IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaymentStatus') IS NULL
    ALTER TABLE [dbo].[ChargingStations] ADD [MaintenancePaymentStatus] NVARCHAR(30) NOT NULL DEFAULT (N'Chưa thanh toán phí duy trì');

UPDATE [dbo].[ChargingStations]
SET [MonthlyMaintenanceFee] = 10000
WHERE [MonthlyMaintenanceFee] IS NULL OR [MonthlyMaintenanceFee] <= 0;

UPDATE [dbo].[ChargingStations]
SET [MaintenancePaymentStatus] = CASE
    WHEN [OwnerUserId] IS NULL THEN N'Không áp dụng'
    WHEN [MaintenancePaidUntil] IS NULL OR [MaintenancePaidUntil] <= GETDATE() THEN N'Chưa thanh toán phí duy trì'
    ELSE N'Đã thanh toán'
END;
