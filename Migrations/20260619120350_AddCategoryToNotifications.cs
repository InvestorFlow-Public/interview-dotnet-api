using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace interview_dotnet_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Notifications",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");
        }
    }
}
