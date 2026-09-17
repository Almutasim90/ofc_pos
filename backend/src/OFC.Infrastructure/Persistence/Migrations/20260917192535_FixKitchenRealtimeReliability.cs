using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixKitchenRealtimeReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kitchen_tickets_BranchId_DispatchId",
                schema: "ofc",
                table: "kitchen_tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "ofc",
                table: "print_jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_BranchId_DispatchId_StationId",
                schema: "ofc",
                table: "kitchen_tickets",
                columns: new[] { "BranchId", "DispatchId", "StationId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kitchen_tickets_BranchId_DispatchId_StationId",
                schema: "ofc",
                table: "kitchen_tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "ofc",
                table: "print_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_kitchen_tickets_BranchId_DispatchId",
                schema: "ofc",
                table: "kitchen_tickets",
                columns: new[] { "BranchId", "DispatchId" },
                unique: true);
        }
    }
}
