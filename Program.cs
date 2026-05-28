using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using tramsac99.Data;
using tramsac99.Services;

var builder = WebApplication.CreateBuilder(args);

// Why changed: add cookie auth so User/Admin can log in with roles
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/User/Account/Login";
        options.AccessDeniedPath = "/User/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<ExternalEvNewsService>(); // Why changed: fetch EV news from external RSS feeds.

builder.Services.AddScoped<LightAiService>();
builder.Services.Configure<SmtpEmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<tramsac99.Services.ChargingHierarchyService>(); // Why changed: sync station-pole statuses after removing charging-port UI.
builder.Services.AddScoped<PayOsCheckoutService>(); // Why changed: create payOS checkout links for station registration fees.

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Why changed: must run before authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Why changed: execute support schema upgrade in separate SQL batches so SQL Server can see new columns immediately.
    var supportUpgradeSqlCommands = new[]
    {
        @"IF OBJECT_ID(N'[dbo].[SupportRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupportRequests]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SenderUserId] INT NULL,
        [SenderUserName] NVARCHAR(100) NULL,
        [FullName] NVARCHAR(120) NOT NULL,
        [Email] NVARCHAR(150) NOT NULL,
        [PhoneNumber] NVARCHAR(30) NULL,
        [Subject] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_SupportRequests_Status] DEFAULT (N'Mới'),
        [IsRead] BIT NOT NULL CONSTRAINT [DF_SupportRequests_IsRead] DEFAULT ((0)),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SupportRequests_CreatedAt] DEFAULT (GETDATE()),
        [ReadAt] DATETIME2 NULL,
        [ResolvedAt] DATETIME2 NULL,
        [AdminReply] NVARCHAR(1000) NULL,
        [LastStatusChangedAt] DATETIME2 NULL,
        [IsUserSeen] BIT NOT NULL CONSTRAINT [DF_SupportRequests_IsUserSeen] DEFAULT ((1)),
        [UserSeenAt] DATETIME2 NULL
    );
END",
        @"IF COL_LENGTH('dbo.SupportRequests', 'SenderUserId') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [SenderUserId] INT NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'SenderUserName') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [SenderUserName] NVARCHAR(100) NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'PhoneNumber') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [PhoneNumber] NVARCHAR(30) NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'Status') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_SupportRequests_Status_Auto] DEFAULT (N'Mới');",
        @"IF COL_LENGTH('dbo.SupportRequests', 'IsRead') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [IsRead] BIT NOT NULL CONSTRAINT [DF_SupportRequests_IsRead_Auto] DEFAULT ((0));",
        @"IF COL_LENGTH('dbo.SupportRequests', 'CreatedAt') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SupportRequests_CreatedAt_Auto] DEFAULT (GETDATE());",
        @"IF COL_LENGTH('dbo.SupportRequests', 'ReadAt') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [ReadAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'ResolvedAt') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [ResolvedAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'AdminReply') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [AdminReply] NVARCHAR(1000) NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'LastStatusChangedAt') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [LastStatusChangedAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'IsUserSeen') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [IsUserSeen] BIT NOT NULL CONSTRAINT [DF_SupportRequests_IsUserSeen_Auto] DEFAULT ((1));",
        @"IF COL_LENGTH('dbo.SupportRequests', 'UserSeenAt') IS NULL
    ALTER TABLE [dbo].[SupportRequests] ADD [UserSeenAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'Status') IS NOT NULL
    UPDATE [dbo].[SupportRequests] SET [Status] = N'Mới' WHERE [Status] IS NULL;",
        @"IF COL_LENGTH('dbo.SupportRequests', 'LastStatusChangedAt') IS NOT NULL
   AND COL_LENGTH('dbo.SupportRequests', 'IsUserSeen') IS NOT NULL
