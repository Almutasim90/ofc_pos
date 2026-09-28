using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaidAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                schema: "ofc",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Until now an order counted as a sale by status alone; keep every existing report unchanged.
            migrationBuilder.Sql("""
                UPDATE ofc.orders SET "PaidAt" = "UpdatedAt"
                WHERE "Status" IN ('Paid', 'SentToKitchen', 'Preparing', 'Ready', 'Completed', 'PartiallyRefunded', 'Refunded');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "ofc",
                table: "orders");
        }
    }
}
