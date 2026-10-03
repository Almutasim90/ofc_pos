using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OFC.Infrastructure.Persistence;

#nullable disable

namespace OFC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OFCDbContext))]
    [Migration("20261003130000_CloseStaleKitchenTickets")]
    public partial class CloseStaleKitchenTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: one-off reset of the kitchen's late count. Tickets left open from a previous
            // (Muscat) day are closed as Completed. CompletedAt stays null so they don't skew prep-time averages.
            migrationBuilder.Sql("""
                WITH stale AS (
                    SELECT "Id" FROM ofc.kitchen_tickets
                    WHERE "Status" IN ('New', 'Preparing', 'Ready')
                      AND "CreatedAt" < (date_trunc('day', now() AT TIME ZONE 'Asia/Muscat') AT TIME ZONE 'Asia/Muscat')
                ), items AS (
                    UPDATE ofc.kitchen_ticket_items SET "Status" = 'Completed'
                    WHERE "KitchenTicketId" IN (SELECT "Id" FROM stale) AND "Status" IN ('New', 'Preparing', 'Ready')
                )
                UPDATE ofc.kitchen_tickets SET "Status" = 'Completed', "UpdatedAt" = now()
                WHERE "Id" IN (SELECT "Id" FROM stale);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
