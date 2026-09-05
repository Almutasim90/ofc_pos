using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintSevenCancellationVoidRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_PaymentId",
                schema: "ofc",
                table: "financial_transactions");

            migrationBuilder.AddColumn<int>(
                name: "VoidedQuantity",
                schema: "ofc",
                table: "order_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "cancellation_approval_thresholds",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cancellation_approval_thresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cancellation_approval_thresholds_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cancellation_reasons",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequiresNote = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cancellation_reasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cancellation_reasons_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_cancellations",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancellationReasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    OrderStatusAtCancellation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WasSentToKitchen = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnInventory = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_cancellations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_cancellations_cancellation_reasons_CancellationReason~",
                        column: x => x.CancellationReasonId,
                        principalSchema: "ofc",
                        principalTable: "cancellation_reasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellations_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellations_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellations_users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_line_voids",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancellationReasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line_voids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_line_voids_cancellation_reasons_CancellationReasonId",
                        column: x => x.CancellationReasonId,
                        principalSchema: "ofc",
                        principalTable: "cancellation_reasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_line_voids_order_lines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "ofc",
                        principalTable: "order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_line_voids_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_line_voids_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_line_voids_users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancellationReasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReturnInventory = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refunds_cancellation_reasons_CancellationReasonId",
                        column: x => x.CancellationReasonId,
                        principalSchema: "ofc",
                        principalTable: "cancellation_reasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "ofc",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_users_RefundedByUserId",
                        column: x => x.RefundedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PaymentId",
                schema: "ofc",
                table: "financial_transactions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_cancellation_approval_thresholds_BranchId_Operation",
                schema: "ofc",
                table: "cancellation_approval_thresholds",
                columns: new[] { "BranchId", "Operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cancellation_reasons_BranchId_Code",
                schema: "ofc",
                table: "cancellation_reasons",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cancellation_reasons_BranchId_IsActive_SortOrder",
                schema: "ofc",
                table: "cancellation_reasons",
                columns: new[] { "BranchId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_ApprovedByUserId",
                schema: "ofc",
                table: "order_cancellations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_BranchId_CancelledAt",
                schema: "ofc",
                table: "order_cancellations",
                columns: new[] { "BranchId", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_CancellationReasonId",
                schema: "ofc",
                table: "order_cancellations",
                column: "CancellationReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_CancelledByUserId",
                schema: "ofc",
                table: "order_cancellations",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_OrderId",
                schema: "ofc",
                table: "order_cancellations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_line_voids_ApprovedByUserId",
                schema: "ofc",
                table: "order_line_voids",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_voids_CancellationReasonId",
                schema: "ofc",
                table: "order_line_voids",
                column: "CancellationReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_voids_OrderId_VoidedAt",
                schema: "ofc",
                table: "order_line_voids",
                columns: new[] { "OrderId", "VoidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_order_line_voids_OrderLineId",
                schema: "ofc",
                table: "order_line_voids",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_voids_VoidedByUserId",
                schema: "ofc",
                table: "order_line_voids",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_ApprovedByUserId",
                schema: "ofc",
                table: "refunds",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_BranchId_RefundedAt",
                schema: "ofc",
                table: "refunds",
                columns: new[] { "BranchId", "RefundedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refunds_CancellationReasonId",
                schema: "ofc",
                table: "refunds",
                column: "CancellationReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId_ClientRequestId",
                schema: "ofc",
                table: "refunds",
                columns: new[] { "OrderId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_PaymentId_RefundedAt",
                schema: "ofc",
                table: "refunds",
                columns: new[] { "PaymentId", "RefundedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refunds_RefundedByUserId",
                schema: "ofc",
                table: "refunds",
                column: "RefundedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cancellation_approval_thresholds",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "order_cancellations",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "order_line_voids",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "cancellation_reasons",
                schema: "ofc");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_PaymentId",
                schema: "ofc",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "VoidedQuantity",
                schema: "ofc",
                table: "order_lines");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PaymentId",
                schema: "ofc",
                table: "financial_transactions",
                column: "PaymentId",
                unique: true);
        }
    }
}
