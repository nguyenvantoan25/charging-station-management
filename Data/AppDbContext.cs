using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ChargingStation> ChargingStations { get; set; }
        public DbSet<StationReview> StationReviews { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<ChargingPole> ChargingPoles { get; set; }
        public DbSet<ChargingPort> ChargingPorts { get; set; }
        public DbSet<FavoriteStation> FavoriteStations { get; set; } // Why changed: store user's favorite stations
        public DbSet<SupportRequest> SupportRequests { get; set; } // Why changed: store contact/support requests from user side.
        public DbSet<StationRegistrationRequest> StationRegistrationRequests { get; set; }
        public DbSet<StationOperationRequest> StationOperationRequests { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<RouteHistory> RouteHistories { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<StationActivityLog> StationActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ChargingStation>()
                .HasMany(s => s.Reviews)
                .WithOne(r => r.ChargingStation)
                .HasForeignKey(r => r.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChargingStation>()
                .HasMany(s => s.ChargingPoles)
                .WithOne(p => p.ChargingStation)
                .HasForeignKey(p => p.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChargingPole>()
                .HasMany(p => p.ChargingPorts)
                .WithOne(c => c.ChargingPole)
                .HasForeignKey(c => c.PoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppUser>()
                .HasIndex(x => x.Username)
                .IsUnique();

            modelBuilder.Entity<AppUser>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<ChargingPole>()
                .HasIndex(x => new { x.StationId, x.PoleCode })
                .IsUnique();

            modelBuilder.Entity<ChargingPort>()
                .HasIndex(x => new { x.PoleId, x.PortCode })
                .IsUnique();

            modelBuilder.Entity<FavoriteStation>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Why changed: delete favorite row when user is deleted

            modelBuilder.Entity<FavoriteStation>()
                .HasOne(x => x.ChargingStation)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade); // Why changed: delete favorite row when station is deleted

            modelBuilder.Entity<FavoriteStation>()
                .HasIndex(x => new { x.UserId, x.StationId })
                .IsUnique(); // Why changed: one user can favorite one station only once

            modelBuilder.Entity<StationReview>()
                .HasIndex(x => new { x.StationId, x.UserName })
                .IsUnique(); // Why changed: one account can review one station only once

            modelBuilder.Entity<SupportRequest>()
                .HasIndex(x => new { x.Status, x.CreatedAt }); // Why changed: support page can filter and sort faster.

            modelBuilder.Entity<SupportRequest>()
                .Property(x => x.Status)
                .HasMaxLength(30);

            modelBuilder.Entity<SupportRequest>()
                .Property(x => x.AdminReply)
                .HasMaxLength(1000); // Why changed: allow admin to reply when resolving a support ticket

            modelBuilder.Entity<SupportRequest>()
                .HasIndex(x => new { x.SenderUserId, x.Status, x.IsUserSeen }); // Why changed: speed up user-side support notification queries

            modelBuilder.Entity<ChargingStation>()
                .HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull); // Why changed: keep station data even if owner account is removed.

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.MonthlyMaintenanceFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.MaintenancePaymentStatus)
                .HasMaxLength(30);

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.MaintenanceFeeStatus)
                .HasMaxLength(40);

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.SystemStatus)
                .HasMaxLength(30);

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.OperationStatus)
                .HasMaxLength(40);

            modelBuilder.Entity<ChargingStation>()
                .Property(x => x.HiddenReason)
                .HasMaxLength(300);

            modelBuilder.Entity<ChargingStation>()
                .HasIndex(x => new { x.OwnerUserId, x.MaintenancePaidUntil });

            modelBuilder.Entity<ChargingStation>()
                .HasIndex(x => new { x.IsVisible, x.MaintenanceFeeStatus, x.Status })
                .HasDatabaseName("IX_ChargingStations_IsVisible_MaintenanceFeeStatus_Status");

            modelBuilder.Entity<ChargingStation>()
                .HasIndex(x => new { x.SystemStatus, x.OperationStatus, x.MaintenanceFeeStatus, x.IsVisible })
                .HasDatabaseName("IX_ChargingStations_PublicBusinessStatus");

            modelBuilder.Entity<StationActivityLog>()
                .HasOne(x => x.Station)
                .WithMany(x => x.ActivityLogs)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StationActivityLog>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StationActivityLog>()
                .HasIndex(x => new { x.StationId, x.CreatedAt });

            modelBuilder.Entity<StationActivityLog>()
                .HasIndex(x => new { x.UserId, x.ActionType, x.CreatedAt });

            modelBuilder.Entity<StationRegistrationRequest>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StationRegistrationRequest>()
                .HasOne(x => x.CreatedStation)
                .WithMany()
                .HasForeignKey(x => x.CreatedStationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StationOperationRequest>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StationOperationRequest>()
                .HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StationRegistrationRequest>()
                .HasIndex(x => new { x.UserId, x.ApprovalStatus, x.PaymentStatus, x.CreatedAt });

            modelBuilder.Entity<StationRegistrationRequest>()
                .HasIndex(x => x.PayOsOrderCode)
                .IsUnique()
                .HasFilter("[PayOsOrderCode] IS NOT NULL");

            modelBuilder.Entity<StationOperationRequest>()
                .HasIndex(x => new { x.StationId, x.Status, x.CreatedAt });

            modelBuilder.Entity<StationRegistrationRequest>()
                .Property(x => x.FeeAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(x => x.Token)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(x => new { x.UserId, x.ExpiresAt, x.UsedAt });

            modelBuilder.Entity<RouteHistory>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RouteHistory>()
                .HasIndex(x => new { x.UserId, x.IsFavorite, x.LastUsedAt, x.UpdatedAt });

            modelBuilder.Entity<RouteHistory>()
                .HasIndex(x => x.ShareToken)
                .IsUnique()
                .HasFilter("[ShareToken] IS NOT NULL");

            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PaymentTransaction>()
                .HasOne(x => x.RegistrationRequest)
                .WithMany()
                .HasForeignKey(x => x.RegistrationRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PaymentTransaction>()
                .Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => x.PayOsOrderCode)
                .IsUnique()
                .HasFilter("[PayOsOrderCode] IS NOT NULL");

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => new { x.UserId, x.PaymentType, x.Status, x.CreatedAt });

            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(x => new { x.StationId, x.PaymentType, x.CreatedAt });
        }
    }
}
