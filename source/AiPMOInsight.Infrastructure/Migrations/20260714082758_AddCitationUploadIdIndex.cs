using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiPMOInsight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCitationUploadIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_findings_citation_upload_id",
                table: "findings",
                column: "citation_upload_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_findings_citation_upload_id",
                table: "findings");
        }
    }
}
