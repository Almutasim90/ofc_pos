using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_layouts",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeaderText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HeaderImage = table.Column<string>(type: "text", nullable: true),
                    FooterText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FooterImage = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_layouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receipt_layouts_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_layouts_BranchId",
                schema: "ofc",
                table: "receipt_layouts",
                column: "BranchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_layouts",
                schema: "ofc");
        }
    }
}
