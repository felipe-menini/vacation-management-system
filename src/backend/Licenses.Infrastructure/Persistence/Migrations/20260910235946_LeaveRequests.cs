using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_requests",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_policy_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    day_portion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    calculated_days = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    balance_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    balance_reservation_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.id);
                    table.CheckConstraint("ck_leave_requests_balance_link_consistency", "(balance_account_id IS NULL AND balance_reservation_operation_id IS NULL) OR (balance_account_id IS NOT NULL AND balance_reservation_operation_id IS NOT NULL)");
                    table.CheckConstraint("ck_leave_requests_calculated_positive", "calculated_days IS NULL OR calculated_days > 0");
                    table.CheckConstraint("ck_leave_requests_date_range", "end_date >= start_date");
                    table.CheckConstraint("ck_leave_requests_day_portion", "day_portion IN ('FULL_DAY','HALF_DAY')");
                    table.CheckConstraint("ck_leave_requests_half_day_single_date", "day_portion <> 'HALF_DAY' OR start_date = end_date");
                    table.CheckConstraint("ck_leave_requests_status", "status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','CANCELLATION_REQUESTED','CANCELLED','REVOKED','COMPLETED')");
                    table.CheckConstraint("ck_leave_requests_submission_consistency", "(status = 'DRAFT' AND leave_policy_version_id IS NULL AND calculated_days IS NULL AND submitted_at_utc IS NULL AND balance_account_id IS NULL) OR (status <> 'DRAFT' AND leave_policy_version_id IS NOT NULL AND calculated_days IS NOT NULL AND submitted_at_utc IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_leave_requests_balance_accounts_balance_account_id",
                        column: x => x.balance_account_id,
                        principalSchema: "licenses",
                        principalTable: "balance_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_leave_policy_versions_leave_policy_version_id",
                        column: x => x.leave_policy_version_id,
                        principalSchema: "licenses",
                        principalTable: "leave_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalSchema: "licenses",
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "licenses",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_balance_account_id",
                schema: "licenses",
                table: "leave_requests",
                column: "balance_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_balance_reservation_operation_id",
                schema: "licenses",
                table: "leave_requests",
                column: "balance_reservation_operation_id",
                unique: true,
                filter: "balance_reservation_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_created_by_user_id",
                schema: "licenses",
                table: "leave_requests",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_leave_policy_version_id",
                schema: "licenses",
                table: "leave_requests",
                column: "leave_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_leave_type_id",
                schema: "licenses",
                table: "leave_requests",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_org_unit_id_status",
                schema: "licenses",
                table: "leave_requests",
                columns: new[] { "org_unit_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_user_id_start_date_end_date_status",
                schema: "licenses",
                table: "leave_requests",
                columns: new[] { "user_id", "start_date", "end_date", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_requests",
                schema: "licenses");
        }
    }
}
