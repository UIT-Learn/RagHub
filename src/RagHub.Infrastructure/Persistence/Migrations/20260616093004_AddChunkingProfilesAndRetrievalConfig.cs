using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RagHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkingProfilesAndRetrievalConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "run_at",
                table: "evaluations",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "retrieval_passed",
                table: "evaluations",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "citation_passed",
                table: "evaluations",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "actual_answer",
                table: "evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "evaluations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "expected_answer",
                table: "evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "reciprocal_rank",
                table: "evaluations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "chunk_size_used",
                table: "documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "chunking_profile_id",
                table: "documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "overlap_used",
                table: "documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chunking_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    strategy = table.Column<string>(type: "text", nullable: false),
                    max_chunk_size = table.Column<int>(type: "integer", nullable: false),
                    overlap = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chunking_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    candidate_k = table.Column<int>(type: "integer", nullable: false),
                    final_n = table.Column<int>(type: "integer", nullable: false),
                    use_hybrid = table.Column<bool>(type: "boolean", nullable: false),
                    use_reranker = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retrieval_configs", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "chunking_profiles",
                columns: new[] { "id", "created_at", "max_chunk_size", "name", "overlap", "strategy" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1500, "Default", 100, "auto" });

            migrationBuilder.InsertData(
                table: "retrieval_configs",
                columns: new[] { "id", "candidate_k", "final_n", "updated_at", "use_hybrid", "use_reranker" },
                values: new object[] { 1, 20, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true });

            migrationBuilder.CreateIndex(
                name: "ix_documents_chunking_profile_id",
                table: "documents",
                column: "chunking_profile_id");

            migrationBuilder.AddForeignKey(
                name: "fk_documents_chunking_profiles_chunking_profile_id",
                table: "documents",
                column: "chunking_profile_id",
                principalTable: "chunking_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_chunking_profiles_chunking_profile_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "chunking_profiles");

            migrationBuilder.DropTable(
                name: "retrieval_configs");

            migrationBuilder.DropIndex(
                name: "ix_documents_chunking_profile_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "actual_answer",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "expected_answer",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "reciprocal_rank",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "chunk_size_used",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "chunking_profile_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "overlap_used",
                table: "documents");

            migrationBuilder.AlterColumn<DateTime>(
                name: "run_at",
                table: "evaluations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "retrieval_passed",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "citation_passed",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