BEGIN
    UPDATE [dbo].[SupportRequests]
    SET [LastStatusChangedAt] = CASE
            WHEN [LastStatusChangedAt] IS NOT NULL THEN [LastStatusChangedAt]
            WHEN [ResolvedAt] IS NOT NULL THEN [ResolvedAt]
            WHEN [ReadAt] IS NOT NULL THEN [ReadAt]
            ELSE [CreatedAt]
        END,
        [IsUserSeen] = CASE
            WHEN [Status] = N'Đã xử lý' AND [IsUserSeen] IS NULL THEN 0
            WHEN [IsUserSeen] IS NULL THEN 1
            ELSE [IsUserSeen]
        END
    WHERE [LastStatusChangedAt] IS NULL OR [IsUserSeen] IS NULL;
END",
        @"IF OBJECT_ID(N'[dbo].[SupportRequests]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.SupportRequests', 'Status') IS NOT NULL
   AND COL_LENGTH('dbo.SupportRequests', 'CreatedAt') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_SupportRequests_Status_CreatedAt'
          AND object_id = OBJECT_ID(N'[dbo].[SupportRequests]')
   )
BEGIN
    CREATE INDEX [IX_SupportRequests_Status_CreatedAt]
        ON [dbo].[SupportRequests]([Status], [CreatedAt]);
END"
    };

    var stationWorkflowUpgradeSqlCommands = new[]
    {
        // Why changed: keep owner link on charging station for "Tram cua toi".
        @"IF COL_LENGTH('dbo.ChargingStations', 'OwnerUserId') IS NULL
        ALTER TABLE [dbo].[ChargingStations] ADD [OwnerUserId] INT NULL;",

        // Why changed: old dev table may exist without UserId, so drop and recreate cleanly.
        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.StationRegistrationRequests', 'UserId') IS NULL
    BEGIN
        DROP TABLE [dbo].[StationRegistrationRequests];
    END",

        // Why changed: old dev table may exist without UserId, so drop and recreate cleanly.
        @"IF OBJECT_ID(N'[dbo].[StationOperationRequests]', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.StationOperationRequests', 'UserId') IS NULL
    BEGIN
        DROP TABLE [dbo].[StationOperationRequests];
    END",

        // Why changed: create registration request table for user -> admin -> payment workflow.
        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[StationRegistrationRequests]
        (
            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [UserId] INT NOT NULL,
            [StationName] NVARCHAR(200) NOT NULL,
            [OperatorName] NVARCHAR(200) NOT NULL,
            [ContactEmail] NVARCHAR(150) NOT NULL,
            [ContactPhone] NVARCHAR(30) NOT NULL,
            [Address] NVARCHAR(300) NOT NULL,
            [Latitude] FLOAT NOT NULL CONSTRAINT [DF_StationRegistrationRequests_Latitude] DEFAULT ((0)),
            [Longitude] FLOAT NOT NULL CONSTRAINT [DF_StationRegistrationRequests_Longitude] DEFAULT ((0)),
            [Description] NVARCHAR(1000) NULL,
            [ImageUrl] NVARCHAR(300) NULL,
            [InitialPoleCount] INT NOT NULL CONSTRAINT [DF_StationRegistrationRequests_InitialPoleCount] DEFAULT ((0)),
            [InitialPoleChargerType] NVARCHAR(100) NULL,
            [InitialPoleMaxPower] NVARCHAR(50) NULL,
            [InitialPoleNote] NVARCHAR(1000) NULL,
            [ApprovalStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_StationRegistrationRequests_ApprovalStatus] DEFAULT (N'Chờ duyệt'),
            [PaymentStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_StationRegistrationRequests_PaymentStatus] DEFAULT (N'Chưa thanh toán'),
            [PayOsOrderCode] BIGINT NULL,
            [PayOsCheckoutUrl] NVARCHAR(500) NULL,
            [FeeAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_StationRegistrationRequests_FeeAmount] DEFAULT ((50000)),
            [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_StationRegistrationRequests_CreatedAt] DEFAULT (GETDATE()),
            [ReviewedAt] DATETIME2 NULL,
            [PaidAt] DATETIME2 NULL,
            [CompletedAt] DATETIME2 NULL,
            [AdminNote] NVARCHAR(1000) NULL,
            [CreatedStationId] INT NULL
        );
    END",

        @"IF COL_LENGTH('dbo.StationRegistrationRequests', 'InitialPoleChargerType') IS NULL
        ALTER TABLE [dbo].[StationRegistrationRequests] ADD [InitialPoleChargerType] NVARCHAR(100) NULL;",

        // Why changed: keep charger type field in sync for charging poles on older DBs.
        @"IF COL_LENGTH('dbo.ChargingPoles', 'ChargerType') IS NULL
        ALTER TABLE [dbo].[ChargingPoles] ADD [ChargerType] NVARCHAR(100) NULL;",

        // Why changed: make old DBs switch FeeAmount default to 50000.
        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.default_object_id = dc.object_id
        INNER JOIN sys.tables t
            ON t.object_id = c.object_id
        WHERE t.name = N'StationRegistrationRequests'
          AND c.name = N'FeeAmount'
          AND dc.name <> N'DF_StationRegistrationRequests_FeeAmount_50000'
   )
