using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiQuerySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "multi_query_count",
                table: "retrieval_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "use_multi_query",
                table: "retrieval_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "retrieval_configs",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "multi_query_count", "use_multi_query" },
                values: new object[] { 3, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "multi_query_count",
                table: "retrieval_configs");

            migrationBuilder.DropColumn(
                name: "use_multi_query",
                table: "retrieval_configs");
        }
    }
}
