using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tramsac99.Migrations
{
    public partial class AddInitialPoleChargerTypeToStationRegistrationRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitialPoleChargerType",
                table: "StationRegistrationRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialPoleChargerType",
                table: "StationRegistrationRequests");
        }
    }
}

