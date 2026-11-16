using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OriginProofs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "origin_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    check_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    check_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_origin_proofs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_origin_proofs_organization_id_origin",
                table: "origin_proofs",
                columns: new[] { "organization_id", "origin" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_origin_proofs_status_check_requested_at",
                table: "origin_proofs",
                columns: new[] { "status", "check_requested_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "origin_proofs");
        }
    }
}
