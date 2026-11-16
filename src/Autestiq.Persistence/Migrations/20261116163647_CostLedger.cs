using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CostLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost",
                table: "test_runs",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    published_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "run_costs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    per = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimate = table.Column<bool>(type: "boolean", nullable: false),
                    billable = table.Column<bool>(type: "boolean", nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_costs", x => x.id);
                    table.ForeignKey(
                        name: "fk_run_costs_price_versions_price_version_id",
                        column: x => x.price_version_id,
                        principalTable: "price_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unit_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,9)", precision: 18, scale: 9, nullable: false),
                    per = table.Column<long>(type: "bigint", nullable: false),
                    estimate = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit_prices", x => x.id);
                    table.CheckConstraint("ck_unit_prices_per_positive", "per > 0");
                    table.ForeignKey(
                        name: "fk_unit_prices_price_versions_price_version_id",
                        column: x => x.price_version_id,
                        principalTable: "price_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "price_versions",
                columns: new[] { "id", "created_at", "created_by", "currency", "effective_from", "note", "number", "published_by", "updated_at", "updated_by" },
                values: new object[] { new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "USD", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Machine and storage are estimates until hosting is chosen; model prices are Anthropic's list prices on 2026-11-16.", 1, "seed", new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.InsertData(
                table: "unit_prices",
                columns: new[] { "id", "created_at", "created_by", "estimate", "item", "per", "price", "price_version_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01a1e500-0000-7000-8000-000000000211"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "browser-second", 3600L, 0.10m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000212"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "storage-byte-month", 1073741824L, 0.023m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000213"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-input-token:claude-sonnet-5-5", 1000000L, 2.00m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000214"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-output-token:claude-sonnet-5-5", 1000000L, 10.00m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000215"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-input-token:claude-opus-5-5", 1000000L, 4.00m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000216"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-output-token:claude-opus-5-5", 1000000L, 20.00m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000217"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-input-token:claude-haiku-5-5", 1000000L, 0.10m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("01a1e500-0000-7000-8000-000000000218"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "model-output-token:claude-haiku-5-5", 1000000L, 0.50m, new Guid("01a1e500-0000-7000-8000-000000000201"), new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_versions_number",
                table: "price_versions",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_run_costs_organization_id_idempotency_key",
                table: "run_costs",
                columns: new[] { "organization_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_run_costs_organization_id_period",
                table: "run_costs",
                columns: new[] { "organization_id", "period" });

            migrationBuilder.CreateIndex(
                name: "ix_run_costs_price_version_id",
                table: "run_costs",
                column: "price_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_costs_run_id",
                table: "run_costs",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_unit_prices_price_version_id_item",
                table: "unit_prices",
                columns: new[] { "price_version_id", "item" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_costs");

            migrationBuilder.DropTable(
                name: "unit_prices");

            migrationBuilder.DropTable(
                name: "price_versions");

            migrationBuilder.DropColumn(
                name: "cost",
                table: "test_runs");
        }
    }
}