BEGIN
    DECLARE @ConstraintName NVARCHAR(200);

    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t
        ON t.object_id = c.object_id
    WHERE t.name = N'StationRegistrationRequests'
      AND c.name = N'FeeAmount';

    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[StationRegistrationRequests] DROP CONSTRAINT [' + @ConstraintName + ']');
    END
END",

        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.default_object_id = dc.object_id
        INNER JOIN sys.tables t
            ON t.object_id = c.object_id
        WHERE t.name = N'StationRegistrationRequests'
          AND c.name = N'FeeAmount'
          AND dc.name = N'DF_StationRegistrationRequests_FeeAmount_50000'
   )
BEGIN
    ALTER TABLE [dbo].[StationRegistrationRequests]
    ADD CONSTRAINT [DF_StationRegistrationRequests_FeeAmount_50000]
    DEFAULT ((50000)) FOR [FeeAmount];
END",

        // Why changed: create station operation request table for status update / add pole requests.
        @"IF OBJECT_ID(N'[dbo].[StationOperationRequests]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[StationOperationRequests]
        (
            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [StationId] INT NOT NULL,
            [UserId] INT NOT NULL,
            [RequestType] NVARCHAR(50) NOT NULL,
            [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_StationOperationRequests_Status] DEFAULT (N'Chờ duyệt'),
            [RequestedStationStatus] NVARCHAR(50) NULL,
            [PoleId] INT NULL,
            [PoleCode] NVARCHAR(50) NULL,
            [PoleMaxPower] NVARCHAR(50) NULL,
            [RequestedPoleStatus] NVARCHAR(50) NULL,
            [UserNote] NVARCHAR(1000) NULL,
            [AdminNote] NVARCHAR(1000) NULL,
            [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_StationOperationRequests_CreatedAt] DEFAULT (GETDATE()),
            [ReviewedAt] DATETIME2 NULL,
            [CompletedAt] DATETIME2 NULL
        );
    END",

        // Why changed: keep new pole-management request fields in sync for update/delete flows.
        @"IF COL_LENGTH('dbo.StationOperationRequests', 'PoleId') IS NULL
        ALTER TABLE [dbo].[StationOperationRequests] ADD [PoleId] INT NULL;",

        @"IF COL_LENGTH('dbo.StationOperationRequests', 'RequestedPoleStatus') IS NULL
        ALTER TABLE [dbo].[StationOperationRequests] ADD [RequestedPoleStatus] NVARCHAR(50) NULL;",

        // Why changed: add index only after the column definitely exists.
        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.StationRegistrationRequests', 'UserId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_StationRegistrationRequests_UserId_ApprovalStatus_PaymentStatus_CreatedAt'
              AND object_id = OBJECT_ID(N'[dbo].[StationRegistrationRequests]')
       )
    BEGIN
        CREATE INDEX [IX_StationRegistrationRequests_UserId_ApprovalStatus_PaymentStatus_CreatedAt]
            ON [dbo].[StationRegistrationRequests]([UserId], [ApprovalStatus], [PaymentStatus], [CreatedAt]);
    END",

        // Why changed: keep payOS order lookup fast and unique.
        @"IF OBJECT_ID(N'[dbo].[StationRegistrationRequests]', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.StationRegistrationRequests', 'PayOsOrderCode') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_StationRegistrationRequests_PayOsOrderCode'
              AND object_id = OBJECT_ID(N'[dbo].[StationRegistrationRequests]')
       )
    BEGIN
        CREATE UNIQUE INDEX [IX_StationRegistrationRequests_PayOsOrderCode]
            ON [dbo].[StationRegistrationRequests]([PayOsOrderCode])
            WHERE [PayOsOrderCode] IS NOT NULL;
    END",

        // Why changed: add request list index only after required columns exist.
        @"IF OBJECT_ID(N'[dbo].[StationOperationRequests]', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.StationOperationRequests', 'StationId') IS NOT NULL
       AND COL_LENGTH('dbo.StationOperationRequests', 'Status') IS NOT NULL
       AND COL_LENGTH('dbo.StationOperationRequests', 'CreatedAt') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_StationOperationRequests_StationId_Status_CreatedAt'
              AND object_id = OBJECT_ID(N'[dbo].[StationOperationRequests]')
       )
    BEGIN
        CREATE INDEX [IX_StationOperationRequests_StationId_Status_CreatedAt]
            ON [dbo].[StationOperationRequests]([StationId], [Status], [CreatedAt]);
    END"
    };

    var passwordResetUpgradeSqlCommands = new[]
    {
        @"IF OBJECT_ID(N'[dbo].[PasswordResetTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PasswordResetTokens]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NOT NULL,
        [Token] NVARCHAR(200) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PasswordResetTokens_CreatedAt] DEFAULT (GETDATE()),
        [ExpiresAt] DATETIME2 NOT NULL,
        [UsedAt] DATETIME2 NULL,
        [RequestedByIp] NVARCHAR(50) NULL
    );
