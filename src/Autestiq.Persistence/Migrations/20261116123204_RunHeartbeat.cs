using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "heartbeat_at",
                table: "test_runs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "heartbeat_at",
                table: "test_runs");
        }
    }
}
