using OFC.Modules.Inventory;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class InventoryAdvancedRulesTests
{
    private static readonly Guid Kg = Guid.NewGuid();
    private static readonly Guid G = Guid.NewGuid();
    private static readonly Guid Piece = Guid.NewGuid();

    private static UnitConversion Edge(Guid from, Guid to, decimal factor) => new() { FromUnitId = from, ToUnitId = to, Factor = factor, IsActive = true };

    private static InventoryItem Item(Guid id, Guid baseUnitId, decimal unitCost) => new() { Id = id, Sku = $"SKU-{id:N}", NameAr = "مادة", NameEn = "Item", BaseUnitId = baseUnitId, UnitCost = unitCost };

    [Fact]
    public void A_count_variance_is_the_counted_amount_less_the_system_amount()
    {
        Assert.Equal(5m, InventoryRules.CountVariance(systemQuantity: 10m, countedQuantity: 15m));
        Assert.Equal(-3m, InventoryRules.CountVariance(systemQuantity: 10m, countedQuantity: 7m));
        Assert.Equal(0m, InventoryRules.CountVariance(systemQuantity: 10m, countedQuantity: 10m));
    }

    [Fact]
    public void A_count_variance_is_only_significant_beyond_the_minimum_tolerance()
    {
        Assert.True(InventoryRules.IsSignificantVariance(0.001m));
        Assert.False(InventoryRules.IsSignificantVariance(0.0000005m));
        Assert.False(InventoryRules.IsSignificantVariance(0m));
    }

    [Fact]
    public void A_count_moves_draft_then_posted_and_cannot_be_repeated()
    {
        Assert.True(InventoryRules.CanApproveCount(InventoryCountStatus.Draft));
        Assert.False(InventoryRules.CanApproveCount(InventoryCountStatus.Posted));
        Assert.True(InventoryRules.CanPostCount(InventoryCountStatus.Draft));
        Assert.False(InventoryRules.CanPostCount(InventoryCountStatus.Posted));
        Assert.True(InventoryRules.CanCancelCount(InventoryCountStatus.Draft));
        Assert.False(InventoryRules.CanCancelCount(InventoryCountStatus.Posted));
    }

    [Fact]
    public void A_transfer_moves_draft_to_in_transit_to_received_and_cannot_skip()
    {
        Assert.True(InventoryRules.CanShipTransfer(StockTransferStatus.Draft));
        Assert.False(InventoryRules.CanShipTransfer(StockTransferStatus.InTransit));
        Assert.True(InventoryRules.CanReceiveTransfer(StockTransferStatus.InTransit));
        Assert.False(InventoryRules.CanReceiveTransfer(StockTransferStatus.Draft));
        Assert.False(InventoryRules.CanReceiveTransfer(StockTransferStatus.Received));
        Assert.True(InventoryRules.CanCancelTransfer(StockTransferStatus.Draft));
        Assert.True(InventoryRules.CanCancelTransfer(StockTransferStatus.InTransit));
        Assert.False(InventoryRules.CanCancelTransfer(StockTransferStatus.Received));
    }

    [Fact]
    public void A_waste_quantity_must_be_positive_and_within_a_sane_range()
    {
        Assert.True(InventoryRules.ValidWasteQuantity(0.5m));
        Assert.False(InventoryRules.ValidWasteQuantity(0m));
        Assert.False(InventoryRules.ValidWasteQuantity(-1m));
        Assert.False(InventoryRules.ValidWasteQuantity(10000m));
    }

    [Fact]
    public void A_recipe_cost_is_reproducible_when_every_line_can_be_converted_at_a_known_cost()
    {
        var chicken = Item(Guid.NewGuid(), G, 0.02m);
        var bread = Item(Guid.NewGuid(), Piece, 0.5m);
        var items = new Dictionary<Guid, InventoryItem> { [chicken.Id] = chicken, [bread.Id] = bread };
        var conversions = new List<UnitConversion> { Edge(Kg, G, 1000m) };
        var lines = new List<RecipeLine>
        {
            new() { InventoryItemId = chicken.Id, Quantity = 150m, UnitId = G },
            new() { InventoryItemId = bread.Id, Quantity = 1m, UnitId = Piece }
        };
        var reproducible = InventoryRules.TryComputeRecipeCost(lines, items, conversions, out var cost);
        Assert.True(reproducible);
        Assert.Equal(3.5m, cost);
    }

    [Fact]
    public void A_recipe_cost_converts_a_bulk_unit_into_its_base_unit_cost()
    {
        var sugar = Item(Guid.NewGuid(), Kg, 1.5m);
        var items = new Dictionary<Guid, InventoryItem> { [sugar.Id] = sugar };
        var conversions = new List<UnitConversion> { Edge(Kg, G, 1000m) };
        var lines = new List<RecipeLine> { new() { InventoryItemId = sugar.Id, Quantity = 2m, UnitId = Kg } };
        var reproducible = InventoryRules.TryComputeRecipeCost(lines, items, conversions, out var cost);
        Assert.True(reproducible);
        Assert.Equal(3m, cost);
    }

    [Fact]
    public void A_recipe_cost_is_not_reproducible_when_a_conversion_is_missing()
    {
        var sugar = Item(Guid.NewGuid(), Kg, 1.5m);
        var items = new Dictionary<Guid, InventoryItem> { [sugar.Id] = sugar };
        var conversions = new List<UnitConversion>();
        var lines = new List<RecipeLine> { new() { InventoryItemId = sugar.Id, Quantity = 2m, UnitId = G } };
        var reproducible = InventoryRules.TryComputeRecipeCost(lines, items, conversions, out _);
        Assert.False(reproducible);
    }

    [Fact]
    public void A_recipe_cost_is_not_reproducible_when_an_item_cost_is_negative_or_missing()
    {
        var unknown = Guid.NewGuid();
        var sugar = Item(Guid.NewGuid(), G, -1m);
        var missing = InventoryRules.TryComputeRecipeCost(new List<RecipeLine> { new() { InventoryItemId = unknown, Quantity = 2m, UnitId = G } }, new Dictionary<Guid, InventoryItem> { [sugar.Id] = sugar }, new List<UnitConversion>(), out _);
        Assert.False(missing);
        var negative = InventoryRules.TryComputeRecipeCost(new List<RecipeLine> { new() { InventoryItemId = sugar.Id, Quantity = 2m, UnitId = G } }, new Dictionary<Guid, InventoryItem> { [sugar.Id] = sugar }, new List<UnitConversion>(), out _);
        Assert.False(negative);
    }

    [Fact]
    public void Food_cost_percent_is_the_recipe_cost_over_the_selling_price()
    {
        Assert.Equal(40m, InventoryRules.FoodCostPercent(recipeCost: 2m, sellingPrice: 5m));
        Assert.Equal(0m, InventoryRules.FoodCostPercent(recipeCost: 2m, sellingPrice: 0m));
    }

    [Fact]
    public void Gross_margin_is_price_less_cost_and_percent_is_margin_over_price()
    {
        Assert.Equal(3m, InventoryRules.GrossMargin(recipeCost: 2m, sellingPrice: 5m));
        Assert.Equal(60m, InventoryRules.GrossMarginPercent(recipeCost: 2m, sellingPrice: 5m));
        Assert.Equal(0m, InventoryRules.GrossMarginPercent(recipeCost: 2m, sellingPrice: 0m));
    }

    [Fact]
    public void Money_and_cost_are_rounded_to_their_ledger_precisions()
    {
        Assert.Equal(1.2346m, InventoryRules.RoundMoney(1.23455m));
        Assert.Equal(0.123457m, InventoryRules.RoundCost(0.1234565m));
    }

    [Fact]
    public void A_counted_quantity_may_be_zero_but_may_not_exceed_the_cap()
    {
        Assert.True(InventoryRules.ValidCountedQuantity(0m));
        Assert.True(InventoryRules.ValidCountedQuantity(9999999m));
        Assert.False(InventoryRules.ValidCountedQuantity(-1m));
        Assert.False(InventoryRules.ValidCountedQuantity(10000000m));
    }
}
