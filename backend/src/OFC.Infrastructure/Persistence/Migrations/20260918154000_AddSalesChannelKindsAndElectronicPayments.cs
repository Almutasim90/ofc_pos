using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OFCDbContext))]
[Migration("20260918154000_AddSalesChannelKindsAndElectronicPayments")]
public sealed class AddSalesChannelKindsAndElectronicPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            schema: "ofc",
            table: "sales_channels",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "InStore");

        migrationBuilder.Sql("""
            UPDATE ofc.sales_channels
            SET "Kind" = CASE UPPER("Code")
                WHEN 'DINEIN' THEN 'DineIn'
                WHEN 'TAKEAWAY' THEN 'Takeaway'
                WHEN 'WEBQR' THEN 'Qr'
                WHEN 'POS' THEN 'InStore'
                ELSE 'Electronic'
            END;

            INSERT INTO ofc.sales_channels ("Id", "Code", "NameAr", "NameEn", "Kind", "IsActive", "CreatedAt")
            VALUES
                (gen_random_uuid(), 'TALABAT', 'طلبات', 'Talabat', 'Electronic', true, now()),
                (gen_random_uuid(), 'TMDONE', 'تم دن', 'TM Done', 'Electronic', true, now()),
                (gen_random_uuid(), 'KHIDMA', 'خدمة', 'Khedmah', 'Electronic', true, now())
            ON CONFLICT ("Code") DO UPDATE SET
                "Kind" = EXCLUDED."Kind",
                "NameAr" = EXCLUDED."NameAr",
                "NameEn" = EXCLUDED."NameEn";

            INSERT INTO ofc.payment_methods
                ("Id", "BranchId", "Code", "NameAr", "NameEn", "Kind", "IsActive", "SortOrder", "CreatedAt")
            SELECT gen_random_uuid(), b."Id", 'EXTERNAL', 'دفع خارجي', 'External payment', 'External', true, 30, now()
            FROM ofc.branches b
            WHERE b."IsActive"
            ON CONFLICT ("BranchId", "Code") DO UPDATE SET
                "NameAr" = EXCLUDED."NameAr",
                "NameEn" = EXCLUDED."NameEn",
                "Kind" = EXCLUDED."Kind",
                "IsActive" = true;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Provider channels and payment methods may already be referenced by real orders. Retain those
        // business records during rollback and remove only the schema field introduced by this migration.
        migrationBuilder.DropColumn(
            name: "Kind",
            schema: "ofc",
            table: "sales_channels");
    }
}
