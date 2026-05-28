using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tramsac99.Data;

#nullable disable

namespace tramsac99.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260402090000_AddUpdatedAtToStationReview")]
    public partial class AddUpdatedAtToStationReview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StationReviews",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StationReviews");
        }
    }
}
