using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Testo.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestRunsAndArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "test_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    scenario_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_origin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    rerun_of = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_requested = table.Column<bool>(type: "boolean", nullable: false),
                    bytes_downloaded = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_runs", x => x.id);
                    table.UniqueConstraint("ak_test_runs_organization_id_id", x => new { x.organization_id, x.id });
                    table.ForeignKey(
                        name: "fk_test_runs_projects_organization_id_project_id",
                        columns: x => new { x.organization_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artifacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_artifacts_test_runs_organization_id_test_run_id",
                        columns: x => new { x.organization_id, x.test_run_id },
                        principalTable: "test_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "test_run_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    screenshot_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_run_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_test_run_steps_test_runs_organization_id_test_run_id",
                        columns: x => new { x.organization_id, x.test_run_id },
                        principalTable: "test_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artifacts_organization_id_captured_at",
                table: "artifacts",
                columns: new[] { "organization_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "ix_artifacts_organization_id_test_run_id",
                table: "artifacts",
                columns: new[] { "organization_id", "test_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_test_run_steps_organization_id_test_run_id_order",
                table: "test_run_steps",
                columns: new[] { "organization_id", "test_run_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_organization_id_project_id_number",
                table: "test_runs",
                columns: new[] { "organization_id", "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_organization_id_project_id_queued_at",
                table: "test_runs",
                columns: new[] { "organization_id", "project_id", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_organization_id_status",
                table: "test_runs",
                columns: new[] { "organization_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifacts");

            migrationBuilder.DropTable(
                name: "test_run_steps");

            migrationBuilder.DropTable(
                name: "test_runs");
        }
    }
}
