using Microsoft.AspNetCore.Identity;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            var hasher = new PasswordHasher<AppUser>();

            var admin = context.AppUsers.FirstOrDefault(x => x.Username == "admin");
            if (admin == null)
            {
                admin = new AppUser
                {
                    Username = "admin",
                    Email = "admin@example.com",
                    FullName = "Admin User",
                    Role = "Admin",
                    IsBlocked = false,
                    CreatedAt = DateTime.Now
                };
                admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");
                context.AppUsers.Add(admin);
            }
            else
            {
                admin.Email = "admin@example.com";
                admin.FullName = "Admin User";
                admin.Role = "Admin";
                admin.IsBlocked = false;
                admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");
            }

            var user1 = context.AppUsers.FirstOrDefault(x => x.Username == "user1");
            if (user1 == null)
            {
                user1 = new AppUser
                {
                    Username = "user1",
                    Email = "user1@example.com",
                    FullName = "User One",
                    Role = "User",
                    IsBlocked = false,
                    CreatedAt = DateTime.Now
                };
                user1.PasswordHash = hasher.HashPassword(user1, "123456");
                context.AppUsers.Add(user1);
            }

            var user2 = context.AppUsers.FirstOrDefault(x => x.Username == "user2");
            if (user2 == null)
            {
                user2 = new AppUser
                {
                    Username = "user2",
                    Email = "user2@example.com",
                    FullName = "User Two",
                    Role = "User",
                    IsBlocked = false,
                    CreatedAt = DateTime.Now
                };
                user2.PasswordHash = hasher.HashPassword(user2, "123456");
                context.AppUsers.Add(user2);
            }

            context.SaveChanges();

            if (!context.ChargingStations.Any())
            {
                var user1Id = context.AppUsers.First(x => x.Username == "user1").Id;
                var user2Id = context.AppUsers.First(x => x.Username == "user2").Id;

                var stations = new List<ChargingStation>
                {
                    new ChargingStation
                    {
                        Name = "Trạm Sạc VinFast Hải Phòng 1",
                        Address = "Lê Hồng Phong, Hải An, Hải Phòng",
                        Latitude = 20.8449,
                        Longitude = 106.6881,
                        Status = "Đang hoạt động",
                        ChargerType = "CCS2",
                        Power = "120kW",
                        PricePerKwh = 3500,
                        OwnerUserId = user1Id
                    },
                    new ChargingStation
                    {
                        Name = "Trạm Sạc VinFast Quận 7",
                        Address = "Nguyễn Hữu Thọ, Quận 7, TP.HCM",
                        Latitude = 10.7342,
                        Longitude = 106.7219,
                        Status = "Đang hoạt động",
                        ChargerType = "CCS2",
                        Power = "150kW",
                        PricePerKwh = 3800,
                        OwnerUserId = user2Id
                    },
                    new ChargingStation
                    {
                        Name = "Trạm Sạc Thủ Đức",
                        Address = "Xa Lộ Hà Nội, Thủ Đức, TP.HCM",
                        Latitude = 10.8506,
                        Longitude = 106.7712,
                        Status = "Bảo trì",
                        ChargerType = "Type 2",
                        Power = "60kW",
                        PricePerKwh = 3200
                    }
                };

                context.ChargingStations.AddRange(stations);
                context.SaveChanges();
            }

            if (!context.StationReviews.Any())
            {
                var station1 = context.ChargingStations.FirstOrDefault(x => x.Name == "Trạm Sạc VinFast Hải Phòng 1");
                var station2 = context.ChargingStations.FirstOrDefault(x => x.Name == "Trạm Sạc VinFast Quận 7");

                var reviews = new List<StationReview>();

                if (station1 != null)
                {
                    reviews.Add(new StationReview { StationId = station1.Id, Rating = 5, UserName = "user1", Comment = "Trạm tốt, dễ tìm" });
                    reviews.Add(new StationReview { StationId = station1.Id, Rating = 4, UserName = "user2", Comment = "Sạc ổn, chỗ đậu xe rộng" });
                }

                if (station2 != null)
                {
                    reviews.Add(new StationReview { StationId = station2.Id, Rating = 5, UserName = "user1", Comment = "Nhân viên hỗ trợ nhanh" });
                }

                if (reviews.Any())
                {
                    context.StationReviews.AddRange(reviews);
                    context.SaveChanges();
                }
            }

            if (!context.ChargingPoles.Any())
            {
                var station1 = context.ChargingStations.FirstOrDefault(x => x.Name == "Trạm Sạc VinFast Hải Phòng 1");
                var station2 = context.ChargingStations.FirstOrDefault(x => x.Name == "Trạm Sạc VinFast Quận 7");

                var poles = new List<ChargingPole>();

                if (station1 != null)
                {
                    poles.Add(new ChargingPole { StationId = station1.Id, PoleCode = "TRU-01", ChargerType = "CCS2", MaxPower = "120 kW", Status = ChargingStatus.Active, SortOrder = 1 });
                    poles.Add(new ChargingPole { StationId = station1.Id, PoleCode = "TRU-02", ChargerType = "CCS2", MaxPower = "120 kW", Status = ChargingStatus.Active, SortOrder = 2 });
                }

                if (station2 != null)
                {
                    poles.Add(new ChargingPole { StationId = station2.Id, PoleCode = "TRU-01", ChargerType = "CCS2", MaxPower = "150 kW", Status = ChargingStatus.Active, SortOrder = 1 });
                }

                if (poles.Any())
                {
                    context.ChargingPoles.AddRange(poles);
                    context.SaveChanges();
                }
            }
        }
    }
}
