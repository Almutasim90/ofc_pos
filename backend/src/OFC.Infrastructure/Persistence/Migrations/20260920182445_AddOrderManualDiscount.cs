using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderManualDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountAmount",
                schema: "ofc",
                table: "orders",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // The "orders.discount" permission did not exist at bootstrap for already-provisioned
            // organizations (IdentityService only seeds Permissions/Roles once, at org creation), so it
            // has to be inserted here to become assignable through the existing roles/permissions admin
            // UI. Admin implicitly owns every permission by definition (see IdentityService.DefaultRoles),
            // so it is granted here too to keep that invariant true; Branch Manager/Cashier are
            // deliberately left out — the discount is meant to require explicit, per-organization sign-off
            // on who gets it, granted via that same admin UI.
            migrationBuilder.Sql("""
                INSERT INTO ofc.permissions ("Id", "Code", "Name")
                VALUES (gen_random_uuid(), 'orders.discount', 'orders.discount')
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO ofc.role_permissions ("RoleId", "PermissionId")
                SELECT r."Id", p."Id"
                FROM ofc.roles r, ofc.permissions p
                WHERE r."Name" = 'Admin' AND p."Code" = 'orders.discount'
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The permission may already be assigned to real roles/users and referenced by real orders;
            // retain it on rollback and remove only the schema column this migration introduced (mirrors
            // the same rollback policy used for the Talabat/TM Done/Khedmah channel seed migration).
            migrationBuilder.DropColumn(
                name: "ManualDiscountAmount",
                schema: "ofc",
                table: "orders");
        }
    }
}
