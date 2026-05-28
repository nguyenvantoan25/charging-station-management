using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tramsac99.Data;

#nullable disable

namespace tramsac99.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260402170000_AddAvatarUrlToAppUser")]
    public partial class AddAvatarUrlToAppUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "AppUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "AppUsers");
        }
    }
}
