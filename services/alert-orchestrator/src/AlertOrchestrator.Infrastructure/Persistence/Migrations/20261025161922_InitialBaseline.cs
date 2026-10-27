using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlertOrchestrator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    risk_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    final_risk_score = table.Column<double>(type: "double precision", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    close_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    current_escalation_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Level1"),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_windows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_score = table.Column<double>(type: "double precision", nullable: false),
                    risk_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    alert_window_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_signals", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_signals_alert_windows_alert_window_id",
                        column: x => x.alert_window_id,
                        principalTable: "alert_windows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "alert_signals_event_id_key",
                table: "alert_signals",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_signals_alert_window_id",
                table: "alert_signals",
                column: "alert_window_id");

            migrationBuilder.CreateIndex(
                name: "idx_alert_windows_confirmed_at",
                table: "alert_windows",
                column: "confirmed_at",
                filter: "confirmed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_alert_windows_expires_at",
                table: "alert_windows",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_alert_windows_region_risk_status",
                table: "alert_windows",
                columns: new[] { "region", "risk_type", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_alert_windows_status",
                table: "alert_windows",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_alert_windows_tenant_id",
                table: "alert_windows",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_signals");

            migrationBuilder.DropTable(
                name: "alert_windows");
        }
    }
}
