using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanningAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_test_cases_organization_id_id",
                table: "test_cases",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "planning_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ceiling_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_attempts", x => x.id);
                    table.UniqueConstraint("ak_planning_attempts_organization_id_id", x => new { x.organization_id, x.id });
                    table.ForeignKey(
                        name: "fk_planning_attempts_test_cases_organization_id_test_case_id",
                        columns: x => new { x.organization_id, x.test_case_id },
                        principalTable: "test_cases",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planning_attempt_outcomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    end = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tokens = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_attempt_outcomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_planning_attempt_outcomes_planning_attempts_organization_id~",
                        columns: x => new { x.organization_id, x.attempt_id },
                        principalTable: "planning_attempts",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_planning_attempt_outcomes_attempt_id",
                table: "planning_attempt_outcomes",
                column: "attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planning_attempt_outcomes_organization_id_attempt_id",
                table: "planning_attempt_outcomes",
                columns: new[] { "organization_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_planning_attempts_created_at",
                table: "planning_attempts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_planning_attempts_organization_id_test_case_id",
                table: "planning_attempts",
                columns: new[] { "organization_id", "test_case_id" });

            // What was spent before attempts were recorded stays spent: one answered attempt per planned test, at the
            // cost it recorded, dated when it was planned. A re-plan before this migration overwrote the earlier cost,
            // so that part of past spend is unknown and not invented here.
            migrationBuilder.Sql("""
                WITH backfilled AS (
                    INSERT INTO planning_attempts (id, organization_id, test_case_id, model, ceiling_usd, created_at, updated_at)
                    SELECT gen_random_uuid(), organization_id, id, COALESCE(model, 'unknown'), model_cost, planned_at, planned_at
                    FROM test_cases
                    WHERE model_cost IS NOT NULL AND planned_at IS NOT NULL
                    RETURNING id, organization_id, test_case_id, ceiling_usd, created_at
                )
                INSERT INTO planning_attempt_outcomes (id, organization_id, attempt_id, "end", cost_usd, tokens, created_at, updated_at)
                SELECT gen_random_uuid(), backfilled.organization_id, backfilled.id, 'Answered', backfilled.ceiling_usd, test_cases.model_tokens, backfilled.created_at, backfilled.created_at
                FROM backfilled
                JOIN test_cases ON test_cases.id = backfilled.test_case_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planning_attempt_outcomes");

            migrationBuilder.DropTable(
                name: "planning_attempts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_test_cases_organization_id_id",
                table: "test_cases");
        }
    }
}
