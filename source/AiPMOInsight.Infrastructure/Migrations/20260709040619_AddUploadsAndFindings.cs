using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiPMOInsight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadsAndFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "refresh_tokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    citation_upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    citation_locator = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_findings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uploads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploads", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_findings_project_key",
                table: "findings",
                column: "project_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "findings");

            migrationBuilder.DropTable(
                name: "uploads");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "refresh_tokens");
        }
    }
}
