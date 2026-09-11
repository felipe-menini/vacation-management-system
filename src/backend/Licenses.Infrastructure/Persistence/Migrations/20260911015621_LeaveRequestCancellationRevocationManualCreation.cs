using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestCancellationRevocationManualCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_decided_at_utc",
                schema: "licenses",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_requested_at_utc",
                schema: "licenses",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at_utc",
                schema: "licenses",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_operation_id",
                schema: "licenses",
                table: "leave_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "leave_request_cancellations",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decision_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    balance_settlement_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_request_cancellations", x => x.id);
                    table.CheckConstraint("ck_leave_request_cancellations_decision", "decision IS NULL OR decision IN ('APPROVE','REJECT')");
                    table.CheckConstraint("ck_leave_request_cancellations_decision_consistency", "(decision IS NULL AND decided_by_user_id IS NULL AND decision_operation_id IS NULL AND decided_at_utc IS NULL) OR (decision IS NOT NULL AND decided_by_user_id IS NOT NULL AND decision_operation_id IS NOT NULL AND decided_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_leave_request_cancellations_reason", "length(btrim(reason)) > 0");
                    table.CheckConstraint("ck_leave_request_cancellations_reject_comment", "decision <> 'REJECT' OR (decision_comment IS NOT NULL AND length(btrim(decision_comment)) > 0)");
                    table.ForeignKey(
                        name: "FK_leave_request_cancellations_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalSchema: "licenses",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_request_cancellations_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_request_cancellations_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_request_revocations",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_settlement_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_request_revocations", x => x.id);
                    table.CheckConstraint("ck_leave_request_revocations_reason", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "FK_leave_request_revocations_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalSchema: "licenses",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_request_revocations_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_balance_settlement_operation_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "balance_settlement_operation_id",
                unique: true,
                filter: "balance_settlement_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_submission_operation_id",
                schema: "licenses",
                table: "leave_requests",
                column: "submission_operation_id",
                unique: true,
                filter: "submission_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_decided_by_user_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_decision_operation_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "decision_operation_id",
                unique: true,
                filter: "decision_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_leave_request_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "leave_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_operation_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_cancellations_requested_by_user_id",
                schema: "licenses",
                table: "leave_request_cancellations",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_revocations_balance_settlement_operation_id",
                schema: "licenses",
                table: "leave_request_revocations",
                column: "balance_settlement_operation_id",
                unique: true,
                filter: "balance_settlement_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_revocations_leave_request_id",
                schema: "licenses",
                table: "leave_request_revocations",
                column: "leave_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_revocations_operation_id",
                schema: "licenses",
                table: "leave_request_revocations",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_request_revocations_revoked_by_user_id",
                schema: "licenses",
                table: "leave_request_revocations",
                column: "revoked_by_user_id");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION licenses.reject_leave_request_cancellation_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'leave_request_cancellations are immutable';
                    END IF;

                    IF OLD.decision IS NULL
                       AND NEW.id = OLD.id
                       AND NEW.leave_request_id = OLD.leave_request_id
                       AND NEW.requested_by_user_id = OLD.requested_by_user_id
                       AND NEW.reason = OLD.reason
                       AND NEW.operation_id = OLD.operation_id
                       AND NEW.requested_at_utc = OLD.requested_at_utc
                       AND NEW.decision IS NOT NULL
                       AND NEW.decided_by_user_id IS NOT NULL
                       AND NEW.decision_operation_id IS NOT NULL
                       AND NEW.decided_at_utc IS NOT NULL THEN
                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'leave_request_cancellations are immutable after decision';
                END;
                $$;

                CREATE TRIGGER trg_leave_request_cancellations_immutable
                BEFORE UPDATE OR DELETE ON licenses.leave_request_cancellations
                FOR EACH ROW EXECUTE FUNCTION licenses.reject_leave_request_cancellation_mutation();

                CREATE OR REPLACE FUNCTION licenses.reject_leave_request_revocation_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'leave_request_revocations are immutable';
                END;
                $$;

                CREATE TRIGGER trg_leave_request_revocations_immutable
                BEFORE UPDATE OR DELETE ON licenses.leave_request_revocations
                FOR EACH ROW EXECUTE FUNCTION licenses.reject_leave_request_revocation_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_leave_request_cancellations_immutable ON licenses.leave_request_cancellations;
                DROP FUNCTION IF EXISTS licenses.reject_leave_request_cancellation_mutation();
                DROP TRIGGER IF EXISTS trg_leave_request_revocations_immutable ON licenses.leave_request_revocations;
                DROP FUNCTION IF EXISTS licenses.reject_leave_request_revocation_mutation();
                """);

            migrationBuilder.DropTable(
                name: "leave_request_cancellations",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "leave_request_revocations",
                schema: "licenses");

            migrationBuilder.DropIndex(
                name: "IX_leave_requests_submission_operation_id",
                schema: "licenses",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "submission_operation_id",
                schema: "licenses",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_decided_at_utc",
                schema: "licenses",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at_utc",
                schema: "licenses",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "revoked_at_utc",
                schema: "licenses",
                table: "leave_requests");
        }
    }
}
