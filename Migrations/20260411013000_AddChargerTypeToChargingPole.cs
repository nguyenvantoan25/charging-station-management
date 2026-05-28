using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tramsac99.Migrations
{
    public partial class AddChargerTypeToChargingPole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargerType",
                table: "ChargingPoles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.ChargerType = s.ChargerType
                FROM ChargingPoles p
                INNER JOIN ChargingStations s ON s.Id = p.StationId
                WHERE p.ChargerType IS NULL
                  AND s.ChargerType IS NOT NULL
            "); // Why changed: preserve existing station-level charger type data on current poles.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargerType",
                table: "ChargingPoles");
        }
    }
}
