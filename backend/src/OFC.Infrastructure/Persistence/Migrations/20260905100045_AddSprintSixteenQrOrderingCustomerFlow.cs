using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintSixteenQrOrderingCustomerFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "ofc",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "ofc",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedByUserId",
                schema: "ofc",
                table: "order_status_history",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LoyaltyReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsWalkIn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qr_contexts",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ApprovalMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qr_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qr_contexts_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qr_contexts_sales_channels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "ofc",
                        principalTable: "sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qr_order_approvals",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qr_order_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qr_order_approvals_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "ofc",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_qr_order_approvals_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qr_order_approvals_qr_contexts_QrContextId",
                        column: x => x.QrContextId,
                        principalSchema: "ofc",
                        principalTable: "qr_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qr_order_approvals_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId",
                schema: "ofc",
                table: "orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_ExternalId",
                schema: "ofc",
                table: "customers",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_LoyaltyReference",
                schema: "ofc",
                table: "customers",
                column: "LoyaltyReference");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Phone",
                schema: "ofc",
                table: "customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_qr_contexts_BranchId_Code",
                schema: "ofc",
                table: "qr_contexts",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qr_contexts_BranchId_IsActive_Kind",
                schema: "ofc",
                table: "qr_contexts",
                columns: new[] { "BranchId", "IsActive", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_qr_contexts_SalesChannelId",
                schema: "ofc",
                table: "qr_contexts",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_qr_order_approvals_CustomerId",
                schema: "ofc",
                table: "qr_order_approvals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_qr_order_approvals_OrderId",
                schema: "ofc",
                table: "qr_order_approvals",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qr_order_approvals_QrContextId_Status_CreatedAt",
                schema: "ofc",
                table: "qr_order_approvals",
                columns: new[] { "QrContextId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_qr_order_approvals_ReviewedByUserId",
                schema: "ofc",
                table: "qr_order_approvals",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_qr_order_approvals_Status_CreatedAt",
                schema: "ofc",
                table: "qr_order_approvals",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_CustomerId",
                schema: "ofc",
                table: "orders",
                column: "CustomerId",
                principalSchema: "ofc",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_CustomerId",
                schema: "ofc",
                table: "orders");

            migrationBuilder.DropTable(
                name: "qr_order_approvals",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "qr_contexts",
                schema: "ofc");

            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId",
                schema: "ofc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "ofc",
                table: "orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "ofc",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedByUserId",
                schema: "ofc",
                table: "order_status_history",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
