using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autestiq.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "evidence_retention_days",
                table: "projects",
                type: "integer",
                nullable: false,

                // Projects that exist keep what every project kept until now, at most 90 days (docs/domain-model.md §7).
                // Set here only, not as the column's default in the model: EF leaves out a value equal to the CLR
                // default, so a model default of 90 would turn a chosen 0 ("keep none") into 90.
                defaultValue: 90);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evidence_retention_days",
                table: "projects");
        }
    }
}
