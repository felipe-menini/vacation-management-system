using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationRoleScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "licenses",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "licenses",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "licenses",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_scope_assignments",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    include_descendants = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_scope_assignments", x => x.id);
                    table.CheckConstraint("ck_role_scope_assignments_valid_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
                    table.ForeignKey(
                        name: "FK_role_scope_assignments_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "licenses",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_scope_assignments_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "licenses",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_scope_assignments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "licenses",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_code",
                schema: "licenses",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "licenses",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_assignments_org_unit_id",
                schema: "licenses",
                table: "role_scope_assignments",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_assignments_role_id",
                schema: "licenses",
                table: "role_scope_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_assignments_user_id",
                schema: "licenses",
                table: "role_scope_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_assignments_user_id_role_id_org_unit_id_effectiv~",
                schema: "licenses",
                table: "role_scope_assignments",
                columns: new[] { "user_id", "role_id", "org_unit_id", "effective_from_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_code",
                schema: "licenses",
                table: "roles",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "role_scope_assignments",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "licenses");
        }
    }
}
