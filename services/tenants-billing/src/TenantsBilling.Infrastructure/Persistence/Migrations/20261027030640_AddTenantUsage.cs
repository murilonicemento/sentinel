using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantsBilling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recorded_usage_events",
                columns: table => new
                {
                    SourceEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recorded_usage_events", x => x.SourceEventId);
                });

            migrationBuilder.CreateTable(
                name: "tenant_usage",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EventsConsumed = table.Column<long>(type: "bigint", nullable: false),
                    AlertsTriggered = table.Column<long>(type: "bigint", nullable: false),
                    ApiRequests = table.Column<long>(type: "bigint", nullable: false),
                    ChannelUsage = table.Column<long>(type: "bigint", nullable: false),
                    QuotaStatus = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_usage", x => new { x.TenantId, x.Period });
                });

            migrationBuilder.CreateIndex(
                name: "IX_recorded_usage_events_TenantId_Period",
                table: "recorded_usage_events",
                columns: new[] { "TenantId", "Period" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recorded_usage_events");

            migrationBuilder.DropTable(
                name: "tenant_usage");
        }
    }
}