END",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'UserId') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [UserId] INT NOT NULL CONSTRAINT [DF_PasswordResetTokens_UserId] DEFAULT ((0));",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'Token') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [Token] NVARCHAR(200) NOT NULL CONSTRAINT [DF_PasswordResetTokens_Token] DEFAULT (N'');",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'CreatedAt') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PasswordResetTokens_CreatedAt_Auto] DEFAULT (GETDATE());",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'ExpiresAt') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [ExpiresAt] DATETIME2 NOT NULL CONSTRAINT [DF_PasswordResetTokens_ExpiresAt] DEFAULT (DATEADD(HOUR, 1, GETDATE()));",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'UsedAt') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [UsedAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.PasswordResetTokens', 'RequestedByIp') IS NULL
    ALTER TABLE [dbo].[PasswordResetTokens] ADD [RequestedByIp] NVARCHAR(50) NULL;",
        @"IF OBJECT_ID(N'[dbo].[PasswordResetTokens]', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_PasswordResetTokens_Token'
              AND object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]')
       )
    BEGIN
        CREATE UNIQUE INDEX [IX_PasswordResetTokens_Token]
            ON [dbo].[PasswordResetTokens]([Token]);
    END",
        @"IF OBJECT_ID(N'[dbo].[PasswordResetTokens]', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_PasswordResetTokens_UserId_ExpiresAt_UsedAt'
              AND object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]')
       )
    BEGIN
        CREATE INDEX [IX_PasswordResetTokens_UserId_ExpiresAt_UsedAt]
            ON [dbo].[PasswordResetTokens]([UserId], [ExpiresAt], [UsedAt]);
    END"
    };


    var routeHistoryUpgradeSqlCommands = new[]
    {
        @"IF OBJECT_ID(N'[dbo].[RouteHistories]', N'U') IS NULL
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
        [TotalDistanceKm] FLOAT NOT NULL CONSTRAINT [DF_RouteHistories_TotalDistanceKm] DEFAULT ((0)),
        [FirstLegKm] FLOAT NOT NULL CONSTRAINT [DF_RouteHistories_FirstLegKm] DEFAULT ((0)),
        [VehicleRangeKm] INT NOT NULL CONSTRAINT [DF_RouteHistories_VehicleRangeKm] DEFAULT ((0)),
        [StartBattery] INT NOT NULL CONSTRAINT [DF_RouteHistories_StartBattery] DEFAULT ((0)),
        [ReserveBattery] INT NOT NULL CONSTRAINT [DF_RouteHistories_ReserveBattery] DEFAULT ((0)),
        [MaxDetourKm] INT NOT NULL CONSTRAINT [DF_RouteHistories_MaxDetourKm] DEFAULT ((0)),
        [StopCount] INT NOT NULL CONSTRAINT [DF_RouteHistories_StopCount] DEFAULT ((0)),
        [StopsJson] NVARCHAR(MAX) NULL,
        [RoutePathJson] NVARCHAR(MAX) NULL,
        [IsFavorite] BIT NOT NULL CONSTRAINT [DF_RouteHistories_IsFavorite] DEFAULT ((0)),
        [IsShared] BIT NOT NULL CONSTRAINT [DF_RouteHistories_IsShared] DEFAULT ((0)),
        [ShareToken] NVARCHAR(80) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RouteHistories_CreatedAt] DEFAULT (GETDATE()),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RouteHistories_UpdatedAt] DEFAULT (GETDATE()),
        [LastUsedAt] DATETIME2 NULL
    );
