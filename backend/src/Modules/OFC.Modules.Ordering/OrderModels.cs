using OFC.Modules.Catalog;

namespace OFC.Modules.Ordering;

public enum OrderSource { Pos = 0, Qr = 1, Delivery = 2, Aggregator = 3 }
public enum OrderStatus { Draft = 0, Pending = 1, Confirmed = 2, Paid = 3, SentToKitchen = 4, Preparing = 5, Ready = 6, Completed = 7, Cancelled = 8, Rejected = 9, PartiallyRefunded = 10, Refunded = 11 }

public sealed class Order
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid SalesChannelId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid ClientRequestId { get; set; }
    public OrderSource Source { get; set; } = OrderSource.Pos;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string? Note { get; set; }
    // Free-text table label so staff can look an order up by table at the point of payment instead of
    // scanning the full current-orders list. Set by the cashier for POS/DINEIN orders, or copied from the
    // QR context's Code for table-QR orders (SprintSixteenEndpoints.SubmitOrder).
    public string? TableNumber { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<OrderLine> Lines { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
}

public sealed class OrderLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductNameAr { get; set; }
    public required string ProductNameEn { get; set; }
    public int Quantity { get; set; }
    public int VoidedQuantity { get; set; }
    public string? Note { get; set; }
    public string SelectionsSnapshot { get; set; } = "[]";
    public decimal UnitListAmount { get; set; }
    public decimal UnitDiscountAmount { get; set; }
    public decimal UnitNetAmount { get; set; }
    public decimal UnitTaxAmount { get; set; }
    public decimal UnitGrossAmount { get; set; }
    public decimal TaxRate { get; set; }
    public TaxCalculationMode TaxCalculationMode { get; set; }
    public string PriceSource { get; set; } = "Default";
    public Guid? PriceRuleId { get; set; }
    public Guid? PromotionId { get; set; }
    public Guid? TaxRuleId { get; set; }
    public Guid? CatalogVersionId { get; set; }
    public int? CatalogVersionNumber { get; set; }
}

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
}

public static class OrderRules
{
    public const int NoteMax = 500;
    public const int TableNumberMax = 20;
    public static bool CanTransition(OrderStatus from, OrderStatus to) => from == to || (from, to) switch
    {
        (OrderStatus.Draft, OrderStatus.Pending or OrderStatus.Cancelled) => true,
        (OrderStatus.Pending, OrderStatus.Confirmed or OrderStatus.Paid or OrderStatus.SentToKitchen or OrderStatus.Cancelled or OrderStatus.Rejected) => true,
        (OrderStatus.Confirmed, OrderStatus.Paid or OrderStatus.SentToKitchen or OrderStatus.Cancelled) => true,
        (OrderStatus.Paid, OrderStatus.SentToKitchen or OrderStatus.Completed or OrderStatus.Refunded or OrderStatus.PartiallyRefunded) => true,
        (OrderStatus.SentToKitchen, OrderStatus.Preparing or OrderStatus.Cancelled) => true,
        (OrderStatus.Preparing, OrderStatus.Ready or OrderStatus.Cancelled) => true,
        (OrderStatus.Ready, OrderStatus.Completed or OrderStatus.Cancelled) => true,
        (OrderStatus.PartiallyRefunded, OrderStatus.Refunded) => true,
        _ => false
    };
}
