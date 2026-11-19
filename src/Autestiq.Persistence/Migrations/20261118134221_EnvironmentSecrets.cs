using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnvironmentSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "environment_secrets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ciphertext = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_environment_secrets", x => x.id);
                    table.ForeignKey(
                        name: "fk_environment_secrets_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_environment_secrets_environment_id",
                table: "environment_secrets",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "ix_environment_secrets_organization_id_environment_id_name",
                table: "environment_secrets",
                columns: new[] { "organization_id", "environment_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "environment_secrets");
        }
    }
}
