using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OFC.Infrastructure.Persistence;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OFCDbContext))]
    [Migration("20261003120000_RenameOmanNetToCard")]
    public partial class RenameOmanNetToCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: legacy "عمان نت" payment methods are shown everywhere as "بطاقة" / "Card".
            migrationBuilder.Sql("""
                UPDATE ofc.payment_methods SET "NameAr" = 'بطاقة', "NameEn" = 'Card'
                WHERE replace("NameAr", ' ', '') = 'عماننت'
                   OR lower(replace("NameEn", ' ', '')) = 'omannet'
                   OR upper("Code") = 'OMANNET';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
