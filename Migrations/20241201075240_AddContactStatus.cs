using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatterBox.Migrations
{
    public partial class AddContactStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new Status column
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Contacts",
                type: "int",
                nullable: false,
                defaultValue: 1);  // Set existing contacts to Accepted (1)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Contacts");
        }
    }
}