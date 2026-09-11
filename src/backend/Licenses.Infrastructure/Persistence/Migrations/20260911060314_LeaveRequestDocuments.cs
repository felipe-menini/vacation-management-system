using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_request_documents",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_request_documents", x => x.id);
                    table.CheckConstraint("ck_leave_request_documents_kind", "kind IN ('MEDICAL_CERTIFICATE')");
                    table.CheckConstraint("ck_leave_request_documents_sha256_hex", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_leave_request_documents_size_positive", "size_bytes > 0");
                    table.ForeignKey(
                        name: "FK_leave_request_documents_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalSchema: "licenses",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_request_documents_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_documents_leave_request_id_created_at_utc",
                schema: "licenses",
                table: "leave_request_documents",
                columns: new[] { "leave_request_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_documents_sha256",
                schema: "licenses",
                table: "leave_request_documents",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_documents_storage_key",
                schema: "licenses",
                table: "leave_request_documents",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_documents_uploaded_by_user_id",
                schema: "licenses",
                table: "leave_request_documents",
                column: "uploaded_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_request_documents",
                schema: "licenses");
        }
    }
}
