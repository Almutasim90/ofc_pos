using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SprintTenKdsKitchenContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kitchen_tickets",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DispatchStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetMinutes = table.Column<int>(type: "integer", nullable: true),
                    KdsAttempts = table.Column<int>(type: "integer", nullable: false),
                    FallbackPrinted = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WasPrepStartedBeforeCancellation = table.Column<bool>(type: "boolean", nullable: false),
                    CancellationNotified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FallbackPrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kitchen_tickets_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kitchen_tickets_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kitchen_tickets_preparation_stations_StationId",
                        column: x => x.StationId,
                        principalSchema: "ofc",
                        principalTable: "preparation_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kitchen_tickets_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kitchen_ticket_items",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KitchenTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductNameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProductNameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    VoidedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SelectionsSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kitchen_ticket_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kitchen_ticket_items_kitchen_tickets_KitchenTicketId",
                        column: x => x.KitchenTicketId,
                        principalSchema: "ofc",
                        principalTable: "kitchen_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kitchen_ticket_items_order_lines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "ofc",
                        principalTable: "order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kitchen_ticket_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_ticket_items_KitchenTicketId",
                schema: "ofc",
                table: "kitchen_ticket_items",
                column: "KitchenTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_ticket_items_OrderLineId",
                schema: "ofc",
                table: "kitchen_ticket_items",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_ticket_items_ProductId",
                schema: "ofc",
                table: "kitchen_ticket_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_BranchId_DispatchId",
                schema: "ofc",
                table: "kitchen_tickets",
                columns: new[] { "BranchId", "DispatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_BranchId_OrderId_StationId",
                schema: "ofc",
                table: "kitchen_tickets",
                columns: new[] { "BranchId", "OrderId", "StationId" });

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_BranchId_StationId_DispatchStatus_CreatedAt",
                schema: "ofc",
                table: "kitchen_tickets",
                columns: new[] { "BranchId", "StationId", "DispatchStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_CreatedByUserId",
                schema: "ofc",
                table: "kitchen_tickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_OrderId",
                schema: "ofc",
                table: "kitchen_tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_StationId",
                schema: "ofc",
                table: "kitchen_tickets",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kitchen_ticket_items",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "kitchen_tickets",
                schema: "ofc");
        }
    }
}
