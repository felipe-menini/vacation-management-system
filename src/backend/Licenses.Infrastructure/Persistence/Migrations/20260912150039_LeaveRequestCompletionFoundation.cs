using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestCompletionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at_utc",
                schema: "licenses",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_status_end_date_id",
                schema: "licenses",
                table: "leave_requests",
                columns: new[] { "status", "end_date", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leave_requests_status_end_date_id",
                schema: "licenses",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                schema: "licenses",
                table: "leave_requests");
        }
    }
}
