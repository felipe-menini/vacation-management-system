using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BalanceLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balance_accounts",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_bucket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balance_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_balance_accounts_balance_buckets_balance_bucket_id",
                        column: x => x.balance_bucket_id,
                        principalSchema: "licenses",
                        principalTable: "balance_buckets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_balance_accounts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "balance_ledger_entries",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    available_delta = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    reserved_delta = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balance_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_balance_ledger_entries_non_zero", "available_delta <> 0 OR reserved_delta <> 0");
                    table.CheckConstraint("ck_balance_ledger_entries_type", "type IN ('GRANT','RESERVE','RELEASE','CONSUME','REFUND','ADJUSTMENT','EXPIRE')");
                    table.CheckConstraint("ck_balance_ledger_entries_type_semantics", "(type = 'GRANT' AND available_delta > 0 AND reserved_delta = 0) OR (type = 'RESERVE' AND available_delta < 0 AND reserved_delta > 0 AND available_delta = -reserved_delta) OR (type = 'RELEASE' AND available_delta > 0 AND reserved_delta < 0 AND available_delta = -reserved_delta) OR (type = 'CONSUME' AND available_delta = 0 AND reserved_delta < 0) OR (type = 'REFUND' AND available_delta > 0 AND reserved_delta = 0) OR (type = 'ADJUSTMENT' AND available_delta <> 0 AND reserved_delta = 0) OR (type = 'EXPIRE' AND available_delta < 0 AND reserved_delta = 0)");
                    table.ForeignKey(
                        name: "FK_balance_ledger_entries_balance_accounts_balance_account_id",
                        column: x => x.balance_account_id,
                        principalSchema: "licenses",
                        principalTable: "balance_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_balance_ledger_entries_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_balance_accounts_balance_bucket_id",
                schema: "licenses",
                table: "balance_accounts",
                column: "balance_bucket_id");

            migrationBuilder.CreateIndex(
                name: "IX_balance_accounts_user_id_balance_bucket_id",
                schema: "licenses",
                table: "balance_accounts",
                columns: new[] { "user_id", "balance_bucket_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_balance_ledger_entries_balance_account_id_created_at_utc",
                schema: "licenses",
                table: "balance_ledger_entries",
                columns: new[] { "balance_account_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_balance_ledger_entries_created_by_user_id",
                schema: "licenses",
                table: "balance_ledger_entries",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_balance_ledger_entries_operation_id",
                schema: "licenses",
                table: "balance_ledger_entries",
                column: "operation_id",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION licenses.prevent_balance_ledger_entry_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'balance_ledger_entries are immutable';
                END;
                $$;

                CREATE TRIGGER trg_balance_ledger_entries_immutable
                BEFORE UPDATE OR DELETE ON licenses.balance_ledger_entries
                FOR EACH ROW EXECUTE FUNCTION licenses.prevent_balance_ledger_entry_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_balance_ledger_entries_immutable ON licenses.balance_ledger_entries;
                DROP FUNCTION IF EXISTS licenses.prevent_balance_ledger_entry_mutation();
                """);

            migrationBuilder.DropTable(
                name: "balance_ledger_entries",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "balance_accounts",
                schema: "licenses");
        }
    }
}
