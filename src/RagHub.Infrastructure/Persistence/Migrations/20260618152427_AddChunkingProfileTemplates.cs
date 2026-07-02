using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RagHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkingProfileTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "chunking_profiles",
                columns: new[] { "id", "created_at", "max_chunk_size", "name", "overlap", "strategy" },
                values: new object[,]
                {
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1200, "Legal / Contract", 150, "legal" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 800, "FAQ", 50, "faq" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1000, "Generic Fixed-size", 150, "fixed" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "chunking_profiles",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "chunking_profiles",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "chunking_profiles",
                keyColumn: "id",
                keyValue: 4);
        }
    }
}
