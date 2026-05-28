-- Cập nhật phí đăng ký trạm và phí duy trì
-- Dùng khi Program.cs không tự cập nhật được database.

IF COL_LENGTH('dbo.StationRegistrationRequests', 'FeeAmount') IS NOT NULL
BEGIN
    UPDATE dbo.StationRegistrationRequests
    SET FeeAmount = 50000
    WHERE FeeAmount IS NULL OR FeeAmount <= 0 OR FeeAmount = 5000;
END

IF COL_LENGTH('dbo.ChargingStations', 'MonthlyMaintenanceFee') IS NULL
BEGIN
    ALTER TABLE dbo.ChargingStations
    ADD MonthlyMaintenanceFee DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_ChargingStations_MonthlyMaintenanceFee DEFAULT ((10000));
END

IF COL_LENGTH('dbo.ChargingStations', 'LastMaintenancePaidAt') IS NULL
BEGIN
    ALTER TABLE dbo.ChargingStations
    ADD LastMaintenancePaidAt DATETIME2 NULL;
END

IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NULL
BEGIN
    ALTER TABLE dbo.ChargingStations
    ADD MaintenancePaidUntil DATETIME2 NULL;
END

IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaymentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.ChargingStations
    ADD MaintenancePaymentStatus NVARCHAR(30) NOT NULL
        CONSTRAINT DF_ChargingStations_MaintenancePaymentStatus DEFAULT (N'Chưa đến hạn');
END

UPDATE dbo.ChargingStations
SET MonthlyMaintenanceFee = 10000
WHERE MonthlyMaintenanceFee IS NULL OR MonthlyMaintenanceFee <= 0;
