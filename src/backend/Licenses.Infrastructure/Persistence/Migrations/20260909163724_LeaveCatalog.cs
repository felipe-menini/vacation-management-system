using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balance_buckets",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balance_buckets", x => x.id);
                    table.CheckConstraint("ck_balance_buckets_unit", "unit IN ('DAY')");
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.id);
                    table.CheckConstraint("ck_leave_types_sort_order_non_negative", "sort_order >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_balance_buckets_code",
                schema: "licenses",
                table: "balance_buckets",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_balance_buckets_is_active",
                schema: "licenses",
                table: "balance_buckets",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_code",
                schema: "licenses",
                table: "leave_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_is_active_sort_order",
                schema: "licenses",
                table: "leave_types",
                columns: new[] { "is_active", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_buckets",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "leave_types",
                schema: "licenses");
        }
    }
}
