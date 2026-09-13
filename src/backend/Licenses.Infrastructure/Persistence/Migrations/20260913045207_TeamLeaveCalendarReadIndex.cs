using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamLeaveCalendarReadIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_org_unit_id_status_start_date_end_date",
                schema: "licenses",
                table: "leave_requests",
                columns: new[] { "org_unit_id", "status", "start_date", "end_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leave_requests_org_unit_id_status_start_date_end_date",
                schema: "licenses",
                table: "leave_requests");
        }
    }
}
