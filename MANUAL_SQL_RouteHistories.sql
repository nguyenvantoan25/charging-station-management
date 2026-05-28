IF OBJECT_ID(N'[dbo].[RouteHistories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RouteHistories]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NOT NULL,
        [RouteName] NVARCHAR(200) NOT NULL,
        [StartName] NVARCHAR(250) NOT NULL,
        [StartAddress] NVARCHAR(500) NULL,
        [StartLatitude] FLOAT NOT NULL,
        [StartLongitude] FLOAT NOT NULL,
        [EndName] NVARCHAR(250) NOT NULL,
        [EndAddress] NVARCHAR(500) NULL,
        [EndLatitude] FLOAT NOT NULL,
        [EndLongitude] FLOAT NOT NULL,
        [TotalDistanceKm] FLOAT NOT NULL DEFAULT ((0)),
        [FirstLegKm] FLOAT NOT NULL DEFAULT ((0)),
        [VehicleRangeKm] INT NOT NULL DEFAULT ((0)),
        [StartBattery] INT NOT NULL DEFAULT ((0)),
        [ReserveBattery] INT NOT NULL DEFAULT ((0)),
        [MaxDetourKm] INT NOT NULL DEFAULT ((0)),
        [StopCount] INT NOT NULL DEFAULT ((0)),
        [StopsJson] NVARCHAR(MAX) NULL,
        [RoutePathJson] NVARCHAR(MAX) NULL,
        [IsFavorite] BIT NOT NULL DEFAULT ((0)),
        [IsShared] BIT NOT NULL DEFAULT ((0)),
        [ShareToken] NVARCHAR(80) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        [LastUsedAt] DATETIME2 NULL
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RouteHistories_UserId_IsFavorite_LastUsedAt_UpdatedAt'
      AND object_id = OBJECT_ID(N'[dbo].[RouteHistories]')
)
BEGIN
    CREATE INDEX [IX_RouteHistories_UserId_IsFavorite_LastUsedAt_UpdatedAt]
        ON [dbo].[RouteHistories]([UserId], [IsFavorite], [LastUsedAt], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RouteHistories_ShareToken'
      AND object_id = OBJECT_ID(N'[dbo].[RouteHistories]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_RouteHistories_ShareToken]
        ON [dbo].[RouteHistories]([ShareToken])
        WHERE [ShareToken] IS NOT NULL;
END;
