using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "plan",
                table: "organizations");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_version_id",
                table: "organizations",
                type: "uuid",
                nullable: false,
                // Every organization that exists today was on "trial", which is the Developer plan's first version.
                defaultValue: new Guid("01a1e500-0000-7000-8000-000000000101"));

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    monthly_browser_minutes = table.Column<long>(type: "bigint", nullable: false),
                    concurrent_runs = table.Column<int>(type: "integer", nullable: false),
                    max_projects = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_versions_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "plans",
                columns: new[] { "id", "code", "created_at", "created_by", "name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01a1e500-0000-7000-8000-000000000001"), "developer", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Developer", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000002"), "starter", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Starter", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000003"), "growth", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Growth", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000004"), "scale", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Scale", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.InsertData(
                table: "plan_versions",
                columns: new[] { "id", "concurrent_runs", "created_at", "created_by", "effective_from", "max_projects", "monthly_browser_minutes", "number", "plan_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01a1e500-0000-7000-8000-000000000101"), 1, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 100L, 1, new Guid("01a1e500-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000102"), 2, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 500L, 1, new Guid("01a1e500-0000-7000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000103"), 5, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10, 2500L, 1, new Guid("01a1e500-0000-7000-8000-000000000003"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000104"), 10, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 10000L, 1, new Guid("01a1e500-0000-7000-8000-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_organizations_plan_version_id",
                table: "organizations",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_versions_plan_id_number",
                table: "plan_versions",
                columns: new[] { "plan_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_code",
                table: "plans",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_organizations_plan_versions_plan_version_id",
                table: "organizations",
                column: "plan_version_id",
                principalTable: "plan_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_organizations_plan_versions_plan_version_id",
                table: "organizations");

            migrationBuilder.DropTable(
                name: "plan_versions");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropIndex(
                name: "ix_organizations_plan_version_id",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "plan_version_id",
                table: "organizations");

            migrationBuilder.AddColumn<string>(
                name: "plan",
                table: "organizations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "trial");
        }
    }
}
