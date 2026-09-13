using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EP17RequiredDocumentKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "required_document_kind",
                schema: "licenses",
                table: "leave_policy_versions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_leave_policy_versions_required_document_kind",
                schema: "licenses",
                table: "leave_policy_versions",
                sql: "required_document_kind IS NULL OR required_document_kind IN ('MEDICAL_CERTIFICATE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_leave_policy_versions_required_document_kind",
                schema: "licenses",
                table: "leave_policy_versions");

            migrationBuilder.DropColumn(
                name: "required_document_kind",
                schema: "licenses",
                table: "leave_policy_versions");
        }
    }
}
