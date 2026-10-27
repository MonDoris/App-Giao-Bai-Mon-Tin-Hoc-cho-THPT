using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinHoc12.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartAtUtc",
                table: "Assignments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartAtUtc",
                table: "Assignments");
        }
    }
}
