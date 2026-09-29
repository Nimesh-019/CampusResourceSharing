using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusResourceSharing.Migrations
{
    /// <inheritdoc />
    public partial class AddItemApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Items");
        }
    }
}
