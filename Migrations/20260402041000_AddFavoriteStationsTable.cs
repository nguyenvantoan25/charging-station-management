using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tramsac99.Data;

#nullable disable

namespace tramsac99.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260402041000_AddFavoriteStationsTable")]
    public partial class AddFavoriteStationsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FavoriteStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteStations_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteStations_ChargingStations_StationId",
                        column: x => x.StationId,
                        principalTable: "ChargingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteStations_StationId",
                table: "FavoriteStations",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteStations_UserId_StationId",
                table: "FavoriteStations",
                columns: new[] { "UserId", "StationId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteStations");
        }
    }
}
