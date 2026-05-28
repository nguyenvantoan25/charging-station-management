using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tramsac99.Migrations
{
    /// <inheritdoc />
    public partial class CreateChargingPoleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargingPoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    PoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxPower = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingPoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingPoles_ChargingStations_StationId",
                        column: x => x.StationId,
                        principalTable: "ChargingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingPoles_StationId_PoleCode",
                table: "ChargingPoles",
                columns: new[] { "StationId", "PoleCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingPoles");
        }
    }
}