END",
        @"IF OBJECT_ID(N'[dbo].[RouteHistories]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RouteHistories_UserId_IsFavorite_LastUsedAt_UpdatedAt'
          AND object_id = OBJECT_ID(N'[dbo].[RouteHistories]')
   )
BEGIN
    CREATE INDEX [IX_RouteHistories_UserId_IsFavorite_LastUsedAt_UpdatedAt]
        ON [dbo].[RouteHistories]([UserId], [IsFavorite], [LastUsedAt], [UpdatedAt]);
END",
        @"IF OBJECT_ID(N'[dbo].[RouteHistories]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.RouteHistories', 'ShareToken') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RouteHistories_ShareToken'
          AND object_id = OBJECT_ID(N'[dbo].[RouteHistories]')
   )
BEGIN
    CREATE UNIQUE INDEX [IX_RouteHistories_ShareToken]
        ON [dbo].[RouteHistories]([ShareToken])
        WHERE [ShareToken] IS NOT NULL;
END"
    };


    var maintenancePaymentUpgradeSqlCommands = new[]
    {
        @"IF COL_LENGTH('dbo.ChargingStations', 'MonthlyMaintenanceFee') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [MonthlyMaintenanceFee] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_ChargingStations_MonthlyMaintenanceFee] DEFAULT ((10000));",
        @"IF COL_LENGTH('dbo.ChargingStations', 'LastMaintenancePaidAt') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [LastMaintenancePaidAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [MaintenancePaidUntil] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaymentStatus') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [MaintenancePaymentStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChargingStations_MaintenancePaymentStatus] DEFAULT (N'Chưa đến hạn');",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'MonthlyMaintenanceFee') IS NOT NULL
        BEGIN
            UPDATE [dbo].[ChargingStations]
            SET [MonthlyMaintenanceFee] = 10000
            WHERE [MonthlyMaintenanceFee] IS NULL OR [MonthlyMaintenanceFee] <= 0;
        END",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'OwnerUserId') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NOT NULL
          AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ChargingStations_OwnerUserId_MaintenancePaidUntil'
                  AND object_id = OBJECT_ID(N'[dbo].[ChargingStations]')
          )
        BEGIN
            CREATE INDEX [IX_ChargingStations_OwnerUserId_MaintenancePaidUntil]
                ON [dbo].[ChargingStations]([OwnerUserId], [MaintenancePaidUntil]);
        END"
        ,
        @"IF COL_LENGTH('dbo.ChargingStations', 'system_status') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [system_status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_ChargingStations_system_status] DEFAULT (N'approved');",
        @"IF COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [operation_status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_ChargingStations_operation_status] DEFAULT (N'active');",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
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
                    WHEN [Status] IN (N'Hoạt động', N'Đang hoạt động', N'active', N'Active') THEN N'active'
                    WHEN [Status] IN (N'Bảo trì', N'Đang bảo trì', N'maintenance', N'Maintenance') THEN N'maintenance'
                    WHEN [Status] IN (N'Không hoạt động', N'inactive', N'Inactive') THEN N'inactive'
                    WHEN [Status] IN (N'Lỗi', N'Lỗi kỹ thuật', N'Quá tải', N'error', N'technical_error', N'overloaded') THEN N'error'
                    ELSE N'active'
                END
            WHERE [system_status] IS NULL
               OR LTRIM(RTRIM([system_status])) = N''
               OR [operation_status] IS NULL
               OR LTRIM(RTRIM([operation_status])) = N'';
        END",
        @"IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_status') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_ChargingStations_maintenance_fee_status] DEFAULT (N'active');",
        @"IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_due_date') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_due_date] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_paid_at') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_paid_at] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_grace_until') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [maintenance_fee_grace_until] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'is_visible') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [is_visible] BIT NOT NULL CONSTRAINT [DF_ChargingStations_is_visible] DEFAULT ((1));",
        @"IF COL_LENGTH('dbo.ChargingStations', 'hidden_reason') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [hidden_reason] NVARCHAR(300) NULL;",
        @"IF COL_LENGTH('dbo.ChargingStations', 'last_maintenance_payment_id') IS NULL
            ALTER TABLE [dbo].[ChargingStations] ADD [last_maintenance_payment_id] INT NULL;",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_due_date') IS NOT NULL
        BEGIN
            UPDATE [dbo].[ChargingStations]
            SET [maintenance_fee_due_date] = ISNULL([maintenance_fee_due_date], [MaintenancePaidUntil]),
                [maintenance_fee_paid_at] = ISNULL([maintenance_fee_paid_at], [LastMaintenancePaidAt]),
                [maintenance_fee_status] = CASE
                    WHEN [OwnerUserId] IS NULL THEN N'active'
                    WHEN [maintenance_fee_due_date] IS NULL OR [maintenance_fee_due_date] <= GETDATE() THEN N'maintenance_unpaid'
                    WHEN DATEDIFF(DAY, GETDATE(), [maintenance_fee_due_date]) <= 7 THEN N'expiring_soon'
                    ELSE N'active'
                END,
                [is_visible] = CASE
                    WHEN [OwnerUserId] IS NULL THEN 1
                    WHEN [maintenance_fee_status] IN (N'hidden', N'locked') THEN 0
                    ELSE [is_visible]
                END
            WHERE [OwnerUserId] IS NULL OR [maintenance_fee_due_date] IS NULL OR [maintenance_fee_paid_at] IS NULL;
        END",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'is_visible') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'maintenance_fee_status') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'Status') IS NOT NULL
          AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ChargingStations_IsVisible_MaintenanceFeeStatus_Status'
                  AND object_id = OBJECT_ID(N'[dbo].[ChargingStations]')
          )
        BEGIN
            CREATE INDEX [IX_ChargingStations_IsVisible_MaintenanceFeeStatus_Status]
                ON [dbo].[ChargingStations]([is_visible], [maintenance_fee_status], [Status]);
        END",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
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
        END",
        @"IF OBJECT_ID(N'[dbo].[ChargingStations]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'Status') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'operation_status') IS NOT NULL
        BEGIN
            UPDATE [dbo].[ChargingStations]
            SET [Status] = CASE
                    WHEN [Status] IN (N'Hoạt động', N'active', N'Active') THEN N'Đang hoạt động'
                    WHEN [Status] IN (N'Lỗi kỹ thuật', N'Quá tải', N'technical_error', N'overloaded', N'error') THEN N'Lỗi'
                    WHEN [Status] IN (N'Đang bảo trì', N'maintenance', N'Maintenance') THEN N'Bảo trì'
                    WHEN [Status] IN (N'Tạm ngừng', N'Tạm ngừng hoạt động', N'inactive', N'Inactive') THEN N'Không hoạt động'
                    WHEN [Status] IN (N'Đang hoạt động', N'Lỗi', N'Bảo trì', N'Không hoạt động') THEN [Status]
                    ELSE N'Đang hoạt động'
                END,
                [operation_status] = CASE
                    WHEN [operation_status] IN (N'active', N'Hoạt động', N'Đang hoạt động') THEN N'active'
                    WHEN [operation_status] IN (N'technical_error', N'overloaded', N'error', N'Lỗi kỹ thuật', N'Quá tải', N'Lỗi') THEN N'error'
                    WHEN [operation_status] IN (N'maintenance', N'Bảo trì', N'Đang bảo trì') THEN N'maintenance'
                    WHEN [operation_status] IN (N'inactive', N'Không hoạt động', N'Tạm ngừng hoạt động') THEN N'inactive'
                    ELSE N'active'
                END;
        END"
    };

    foreach (var sql in supportUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    foreach (var sql in stationWorkflowUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    await db.Database.ExecuteSqlRawAsync(@"IF OBJECT_ID(N'[dbo].[ChargingPoles]', N'U') IS NOT NULL
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
END");

    foreach (var sql in maintenancePaymentUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }


    // Why changed: older databases may miss this table; PayOS return writes logs while completing station registration.
    var stationActivityLogUpgradeSqlCommands = new[]
    {
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NULL
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
END",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'station_id') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [station_id] INT NOT NULL CONSTRAINT [DF_station_activity_logs_station_id] DEFAULT ((0));",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'user_id') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [user_id] INT NULL;",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'action_type') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [action_type] NVARCHAR(80) NOT NULL CONSTRAINT [DF_station_activity_logs_action_type] DEFAULT (N'unknown');",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'old_value') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [old_value] NVARCHAR(1000) NULL;",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'new_value') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [new_value] NVARCHAR(1000) NULL;",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'description') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [description] NVARCHAR(1000) NULL;",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.station_activity_logs', 'created_at') IS NULL
    ALTER TABLE [dbo].[station_activity_logs] ADD [created_at] DATETIME2 NOT NULL CONSTRAINT [DF_station_activity_logs_created_at_auto] DEFAULT (GETDATE());",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_station_activity_logs_station_id_created_at'
          AND object_id = OBJECT_ID(N'[dbo].[station_activity_logs]')
   )
