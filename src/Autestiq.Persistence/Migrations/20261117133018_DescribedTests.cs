using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DescribedTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "test_case_id",
                table: "test_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ai_processing_allowed_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ai_processing_allowed_by",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "test_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    snapshot = table.Column<string>(type: "text", nullable: true),
                    plan = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    sent_to_model = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_tokens = table.Column<int>(type: "integer", nullable: true),
                    model_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    planning_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_test_cases_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_test_cases_projects_organization_id_project_id",
                        columns: x => new { x.organization_id, x.project_id },
                        principalTable: "projects",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_test_case_id",
                table: "test_runs",
                column: "test_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_environment_id",
                table: "test_cases",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_organization_id_planned_at",
                table: "test_cases",
                columns: new[] { "organization_id", "planned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_organization_id_project_id_number",
                table: "test_cases",
                columns: new[] { "organization_id", "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_status_planning_started_at",
                table: "test_cases",
                columns: new[] { "status", "planning_started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "test_cases");

            migrationBuilder.DropIndex(
                name: "ix_test_runs_test_case_id",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "test_case_id",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "ai_processing_allowed_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ai_processing_allowed_by",
                table: "projects");
        }
    }
}
