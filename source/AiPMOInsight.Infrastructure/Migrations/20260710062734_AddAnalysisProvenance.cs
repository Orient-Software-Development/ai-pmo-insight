using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiPMOInsight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "citation_structured_excerpt",
                table: "findings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citation_text_snippet",
                table: "findings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confidence",
                table: "findings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "findings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "producing_agent",
                table: "findings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "findings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "run_id",
                table: "findings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_findings_run_id",
                table: "findings",
                column: "run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_findings_run_id",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "citation_structured_excerpt",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "citation_text_snippet",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "confidence",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "producing_agent",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "run_id",
                table: "findings");
        }
    }
}
