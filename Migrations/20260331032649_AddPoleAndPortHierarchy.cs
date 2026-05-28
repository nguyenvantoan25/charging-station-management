using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tramsac99.Migrations
{
    /// <inheritdoc />
    public partial class AddPoleAndPortHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectorType",
                table: "ChargingPoles");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ChargingPoles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChargingPorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoleId = table.Column<int>(type: "int", nullable: false),
                    PortCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxPower = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingPorts_ChargingPoles_PoleId",
                        column: x => x.PoleId,
                        principalTable: "ChargingPoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingPorts_PoleId_PortCode",
                table: "ChargingPorts",
                columns: new[] { "PoleId", "PortCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingPorts");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ChargingPoles");

            migrationBuilder.AddColumn<string>(
                name: "ConnectorType",
                table: "ChargingPoles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
