using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations;

public partial class InitialOrganization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "licenses");

        migrationBuilder.CreateTable(
            name: "org_units",
            schema: "licenses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_org_units", x => x.id);
                table.CheckConstraint("ck_org_units_not_own_parent", "parent_id IS NULL OR parent_id <> id");
                table.ForeignKey(
                    name: "fk_org_units_org_units_parent_id",
                    column: x => x.parent_id,
                    principalSchema: "licenses",
                    principalTable: "org_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "licenses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                external_identity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_org_assignments",
            schema: "licenses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_org_assignments", x => x.id);
                table.CheckConstraint("ck_user_org_assignments_valid_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
                table.ForeignKey(
                    name: "fk_user_org_assignments_org_units_org_unit_id",
                    column: x => x.org_unit_id,
                    principalSchema: "licenses",
                    principalTable: "org_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_user_org_assignments_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "licenses",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "ix_org_units_code", schema: "licenses", table: "org_units", column: "code", unique: true);
        migrationBuilder.CreateIndex(name: "ix_org_units_parent_id", schema: "licenses", table: "org_units", column: "parent_id");
        migrationBuilder.CreateIndex(name: "ix_users_email", schema: "licenses", table: "users", column: "email", unique: true);
        migrationBuilder.CreateIndex(name: "ix_users_external_identity_id", schema: "licenses", table: "users", column: "external_identity_id", unique: true, filter: "external_identity_id IS NOT NULL");
        migrationBuilder.CreateIndex(name: "ix_user_org_assignments_org_unit_id", schema: "licenses", table: "user_org_assignments", column: "org_unit_id");
        migrationBuilder.CreateIndex(name: "ix_user_org_assignments_user_id", schema: "licenses", table: "user_org_assignments", column: "user_id", unique: true, filter: "is_primary = true AND effective_to_utc IS NULL");
        migrationBuilder.CreateIndex(name: "ix_user_org_assignments_user_id_org_unit_id_effective_from_utc", schema: "licenses", table: "user_org_assignments", columns: new[] { "user_id", "org_unit_id", "effective_from_utc" }, unique: true);

        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        migrationBuilder.Sql("""
            ALTER TABLE licenses.user_org_assignments
            ADD CONSTRAINT ex_user_org_assignments_one_primary_period
            EXCLUDE USING gist (
                user_id WITH =,
                tstzrange(effective_from_utc, COALESCE(effective_to_utc, 'infinity'::timestamptz), '[)') WITH &&
            )
            WHERE (is_primary);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_org_assignments", schema: "licenses");
        migrationBuilder.DropTable(name: "org_units", schema: "licenses");
        migrationBuilder.DropTable(name: "users", schema: "licenses");
    }
}
