using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEvaluationVerifiedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verified_by",
                table: "evaluations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "verified_by",
                table: "evaluations",
                type: "text",
                nullable: true);
        }
    }
}
