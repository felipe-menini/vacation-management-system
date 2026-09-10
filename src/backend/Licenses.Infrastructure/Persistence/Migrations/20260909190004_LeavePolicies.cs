using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeavePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_policies",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applies_to_descendants = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_policies", x => x.id);
                    table.CheckConstraint("ck_leave_policies_global_descendants_false", "org_unit_id IS NOT NULL OR applies_to_descendants = false");
                    table.ForeignKey(
                        name: "FK_leave_policies_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalSchema: "licenses",
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_policies_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "licenses",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_policy_versions",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    day_count_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    allow_half_day = table.Column<bool>(type: "boolean", nullable: false),
                    minimum_notice_days = table.Column<int>(type: "integer", nullable: true),
                    notice_day_count_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    maximum_request_days = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    overlap_behavior = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    consumes_balance = table.Column<bool>(type: "boolean", nullable: false),
                    balance_bucket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_policy_versions", x => x.id);
                    table.CheckConstraint("ck_leave_policy_versions_balance_consistency", "(consumes_balance = true AND balance_bucket_id IS NOT NULL) OR (consumes_balance = false AND balance_bucket_id IS NULL)");
                    table.CheckConstraint("ck_leave_policy_versions_day_count_mode", "day_count_mode IN ('BUSINESS_DAYS','CALENDAR_DAYS')");
                    table.CheckConstraint("ck_leave_policy_versions_effective_range", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_leave_policy_versions_max_request_positive", "maximum_request_days IS NULL OR maximum_request_days > 0");
                    table.CheckConstraint("ck_leave_policy_versions_min_notice_non_negative", "minimum_notice_days IS NULL OR minimum_notice_days >= 0");
                    table.CheckConstraint("ck_leave_policy_versions_notice_day_count_mode", "notice_day_count_mode IN ('BUSINESS_DAYS','CALENDAR_DAYS')");
                    table.CheckConstraint("ck_leave_policy_versions_overlap_behavior", "overlap_behavior IN ('BLOCK','WARN','ALLOW')");
                    table.CheckConstraint("ck_leave_policy_versions_published_at", "(status = 'PUBLISHED' AND published_at_utc IS NOT NULL) OR (status = 'DRAFT' AND published_at_utc IS NULL)");
                    table.CheckConstraint("ck_leave_policy_versions_status", "status IN ('DRAFT','PUBLISHED')");
                    table.CheckConstraint("ck_leave_policy_versions_version_positive", "version_number > 0");
                    table.ForeignKey(
                        name: "FK_leave_policy_versions_balance_buckets_balance_bucket_id",
                        column: x => x.balance_bucket_id,
                        principalSchema: "licenses",
                        principalTable: "balance_buckets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_policy_versions_leave_policies_leave_policy_id",
                        column: x => x.leave_policy_id,
                        principalSchema: "licenses",
                        principalTable: "leave_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_policies_leave_type_id",
                schema: "licenses",
                table: "leave_policies",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_policies_leave_type_id_org_unit_id",
                schema: "licenses",
                table: "leave_policies",
                columns: new[] { "leave_type_id", "org_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_policies_org_unit_id",
                schema: "licenses",
                table: "leave_policies",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_policy_versions_balance_bucket_id",
                schema: "licenses",
                table: "leave_policy_versions",
                column: "balance_bucket_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_policy_versions_leave_policy_id_status_effective_from~",
                schema: "licenses",
                table: "leave_policy_versions",
                columns: new[] { "leave_policy_id", "status", "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_policy_versions_leave_policy_id_version_number",
                schema: "licenses",
                table: "leave_policy_versions",
                columns: new[] { "leave_policy_id", "version_number" },
                unique: true);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_leave_policies_company_scope
                ON licenses.leave_policies (leave_type_id)
                WHERE org_unit_id IS NULL;
                """);
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_leave_policies_org_scope
                ON licenses.leave_policies (leave_type_id, org_unit_id)
                WHERE org_unit_id IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE licenses.leave_policy_versions
                ADD CONSTRAINT ex_leave_policy_versions_no_published_overlap
                EXCLUDE USING gist (
                    leave_policy_id WITH =,
                    daterange(effective_from, COALESCE(effective_to, 'infinity'::date), '[]') WITH &&
                )
                WHERE (status = 'PUBLISHED');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE licenses.leave_policy_versions DROP CONSTRAINT IF EXISTS ex_leave_policy_versions_no_published_overlap;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS licenses.ux_leave_policies_org_scope;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS licenses.ux_leave_policies_company_scope;");

            migrationBuilder.DropTable(
                name: "leave_policy_versions",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "leave_policies",
                schema: "licenses");
        }
    }
}
