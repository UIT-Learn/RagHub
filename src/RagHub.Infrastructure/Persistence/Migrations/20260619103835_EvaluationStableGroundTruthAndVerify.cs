using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvaluationStableGroundTruthAndVerify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "expected_chunk_ids",
                table: "evaluations",
                newName: "expected_sources");

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                table: "evaluations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verified_by",
                table: "evaluations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "verified_by",
                table: "evaluations");

            migrationBuilder.RenameColumn(
                name: "expected_sources",
                table: "evaluations",
                newName: "expected_chunk_ids");
        }
    }
}
