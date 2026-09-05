using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintTwelveOfflineSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_operations",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    BaseVersion = table.Column<long>(type: "bigint", nullable: true),
                    BaseCatalogVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "jsonb", nullable: true),
                    ConflictReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: true),
                    ClientOccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_operations_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sync_operations_pos_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "ofc",
                        principalTable: "pos_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sync_operations_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sync_states",
                schema: "ofc",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_states", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_sync_states_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_operations_BranchId_DeviceId_IdempotencyKey",
                schema: "ofc",
                table: "sync_operations",
                columns: new[] { "BranchId", "DeviceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_operations_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "sync_operations",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_operations_CreatedByUserId",
                schema: "ofc",
                table: "sync_operations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_operations_DeviceId_CreatedAt",
                schema: "ofc",
                table: "sync_operations",
                columns: new[] { "DeviceId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_operations",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "sync_states",
                schema: "ofc");
        }
    }
}
