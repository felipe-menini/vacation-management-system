using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkingCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "working_calendars",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_calendars", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "working_calendar_exceptions",
                schema: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    working_calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_working_day = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_calendar_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_working_calendar_exceptions_working_calendars_working_calen~",
                        column: x => x.working_calendar_id,
                        principalSchema: "licenses",
                        principalTable: "working_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "working_calendar_weekdays",
                schema: "licenses",
                columns: table => new
                {
                    working_calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    is_working_day = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_calendar_weekdays", x => new { x.working_calendar_id, x.day_of_week });
                    table.CheckConstraint("ck_working_calendar_weekdays_day_of_week", "day_of_week BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_working_calendar_weekdays_working_calendars_working_calenda~",
                        column: x => x.working_calendar_id,
                        principalSchema: "licenses",
                        principalTable: "working_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_policy_versions_working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions",
                column: "working_calendar_id");

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    compatibility_calendar_id uuid;
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM licenses.leave_policy_versions
                        WHERE working_calendar_id IS NULL
                          AND (day_count_mode = 'BUSINESS_DAYS' OR notice_day_count_mode = 'BUSINESS_DAYS')
                    ) THEN
                        SELECT id INTO compatibility_calendar_id
                        FROM licenses.working_calendars
                        WHERE code = 'MIGRATION_COMPATIBILITY_MON_FRI';

                        IF compatibility_calendar_id IS NULL THEN
                            compatibility_calendar_id := 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'::uuid;
                            INSERT INTO licenses.working_calendars
                                (id, code, name, description, is_active, created_at_utc, updated_at_utc)
                            VALUES
                                (compatibility_calendar_id, 'MIGRATION_COMPATIBILITY_MON_FRI', 'Migration Compatibility Monday-Friday Calendar', 'Compatibility calendar assigned only to pre-EP-05 business-day policy versions during migration. Replace with configured business calendars in ordinary administration.', true, now(), now());

                            INSERT INTO licenses.working_calendar_weekdays
                                (working_calendar_id, day_of_week, is_working_day)
                            VALUES
                                (compatibility_calendar_id, 0, false),
                                (compatibility_calendar_id, 1, true),
                                (compatibility_calendar_id, 2, true),
                                (compatibility_calendar_id, 3, true),
                                (compatibility_calendar_id, 4, true),
                                (compatibility_calendar_id, 5, true),
                                (compatibility_calendar_id, 6, false);
                        END IF;

                        UPDATE licenses.leave_policy_versions
                        SET working_calendar_id = compatibility_calendar_id
                        WHERE working_calendar_id IS NULL
                          AND (day_count_mode = 'BUSINESS_DAYS' OR notice_day_count_mode = 'BUSINESS_DAYS');
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_leave_policy_versions_calendar_required_for_business_days",
                schema: "licenses",
                table: "leave_policy_versions",
                sql: "((day_count_mode = 'BUSINESS_DAYS' OR notice_day_count_mode = 'BUSINESS_DAYS') AND working_calendar_id IS NOT NULL) OR (day_count_mode = 'CALENDAR_DAYS' AND notice_day_count_mode = 'CALENDAR_DAYS')");

            migrationBuilder.CreateIndex(
                name: "IX_working_calendar_exceptions_working_calendar_id_date",
                schema: "licenses",
                table: "working_calendar_exceptions",
                columns: new[] { "working_calendar_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_working_calendars_code",
                schema: "licenses",
                table: "working_calendars",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_leave_policy_versions_working_calendars_working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions",
                column: "working_calendar_id",
                principalSchema: "licenses",
                principalTable: "working_calendars",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leave_policy_versions_working_calendars_working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions");

            migrationBuilder.DropTable(
                name: "working_calendar_exceptions",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "working_calendar_weekdays",
                schema: "licenses");

            migrationBuilder.DropTable(
                name: "working_calendars",
                schema: "licenses");

            migrationBuilder.DropIndex(
                name: "IX_leave_policy_versions_working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_leave_policy_versions_calendar_required_for_business_days",
                schema: "licenses",
                table: "leave_policy_versions");

            migrationBuilder.DropColumn(
                name: "working_calendar_id",
                schema: "licenses",
                table: "leave_policy_versions");
        }
    }
}
