using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintFourteenProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "ofc",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    VatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "ofc",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipts_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "ofc",
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_goods_receipts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "ofc",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "ofc",
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "ofc",
                        principalTable: "goods_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_SupplierId",
                schema: "ofc",
                table: "inventory_movements",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_GoodsReceiptId_InventoryItemId",
                schema: "ofc",
                table: "goods_receipt_lines",
                columns: new[] { "GoodsReceiptId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_InventoryItemId",
                schema: "ofc",
                table: "goods_receipt_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_UnitId",
                schema: "ofc",
                table: "goods_receipt_lines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_BranchId_Number",
                schema: "ofc",
                table: "goods_receipts",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "goods_receipts",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_ClientReceiptId",
                schema: "ofc",
                table: "goods_receipts",
                column: "ClientReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_CreatedByUserId",
                schema: "ofc",
                table: "goods_receipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_PostedByUserId",
                schema: "ofc",
                table: "goods_receipts",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_PurchaseOrderId",
                schema: "ofc",
                table: "goods_receipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_SupplierId",
                schema: "ofc",
                table: "goods_receipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_InventoryItemId",
                schema: "ofc",
                table: "purchase_order_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_PurchaseOrderId_InventoryItemId",
                schema: "ofc",
                table: "purchase_order_lines",
                columns: new[] { "PurchaseOrderId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_UnitId",
                schema: "ofc",
                table: "purchase_order_lines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ApprovedByUserId",
                schema: "ofc",
                table: "purchase_orders",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_BranchId_Number",
                schema: "ofc",
                table: "purchase_orders",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "purchase_orders",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_CreatedByUserId",
                schema: "ofc",
                table: "purchase_orders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                schema: "ofc",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Code",
                schema: "ofc",
                table: "suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_IsActive_NameAr",
                schema: "ofc",
                table: "suppliers",
                columns: new[] { "IsActive", "NameAr" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_VatNumber",
                schema: "ofc",
                table: "suppliers",
                column: "VatNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_movements_purchase_orders_PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements",
                column: "PurchaseOrderId",
                principalSchema: "ofc",
                principalTable: "purchase_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_movements_suppliers_SupplierId",
                schema: "ofc",
                table: "inventory_movements",
                column: "SupplierId",
                principalSchema: "ofc",
                principalTable: "suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_movements_purchase_orders_PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_movements_suppliers_SupplierId",
                schema: "ofc",
                table: "inventory_movements");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "ofc");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_SupplierId",
                schema: "ofc",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                schema: "ofc",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "ofc",
                table: "inventory_movements");
        }
    }
}
