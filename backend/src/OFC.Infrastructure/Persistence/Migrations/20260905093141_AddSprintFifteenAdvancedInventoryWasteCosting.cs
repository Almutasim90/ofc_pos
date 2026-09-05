using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintFifteenAdvancedInventoryWasteCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_counts",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_counts_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_counts_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_counts_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_counts_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_DestinationBranchId",
                        column: x => x.DestinationBranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_SourceBranchId",
                        column: x => x.SourceBranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "waste_records",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waste_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_waste_records_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waste_records_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waste_records_inventory_movements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalSchema: "ofc",
                        principalTable: "inventory_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_records_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_records_pos_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "ofc",
                        principalTable: "pos_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_records_shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "ofc",
                        principalTable: "shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_records_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waste_records_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Variance = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_inventory_counts_CountId",
                        column: x => x.CountId,
                        principalSchema: "ofc",
                        principalTable: "inventory_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "ofc",
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalSchema: "ofc",
                        principalTable: "stock_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_units_of_measure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "ofc",
                        principalTable: "units_of_measure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_CountId_InventoryItemId",
                schema: "ofc",
                table: "inventory_count_lines",
                columns: new[] { "CountId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_InventoryItemId",
                schema: "ofc",
                table: "inventory_count_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_UnitId",
                schema: "ofc",
                table: "inventory_count_lines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_ApprovedByUserId",
                schema: "ofc",
                table: "inventory_counts",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_BranchId_Number",
                schema: "ofc",
                table: "inventory_counts",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "inventory_counts",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_ClientCountId",
                schema: "ofc",
                table: "inventory_counts",
                column: "ClientCountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_CreatedByUserId",
                schema: "ofc",
                table: "inventory_counts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_PostedByUserId",
                schema: "ofc",
                table: "inventory_counts",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_InventoryItemId",
                schema: "ofc",
                table: "stock_transfer_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_StockTransferId_InventoryItemId",
                schema: "ofc",
                table: "stock_transfer_lines",
                columns: new[] { "StockTransferId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_UnitId",
                schema: "ofc",
                table: "stock_transfer_lines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ClientTransferId",
                schema: "ofc",
                table: "stock_transfers",
                column: "ClientTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_CreatedByUserId",
                schema: "ofc",
                table: "stock_transfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_DestinationBranchId_Status_CreatedAt",
                schema: "ofc",
                table: "stock_transfers",
                columns: new[] { "DestinationBranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ReceivedByUserId",
                schema: "ofc",
                table: "stock_transfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_SourceBranchId_Number",
                schema: "ofc",
                table: "stock_transfers",
                columns: new[] { "SourceBranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_BranchId_Number",
                schema: "ofc",
                table: "waste_records",
                columns: new[] { "BranchId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_BranchId_OccurredAt",
                schema: "ofc",
                table: "waste_records",
                columns: new[] { "BranchId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_ClientRecordId",
                schema: "ofc",
                table: "waste_records",
                column: "ClientRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_CreatedByUserId",
                schema: "ofc",
                table: "waste_records",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_DeviceId",
                schema: "ofc",
                table: "waste_records",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_InventoryItemId",
                schema: "ofc",
                table: "waste_records",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_InventoryMovementId",
                schema: "ofc",
                table: "waste_records",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_OrderId",
                schema: "ofc",
                table: "waste_records",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_ShiftId",
                schema: "ofc",
                table: "waste_records",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_records_UnitId",
                schema: "ofc",
                table: "waste_records",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_count_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "waste_records",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "inventory_counts",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "stock_transfers",
                schema: "ofc");
        }
    }
}
