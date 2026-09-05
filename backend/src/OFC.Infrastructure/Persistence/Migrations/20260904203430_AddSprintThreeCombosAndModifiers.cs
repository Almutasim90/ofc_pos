using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintThreeCombosAndModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "selection_groups",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinSelections = table.Column<int>(type: "integer", nullable: false),
                    MaxSelections = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_selection_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_selection_groups",
                schema: "ofc",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_selection_groups", x => new { x.ProductId, x.SelectionGroupId });
                    table.ForeignKey(
                        name: "FK_product_selection_groups_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_selection_groups_selection_groups_SelectionGroupId",
                        column: x => x.SelectionGroupId,
                        principalSchema: "ofc",
                        principalTable: "selection_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "selection_group_branch_availability",
                schema: "ofc",
                columns: table => new
                {
                    SelectionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_selection_group_branch_availability", x => new { x.SelectionGroupId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_selection_group_branch_availability_selection_groups_Select~",
                        column: x => x.SelectionGroupId,
                        principalSchema: "ofc",
                        principalTable: "selection_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "selection_options",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceAdjustment = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_selection_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_selection_options_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_selection_options_selection_groups_SelectionGroupId",
                        column: x => x.SelectionGroupId,
                        principalSchema: "ofc",
                        principalTable: "selection_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_selection_groups_SelectionGroupId",
                schema: "ofc",
                table: "product_selection_groups",
                column: "SelectionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_selection_group_branch_availability_BranchId",
                schema: "ofc",
                table: "selection_group_branch_availability",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_selection_groups_Kind_IsActive",
                schema: "ofc",
                table: "selection_groups",
                columns: new[] { "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_selection_options_ProductId",
                schema: "ofc",
                table: "selection_options",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_selection_options_SelectionGroupId_ProductId",
                schema: "ofc",
                table: "selection_options",
                columns: new[] { "SelectionGroupId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_selection_groups",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "selection_group_branch_availability",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "selection_options",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "selection_groups",
                schema: "ofc");
        }
    }
}
