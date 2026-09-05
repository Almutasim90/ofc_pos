using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SprintNinePrintingLocalHardware : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_templates",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WidthChars = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_print_templates_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printer_configurations",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_printer_configurations_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "print_jobs",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrinterConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_print_jobs_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_print_jobs_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ofc",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_print_jobs_printer_configurations_PrinterConfigurationId",
                        column: x => x.PrinterConfigurationId,
                        principalSchema: "ofc",
                        principalTable: "printer_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_print_jobs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printer_routes",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationStationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrinterConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printer_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_printer_routes_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printer_routes_preparation_stations_PreparationStationId",
                        column: x => x.PreparationStationId,
                        principalSchema: "ofc",
                        principalTable: "preparation_stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printer_routes_print_templates_PrintTemplateId",
                        column: x => x.PrintTemplateId,
                        principalSchema: "ofc",
                        principalTable: "print_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printer_routes_printer_configurations_PrinterConfigurationId",
                        column: x => x.PrinterConfigurationId,
                        principalSchema: "ofc",
                        principalTable: "printer_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_BranchId_ClientRequestId_Kind",
                schema: "ofc",
                table: "print_jobs",
                columns: new[] { "BranchId", "ClientRequestId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_BranchId_Status_CreatedAt",
                schema: "ofc",
                table: "print_jobs",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_CreatedByUserId",
                schema: "ofc",
                table: "print_jobs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_OrderId",
                schema: "ofc",
                table: "print_jobs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_PrinterConfigurationId",
                schema: "ofc",
                table: "print_jobs",
                column: "PrinterConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_print_templates_BranchId_Code",
                schema: "ofc",
                table: "print_templates",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_print_templates_BranchId_Kind_IsActive",
                schema: "ofc",
                table: "print_templates",
                columns: new[] { "BranchId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_printer_configurations_BranchId_Code",
                schema: "ofc",
                table: "printer_configurations",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printer_configurations_BranchId_IsActive_SortOrder",
                schema: "ofc",
                table: "printer_configurations",
                columns: new[] { "BranchId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_printer_routes_BranchId_PreparationStationId_Priority",
                schema: "ofc",
                table: "printer_routes",
                columns: new[] { "BranchId", "PreparationStationId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_printer_routes_PreparationStationId",
                schema: "ofc",
                table: "printer_routes",
                column: "PreparationStationId");

            migrationBuilder.CreateIndex(
                name: "IX_printer_routes_PrinterConfigurationId",
                schema: "ofc",
                table: "printer_routes",
                column: "PrinterConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_printer_routes_PrintTemplateId",
                schema: "ofc",
                table: "printer_routes",
                column: "PrintTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_jobs",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "printer_routes",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "print_templates",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "printer_configurations",
                schema: "ofc");
        }
    }
}
