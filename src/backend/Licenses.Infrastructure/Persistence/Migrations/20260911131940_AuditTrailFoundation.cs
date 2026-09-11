using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_events_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "licenses",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_events_users_subject_user_id",
                        column: x => x.subject_user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_actor_user_id_occurred_at_utc",
                schema: "licenses",
                table: "audit_events",
                columns: new[] { "actor_user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_occurred_at_utc",
                schema: "licenses",
                table: "audit_events",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_org_unit_id_occurred_at_utc",
                schema: "licenses",
                table: "audit_events",
                columns: new[] { "org_unit_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_resource_type_resource_id_occurred_at_utc",
                schema: "licenses",
                table: "audit_events",
                columns: new[] { "resource_type", "resource_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_subject_user_id_occurred_at_utc",
                schema: "licenses",
                table: "audit_events",
                columns: new[] { "subject_user_id", "occurred_at_utc" });

            migrationBuilder.Sql("""
                CREATE FUNCTION licenses.prevent_audit_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events are immutable';
                END;
                $$;

                CREATE TRIGGER trg_audit_events_immutable
                BEFORE UPDATE OR DELETE ON licenses.audit_events
                FOR EACH ROW EXECUTE FUNCTION licenses.prevent_audit_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_events_immutable ON licenses.audit_events;
                DROP FUNCTION IF EXISTS licenses.prevent_audit_event_mutation();
                """);

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "licenses");
        }
    }
}