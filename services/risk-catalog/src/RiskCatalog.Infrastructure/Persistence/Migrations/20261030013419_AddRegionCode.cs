using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RC-14, in three steps so existing rows survive the unique index: add the column empty, give every
            // existing region the legacy code derived from its id, then require it.
            migrationBuilder.AddColumn<string>(
                name: "region_code",
                table: "regional_parameter",
                type: "varchar(64)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE regional_parameter SET region_code = 'legacy-' || replace(id::text, '-', '') WHERE region_code IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "region_code",
                table: "regional_parameter",
                type: "varchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_regional_parameter_region_code",
                table: "regional_parameter",
                column: "region_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_regional_parameter_region_code",
                table: "regional_parameter");

            migrationBuilder.DropColumn(
                name: "region_code",
                table: "regional_parameter");
        }
    }
}