BEGIN
    CREATE INDEX [IX_station_activity_logs_station_id_created_at]
    ON [dbo].[station_activity_logs]([station_id], [created_at]);
END",
        @"IF OBJECT_ID(N'[dbo].[station_activity_logs]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_station_activity_logs_user_id_action_type_created_at'
          AND object_id = OBJECT_ID(N'[dbo].[station_activity_logs]')
   )
BEGIN
    CREATE INDEX [IX_station_activity_logs_user_id_action_type_created_at]
    ON [dbo].[station_activity_logs]([user_id], [action_type], [created_at]);
END"
    };

    foreach (var sql in stationActivityLogUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    var paymentTransactionSqlCommands = new[]
    {
        @"IF OBJECT_ID(N'[dbo].[PaymentTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentTransactions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NOT NULL,
        [StationId] INT NULL,
        [RegistrationRequestId] INT NULL,
        [PaymentType] NVARCHAR(40) NOT NULL CONSTRAINT [DF_PaymentTransactions_PaymentType] DEFAULT (N'Phí kích hoạt trạm'),
        [Status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_PaymentTransactions_Status] DEFAULT (N'Đang chờ thanh toán'),
        [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_PaymentTransactions_Amount] DEFAULT ((0)),
        [PayOsOrderCode] BIGINT NULL,
        [PayOsCheckoutUrl] NVARCHAR(500) NULL,
        [Description] NVARCHAR(500) NULL,
        [Note] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentTransactions_CreatedAt] DEFAULT (GETDATE()),
        [PaidAt] DATETIME2 NULL,
        [CancelledAt] DATETIME2 NULL
    );
