using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SprintElevenInventoryUomBom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recipe_versions",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_versions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DescriptionAr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    StockOnHand = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_items_units_of_measure_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unit_conversions",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(19,9)", precision: 19, scale: 9, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_unit_conversions_units_of_measure_FromUnitId",
                        column: x => x.FromUnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unit_conversions_units_of_measure_ToUnitId",
                        column: x => x.ToUnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_recipe_versions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalSchema: "ofc",
                        principalTable: "recipe_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_movements_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recipe_lines_recipe_versions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalSchema: "ofc",
                        principalTable: "recipe_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_lines_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Barcode",
                schema: "ofc",
                table: "inventory_items",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_BaseUnitId",
                schema: "ofc",
                table: "inventory_items",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Sku",
                schema: "ofc",
                table: "inventory_items",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_Type",
                schema: "ofc",
                table: "inventory_items",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_BranchId_InventoryItemId_OccurredAt",
                schema: "ofc",
                table: "inventory_movements",
                columns: new[] { "BranchId", "InventoryItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_BranchId_Type_OccurredAt",
                schema: "ofc",
                table: "inventory_movements",
                columns: new[] { "BranchId", "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_CreatedByUserId",
                schema: "ofc",
                table: "inventory_movements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_InventoryItemId",
                schema: "ofc",
                table: "inventory_movements",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_RecipeVersionId",
                schema: "ofc",
                table: "inventory_movements",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_UnitId",
                schema: "ofc",
                table: "inventory_movements",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_lines_InventoryItemId",
                schema: "ofc",
                table: "recipe_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_lines_RecipeVersionId_InventoryItemId",
                schema: "ofc",
                table: "recipe_lines",
                columns: new[] { "RecipeVersionId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_lines_UnitId",
                schema: "ofc",
                table: "recipe_lines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_versions_CreatedByUserId",
                schema: "ofc",
                table: "recipe_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_versions_ProductId_Status",
                schema: "ofc",
                table: "recipe_versions",
                columns: new[] { "ProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_versions_ProductId_VersionNumber",
                schema: "ofc",
                table: "recipe_versions",
                columns: new[] { "ProductId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unit_conversions_FromUnitId_ToUnitId",
                schema: "ofc",
                table: "unit_conversions",
                columns: new[] { "FromUnitId", "ToUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unit_conversions_ToUnitId",
                schema: "ofc",
                table: "unit_conversions",
                column: "ToUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_Code",
                schema: "ofc",
                table: "units_of_measure",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_IsActive_SortOrder",
                schema: "ofc",
                table: "units_of_measure",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_movements",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "recipe_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "unit_conversions",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "recipe_versions",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "units_of_measure",
                schema: "ofc");
        }
    }
}
