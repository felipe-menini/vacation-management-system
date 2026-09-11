using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at_utc",
                schema: "licenses",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "leave_request_decisions",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_settlement_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_request_decisions", x => x.id);
                    table.CheckConstraint("ck_leave_request_decisions_decision", "decision IN ('APPROVE','REJECT')");
                    table.CheckConstraint("ck_leave_request_decisions_reject_comment", "decision <> 'REJECT' OR (comment IS NOT NULL AND length(btrim(comment)) > 0)");
                    table.ForeignKey(
                        name: "FK_leave_request_decisions_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalSchema: "licenses",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_request_decisions_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_decisions_balance_settlement_operation_id",
                schema: "licenses",
                table: "leave_request_decisions",
                column: "balance_settlement_operation_id",
                unique: true,
                filter: "balance_settlement_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_decisions_decided_by_user_id",
                schema: "licenses",
                table: "leave_request_decisions",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_decisions_leave_request_id",
                schema: "licenses",
                table: "leave_request_decisions",
                column: "leave_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_decisions_operation_id",
                schema: "licenses",
                table: "leave_request_decisions",
                column: "operation_id",
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION licenses.reject_leave_request_decision_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'leave_request_decisions are immutable';
                END;
                $$;

                CREATE TRIGGER trg_leave_request_decisions_immutable
                BEFORE UPDATE OR DELETE ON licenses.leave_request_decisions
                FOR EACH ROW EXECUTE FUNCTION licenses.reject_leave_request_decision_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_leave_request_decisions_immutable ON licenses.leave_request_decisions;
                DROP FUNCTION IF EXISTS licenses.reject_leave_request_decision_mutation();
                """);

            migrationBuilder.DropTable(
                name: "leave_request_decisions",
                schema: "licenses");

            migrationBuilder.DropColumn(
                name: "decided_at_utc",
                schema: "licenses",
                table: "leave_requests");
        }
    }
}