END",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'StationId') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [StationId] INT NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'RegistrationRequestId') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [RegistrationRequestId] INT NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'PaymentType') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [PaymentType] NVARCHAR(40) NOT NULL CONSTRAINT [DF_PaymentTransactions_PaymentType_Auto] DEFAULT (N'Phí kích hoạt trạm');",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'Status') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [Status] NVARCHAR(40) NOT NULL CONSTRAINT [DF_PaymentTransactions_Status_Auto] DEFAULT (N'Đang chờ thanh toán');",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'Amount') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_PaymentTransactions_Amount_Auto] DEFAULT ((0));",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'PayOsOrderCode') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [PayOsOrderCode] BIGINT NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'PayOsCheckoutUrl') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [PayOsCheckoutUrl] NVARCHAR(500) NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'Description') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [Description] NVARCHAR(500) NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'Note') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [Note] NVARCHAR(1000) NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'CreatedAt') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentTransactions_CreatedAt_Auto] DEFAULT (GETDATE());",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'PaidAt') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [PaidAt] DATETIME2 NULL;",
        @"IF COL_LENGTH('dbo.PaymentTransactions', 'CancelledAt') IS NULL
            ALTER TABLE [dbo].[PaymentTransactions] ADD [CancelledAt] DATETIME2 NULL;",
        @"IF OBJECT_ID(N'[dbo].[PaymentTransactions]', N'U') IS NOT NULL
          AND COL_LENGTH('dbo.PaymentTransactions', 'PayOsOrderCode') IS NOT NULL
          AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_PaymentTransactions_PayOsOrderCode'
                  AND object_id = OBJECT_ID(N'[dbo].[PaymentTransactions]')
          )
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransactions_PayOsOrderCode]
        ON [dbo].[PaymentTransactions]([PayOsOrderCode])
        WHERE [PayOsOrderCode] IS NOT NULL;
