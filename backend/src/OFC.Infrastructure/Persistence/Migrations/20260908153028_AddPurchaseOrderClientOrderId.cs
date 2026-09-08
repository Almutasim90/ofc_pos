using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderClientOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientOrderId",
                schema: "ofc",
                table: "purchase_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ClientOrderId",
                schema: "ofc",
                table: "purchase_orders",
                column: "ClientOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_ClientOrderId",
                schema: "ofc",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                schema: "ofc",
                table: "purchase_orders");
        }
    }
}
