using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTableNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TableNumber",
                schema: "ofc",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_BranchId_TableNumber",
                schema: "ofc",
                table: "orders",
                columns: new[] { "BranchId", "TableNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_BranchId_TableNumber",
                schema: "ofc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TableNumber",
                schema: "ofc",
                table: "orders");
        }
    }
}