END",
        @"IF OBJECT_ID(N'[dbo].[PaymentTransactions]', N'U') IS NOT NULL
          AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_PaymentTransactions_UserId_PaymentType_Status_CreatedAt'
                  AND object_id = OBJECT_ID(N'[dbo].[PaymentTransactions]')
          )
BEGIN
    CREATE INDEX [IX_PaymentTransactions_UserId_PaymentType_Status_CreatedAt]
        ON [dbo].[PaymentTransactions]([UserId], [PaymentType], [Status], [CreatedAt]);
END",
        @"IF OBJECT_ID(N'[dbo].[PaymentTransactions]', N'U') IS NOT NULL
          AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_PaymentTransactions_StationId_PaymentType_CreatedAt'
                  AND object_id = OBJECT_ID(N'[dbo].[PaymentTransactions]')
          )
BEGIN
    CREATE INDEX [IX_PaymentTransactions_StationId_PaymentType_CreatedAt]
        ON [dbo].[PaymentTransactions]([StationId], [PaymentType], [CreatedAt]);
END",
        @"IF COL_LENGTH('dbo.ChargingStations', 'MaintenancePaymentStatus') IS NOT NULL
          AND COL_LENGTH('dbo.ChargingStations', 'MaintenancePaidUntil') IS NOT NULL
BEGIN
    UPDATE [dbo].[ChargingStations]
    SET [MaintenancePaymentStatus] = CASE
        WHEN [OwnerUserId] IS NULL THEN N'Không áp dụng'
        WHEN [MaintenancePaidUntil] IS NULL OR [MaintenancePaidUntil] <= GETDATE() THEN N'Chưa thanh toán phí duy trì'
        ELSE N'Đã thanh toán'
    END
    WHERE [MaintenancePaymentStatus] IS NULL
       OR [MaintenancePaymentStatus] <> CASE
            WHEN [OwnerUserId] IS NULL THEN N'Không áp dụng'
            WHEN [MaintenancePaidUntil] IS NULL OR [MaintenancePaidUntil] <= GETDATE() THEN N'Chưa thanh toán phí duy trì'
            ELSE N'Đã thanh toán'
        END;
END"
    };

    foreach (var sql in paymentTransactionSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }


    foreach (var sql in passwordResetUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    foreach (var sql in routeHistoryUpgradeSqlCommands)
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    DbSeeder.Seed(db);
}

app.Run();
