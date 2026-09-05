using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintFourPricingChannelsTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_versions",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_versions_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalSchema: "ofc",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_channels",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rules",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CalculationMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_rules_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_rules_tax_categories_TaxCategoryId",
                        column: x => x.TaxCategoryId,
                        principalSchema: "ofc",
                        principalTable: "tax_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_rules",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_rules_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_rules_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_price_rules_sales_channels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "ofc",
                        principalTable: "sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                schema: "ofc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesChannelId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promotions_branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ofc",
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ofc",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promotions_sales_channels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "ofc",
                        principalTable: "sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_versions_Number",
                schema: "ofc",
                table: "catalog_versions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_versions_PublishedByUserId",
                schema: "ofc",
                table: "catalog_versions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_price_rules_BranchId",
                schema: "ofc",
                table: "price_rules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_price_rules_ProductId_BranchId_SalesChannelId_EffectiveFrom",
                schema: "ofc",
                table: "price_rules",
                columns: new[] { "ProductId", "BranchId", "SalesChannelId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_price_rules_SalesChannelId",
                schema: "ofc",
                table: "price_rules",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_BranchId",
                schema: "ofc",
                table: "promotions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_Code",
                schema: "ofc",
                table: "promotions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotions_ProductId_BranchId_SalesChannelId_EffectiveFrom",
                schema: "ofc",
                table: "promotions",
                columns: new[] { "ProductId", "BranchId", "SalesChannelId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_promotions_SalesChannelId",
                schema: "ofc",
                table: "promotions",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_channels_Code",
                schema: "ofc",
                table: "sales_channels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_BranchId",
                schema: "ofc",
                table: "tax_rules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_TaxCategoryId_BranchId_EffectiveFrom",
                schema: "ofc",
                table: "tax_rules",
                columns: new[] { "TaxCategoryId", "BranchId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_versions",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "price_rules",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "promotions",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "tax_rules",
                schema: "ofc");

            migrationBuilder.DropTable(
                name: "sales_channels",
                schema: "ofc");
        }
    }
}
