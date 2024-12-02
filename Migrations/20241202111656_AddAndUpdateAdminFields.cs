using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChatterBox.Migrations
{
    public partial class AddAndUpdateAdminFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns
            migrationBuilder.AddColumn<string>(
                name: "CurrentAdminId",
                table: "Groups",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAdminTransferDate",
                table: "Groups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasAdmin",
                table: "GroupMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BecameAdminAt",
                table: "GroupMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedBeingAdminAt",
                table: "GroupMembers",
                type: "datetime2",
                nullable: true);

            // Update existing groups to set CurrentAdminId = CreatedById
            migrationBuilder.Sql(@"
                UPDATE Groups
                SET CurrentAdminId = CreatedById,
                    LastAdminTransferDate = CreatedAt
                WHERE CurrentAdminId IS NULL
            ");

            // Update existing group members to set proper roles
            migrationBuilder.Sql(@"
                UPDATE gm
                SET Role = CASE WHEN g.CreatedById = gm.UserId THEN 'Admin' ELSE 'Member' END,
                    BecameAdminAt = CASE WHEN g.CreatedById = gm.UserId THEN g.CreatedAt ELSE NULL END
                FROM GroupMembers gm
                JOIN Groups g ON g.GroupId = gm.GroupId
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_CurrentAdminId",
                table: "Groups",
                column: "CurrentAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_AspNetUsers_CurrentAdminId",
                table: "Groups",
                column: "CurrentAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_AspNetUsers_CurrentAdminId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_CurrentAdminId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "CurrentAdminId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "LastAdminTransferDate",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "WasAdmin",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "BecameAdminAt",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "StoppedBeingAdminAt",
                table: "GroupMembers");
        }
    }
}