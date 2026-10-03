using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OFC.Infrastructure.Persistence;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OFCDbContext))]
    [Migration("20261003140000_CloseStaleKitchenOrders")]
    public partial class CloseStaleKitchenOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: companion to CloseStaleKitchenTickets. Orders still in a kitchen status from a previous
            // (Muscat) day are closed as Completed, with a history row. Payment (PaidAt) is left untouched.
            migrationBuilder.Sql("""
                WITH stale AS (
                    SELECT "Id", "Status" FROM ofc.orders
                    WHERE "Status" IN ('SentToKitchen', 'Preparing', 'Ready')
                      AND "CreatedAt" < (date_trunc('day', now() AT TIME ZONE 'Asia/Muscat') AT TIME ZONE 'Asia/Muscat')
                ), history AS (
                    INSERT INTO ofc.order_status_history ("Id", "OrderId", "FromStatus", "ToStatus", "ChangedByUserId", "ChangedAt", "Note")
                    SELECT gen_random_uuid(), "Id", "Status", 'Completed', NULL, now(), 'Closed by kitchen late-count reset'
                    FROM stale
                )
                UPDATE ofc.orders SET "Status" = 'Completed', "UpdatedAt" = now()
                WHERE "Id" IN (SELECT "Id" FROM stale);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
