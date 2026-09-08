using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterConfigurationHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastHealthError",
                schema: "ofc",
                table: "printer_configurations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenAt",
                schema: "ofc",
                table: "printer_configurations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHealthError",
                schema: "ofc",
                table: "printer_configurations");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                schema: "ofc",
                table: "printer_configurations");
        }
    }
}
