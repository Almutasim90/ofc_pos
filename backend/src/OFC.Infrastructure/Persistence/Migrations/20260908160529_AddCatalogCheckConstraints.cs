using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_selection_groups_min_max",
                schema: "ofc",
                table: "selection_groups",
                sql: "\"MinSelections\" >= 0 AND \"MaxSelections\" >= 1 AND \"MinSelections\" <= \"MaxSelections\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_base_price_non_negative",
                schema: "ofc",
                table: "products",
                sql: "\"BasePrice\" IS NULL OR \"BasePrice\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_selection_groups_min_max",
                schema: "ofc",
                table: "selection_groups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_base_price_non_negative",
                schema: "ofc",
                table: "products");
        }
    }
}
