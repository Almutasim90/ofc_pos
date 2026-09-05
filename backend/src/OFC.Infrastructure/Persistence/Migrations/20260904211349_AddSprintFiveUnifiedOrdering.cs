using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintFiveUnifiedOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_sales_channels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "ofc",
                        principalTable: "sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductNameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProductNameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SelectionsSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    UnitListAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitDiscountAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitNetAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitTaxAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    UnitGrossAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxCalculationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PriceRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogVersionNumber = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_lines_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_lines_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_status_history_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_status_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_OrderId",
                schema: "ofc",
                table: "order_lines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_ProductId",
                schema: "ofc",
                table: "order_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_ChangedByUserId",
                schema: "ofc",
                table: "order_status_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_OrderId_ChangedAt",
                schema: "ofc",
                table: "order_status_history",
                columns: new[] { "OrderId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_BranchId_ClientRequestId",
                schema: "ofc",
                table: "orders",
                columns: new[] { "BranchId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "orders",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedByUserId",
                schema: "ofc",
                table: "orders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_SalesChannelId",
                schema: "ofc",
                table: "orders",
                column: "SalesChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "ofc");
        }
    }
}
