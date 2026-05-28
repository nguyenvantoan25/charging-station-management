using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tramsac99.Migrations
{
    public partial class UpgradeSupportAndNews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminReply",
                table: "SupportRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUserSeen",
                table: "SupportRequests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangedAt",
                table: "SupportRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UserSeenAt",
                table: "SupportRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE SupportRequests
                SET IsUserSeen = 1,
                    LastStatusChangedAt = CASE
                        WHEN ResolvedAt IS NOT NULL THEN ResolvedAt
                        WHEN ReadAt IS NOT NULL THEN ReadAt
                        ELSE CreatedAt
                    END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminReply",
                table: "SupportRequests");

            migrationBuilder.DropColumn(
                name: "IsUserSeen",
                table: "SupportRequests");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedAt",
                table: "SupportRequests");

            migrationBuilder.DropColumn(
                name: "UserSeenAt",
                table: "SupportRequests");
        }
    }
}
