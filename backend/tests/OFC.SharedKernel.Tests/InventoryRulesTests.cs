using OFC.Modules.Inventory;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class InventoryRulesTests
{
    private static readonly Guid Kg = Guid.NewGuid();
    private static readonly Guid G = Guid.NewGuid();
    private static readonly Guid Ml = Guid.NewGuid();
    private static readonly Guid Carton = Guid.NewGuid();
    private static readonly Guid Pack = Guid.NewGuid();
    private static readonly Guid Piece = Guid.NewGuid();

    private static UnitConversion Edge(Guid from, Guid to, decimal factor) => new() { FromUnitId = from, ToUnitId = to, Factor = factor, IsActive = true };

    private static readonly UnitConversion[] MassEdges = [Edge(Kg, G, 1000m), Edge(G, Ml, 1m)];

    private static readonly UnitConversion[] PackEdges = [Edge(Carton, Pack, 24m), Edge(Pack, Piece, 1m)];

    [Fact]
    public void Walking_the_same_unit_is_identity()
    {
        var found = InventoryRules.TryConvert(5m, Kg, Kg, MassEdges, out var result);
        Assert.True(found);
        Assert.Equal(5m, result);
    }

    [Fact]
    public void Direct_conversion_multiplies_the_ingredient_into_the_base_unit()
    {
        var found = InventoryRules.TryConvert(2m, Kg, G, MassEdges, out var result);
        Assert.True(found);
        Assert.Equal(2000m, result);
    }

    [Fact]
    public void Inverse_conversion_uses_the_reciprocal_without_an_explicit_reverse_row()
    {
        var found = InventoryRules.TryConvert(1500m, G, Kg, MassEdges, out var result);
        Assert.True(found);
        Assert.Equal(1.5m, result);
    }

    [Fact]
    public void Transitive_conversion_chains_two_steps_and_keeps_precision()
    {
        var found = InventoryRules.TryConvert(2m, Carton, Piece, PackEdges, out var result);
        Assert.True(found);
        Assert.Equal(48m, result);
    }

    [Fact]
    public void A_missing_conversion_path_is_reported_as_unconvertible()
    {
        var found = InventoryRules.TryConvert(2m, Kg, Piece, PackEdges, out var result);
        Assert.False(found);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Inactive_conversions_are_ignored_when_walking_the_graph()
    {
        var edges = new[] { new UnitConversion { FromUnitId = Kg, ToUnitId = G, Factor = 1000m, IsActive = false } };
        var found = InventoryRules.TryConvert(2m, Kg, G, edges, out var result);
        Assert.False(found);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Quantity_must_be_positive_and_within_an_allowable_tolerance()
    {
        Assert.False(InventoryRules.ValidDirection(InventoryMovementType.Opening, 0m));
        Assert.False(InventoryRules.ValidDirection(InventoryMovementType.Purchase, -1m));
        Assert.True(InventoryRules.ValidDirection(InventoryMovementType.Opening, 100m));
        Assert.True(InventoryRules.ValidDirection(InventoryMovementType.Waste, -3m));
        Assert.False(InventoryRules.ValidDirection(InventoryMovementType.Waste, 3m));
        Assert.True(InventoryRules.ValidDirection(InventoryMovementType.Adjustment, -2m));
        Assert.True(InventoryRules.ValidDirection(InventoryMovementType.Adjustment, 2m));
        Assert.True(InventoryRules.ValidDirection(InventoryMovementType.CountAdjustment, 0.5m));
        Assert.False(InventoryRules.ValidDirection(InventoryMovementType.SaleDeduction, -1m));
    }

    [Fact]
    public void A_conversion_is_valid_only_when_units_differ_and_the_factor_is_positive()
    {
        Assert.True(InventoryRules.ValidConversion(Edge(Kg, G, 1000m)));
        Assert.False(InventoryRules.ValidConversion(Edge(Kg, G, 0m)));
        Assert.False(InventoryRules.ValidConversion(Edge(Kg, G, -1000m)));
        Assert.False(InventoryRules.ValidConversion(Edge(Kg, Kg, 1m)));
        Assert.False(InventoryRules.ValidConversion(Edge(Kg, G, InventoryRules.MaxConversionFactor * 2m)));
    }

    [Fact]
    public void A_draft_recipe_can_be_activated_but_not_revised()
    {
        Assert.True(InventoryRules.CanActivate(RecipeStatus.Draft));
        Assert.False(InventoryRules.CanActivate(RecipeStatus.Active));
        Assert.False(InventoryRules.CanActivate(RecipeStatus.Archived));
        Assert.False(InventoryRules.CanRevise(RecipeStatus.Draft));
        Assert.True(InventoryRules.CanRevise(RecipeStatus.Active));
        Assert.False(InventoryRules.CanRevise(RecipeStatus.Archived));
    }

    [Fact]
    public void Planning_a_deduction_scales_each_ingredient_line_by_the_product_quantity()
    {
        var chicken = Guid.NewGuid();
        var bread = Guid.NewGuid();
        var sauce = Guid.NewGuid();
        var lines = new List<RecipeLine>
        {
            new() { InventoryItemId = chicken, Quantity = 150m, UnitId = G },
            new() { InventoryItemId = bread, Quantity = 1m, UnitId = Piece },
            new() { InventoryItemId = sauce, Quantity = 0.3m, UnitId = Ml }
        };
        var planned = InventoryRules.PlanDeductions(lines, 2m);
        Assert.Contains((chicken, G, 300m), planned);
        Assert.Contains((bread, Piece, 2m), planned);
        Assert.Contains((sauce, Ml, 0.6m), planned);
    }

    [Fact]
    public void Planning_a_deduction_aggregates_repeated_ingredient_lines_into_a_single_charge()
    {
        var cheese = Guid.NewGuid();
        var lines = new List<RecipeLine>
        {
            new() { InventoryItemId = cheese, Quantity = 50m, UnitId = G },
            new() { InventoryItemId = cheese, Quantity = 10m, UnitId = G }
        };
        var planned = InventoryRules.PlanDeductions(lines, 3m);
        Assert.Single(planned);
        Assert.Equal((cheese, G, 180m), planned[0]);
    }

    [Fact]
    public void Zero_quantity_lines_do_not_produce_a_deduction_charge()
    {
        var salt = Guid.NewGuid();
        var lines = new List<RecipeLine> { new() { InventoryItemId = salt, Quantity = 0m, UnitId = G } };
        Assert.Empty(InventoryRules.PlanDeductions(lines, 2m));
    }

    [Fact]
    public void Quantity_is_rounded_to_the_ledger_precision()
    {
        Assert.Equal(1.234567m, InventoryRules.RoundQuantity(1.2345674m));
        Assert.Equal(1.234568m, InventoryRules.RoundQuantity(1.2345675m));
    }
}
