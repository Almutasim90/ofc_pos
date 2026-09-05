using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintEightShiftsCashControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                schema: "ofc",
                table: "order_cancellations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                schema: "ofc",
                table: "financial_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shifts",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashSales = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CardSales = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CashRefunds = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CardRefunds = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CashInTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CashOutTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    PettyCashTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CashDropsTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CardExpectedTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualCash = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    ActualCardTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CashVariance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    CardVariance = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shifts_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shifts_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shifts_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shifts_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_denominations",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Denomination = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_denominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_denominations_shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "ofc",
                        principalTable: "shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_movements",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_movements_shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "ofc",
                        principalTable: "shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shift_movements_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellations_ShiftId",
                schema: "ofc",
                table: "order_cancellations",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_BranchId_ShiftId_CreatedAt",
                schema: "ofc",
                table: "financial_transactions",
                columns: new[] { "BranchId", "ShiftId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ShiftId",
                schema: "ofc",
                table: "financial_transactions",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_denominations_ShiftId_Denomination",
                schema: "ofc",
                table: "shift_denominations",
                columns: new[] { "ShiftId", "Denomination" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_movements_CreatedByUserId",
                schema: "ofc",
                table: "shift_movements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_movements_ShiftId_CreatedAt",
                schema: "ofc",
                table: "shift_movements",
                columns: new[] { "ShiftId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shifts_BranchId_Status_OpenedAt",
                schema: "ofc",
                table: "shifts",
                columns: new[] { "BranchId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shifts_ClosedByUserId",
                schema: "ofc",
                table: "shifts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_OpenedByUserId",
                schema: "ofc",
                table: "shifts",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_ReviewedByUserId",
                schema: "ofc",
                table: "shifts",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_financial_transactions_shifts_ShiftId",
                schema: "ofc",
                table: "financial_transactions",
                column: "ShiftId",
                principalSchema: "ofc",
                principalTable: "shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_order_cancellations_shifts_ShiftId",
                schema: "ofc",
                table: "order_cancellations",
                column: "ShiftId",
                principalSchema: "ofc",
                principalTable: "shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_financial_transactions_shifts_ShiftId",
                schema: "ofc",
                table: "financial_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_order_cancellations_shifts_ShiftId",
                schema: "ofc",
                table: "order_cancellations");

            migrationBuilder.DropTable(
                name: "shift_denominations",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "shift_movements",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "shifts",
                schema: "ofc");

            migrationBuilder.DropIndex(
                name: "IX_order_cancellations_ShiftId",
                schema: "ofc",
                table: "order_cancellations");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_BranchId_ShiftId_CreatedAt",
                schema: "ofc",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_ShiftId",
                schema: "ofc",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                schema: "ofc",
                table: "order_cancellations");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                schema: "ofc",
                table: "financial_transactions");
        }
    }
}
