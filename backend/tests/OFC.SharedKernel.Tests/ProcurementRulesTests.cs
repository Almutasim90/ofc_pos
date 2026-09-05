using OFC.Modules.Procurement;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class ProcurementRulesTests
{
    [Fact]
    public void A_supplier_name_must_be_present_within_its_length_limit()
    {
        Assert.True(ProcurementRules.ValidName("خالد"));
        Assert.True(ProcurementRules.ValidName("Chicken Co"));
        Assert.False(ProcurementRules.ValidName(null));
        Assert.False(ProcurementRules.ValidName("   "));
        Assert.False(ProcurementRules.ValidName(new string('x', ProcurementRules.NameMax + 1)));
    }

    [Fact]
    public void A_supplier_code_must_be_present_within_its_length_limit()
    {
        Assert.True(ProcurementRules.ValidCode("SUP-0001"));
        Assert.False(ProcurementRules.ValidCode(null));
        Assert.False(ProcurementRules.ValidCode("  "));
        Assert.False(ProcurementRules.ValidCode(new string('c', ProcurementRules.CodeMax + 1)));
    }

    [Fact]
    public void An_email_may_be_absent_but_must_contain_an_at_sign_when_supplied()
    {
        Assert.True(ProcurementRules.ValidEmail(null));
        Assert.True(ProcurementRules.ValidEmail("buyer@ofc.om"));
        Assert.False(ProcurementRules.ValidEmail("not-an-email"));
        Assert.False(ProcurementRules.ValidEmail(new string('a', ProcurementRules.EmailMax) + "@x"));
    }

    [Fact]
    public void A_receipt_quantity_must_be_positive_and_within_an_allowable_range()
    {
        Assert.True(ProcurementRules.ValidQuantity(10m));
        Assert.False(ProcurementRules.ValidQuantity(0m));
        Assert.False(ProcurementRules.ValidQuantity(-3m));
        Assert.False(ProcurementRules.ValidQuantity(0.0000001m));
        Assert.False(ProcurementRules.ValidQuantity(10_000_000m));
    }

    [Fact]
    public void A_line_total_is_quantity_times_unit_cost_rounded_to_money()
    {
        Assert.Equal(10m, ProcurementRules.LineTotal(2m, 5m));
        Assert.Equal(10.0085m, ProcurementRules.LineTotal(2.0011m, 5.0015m));
        Assert.Equal(0m, ProcurementRules.LineTotal(2m, 0m));
    }

    [Fact]
    public void Quantities_and_costs_are_rounded_to_the_ledger_precision()
    {
        Assert.Equal(1.234567m, ProcurementRules.RoundQuantity(1.2345674m));
        Assert.Equal(1.234568m, ProcurementRules.RoundQuantity(1.2345675m));
        Assert.Equal(0.123457m, ProcurementRules.RoundCost(0.1234565m));
    }

    [Fact]
    public void The_weighted_average_cost_blends_existing_stock_with_the_receipt()
    {
        var average = ProcurementRules.WeightedAverageCost(currentStock: 10m, currentCost: 2m, receivedQuantity: 10m, receivedCost: 4m);
        Assert.Equal(3m, average);
    }

    [Fact]
    public void A_weighted_average_cost_keeps_the_current_cost_when_stock_is_unchanged()
    {
        var average = ProcurementRules.WeightedAverageCost(currentStock: 0m, currentCost: 2m, receivedQuantity: 10m, receivedCost: 4m);
        Assert.Equal(4m, average);
    }

    [Fact]
    public void The_weighted_average_cost_projects_a_new_cost_when_stock_depleted()
    {
        var average = ProcurementRules.WeightedAverageCost(currentStock: 5m, currentCost: 2m, receivedQuantity: -5m, receivedCost: 4m);
        Assert.Equal(4m, average);
    }

    [Fact]
    public void A_purchase_order_only_moves_through_the_applicable_workflow_steps()
    {
        Assert.True(ProcurementRules.CanSubmit(PurchaseOrderStatus.Draft));
        Assert.False(ProcurementRules.CanSubmit(PurchaseOrderStatus.Submitted));
        Assert.True(ProcurementRules.CanApprove(PurchaseOrderStatus.Submitted));
        Assert.False(ProcurementRules.CanApprove(PurchaseOrderStatus.Approved));
        Assert.True(ProcurementRules.CanReceive(PurchaseOrderStatus.Approved));
        Assert.False(ProcurementRules.CanReceive(PurchaseOrderStatus.Submitted));
        Assert.True(ProcurementRules.CanCancel(PurchaseOrderStatus.Draft));
        Assert.False(ProcurementRules.CanCancel(PurchaseOrderStatus.Received));
        Assert.True(ProcurementRules.CanPost(GoodsReceiptStatus.Draft));
        Assert.False(ProcurementRules.CanPost(GoodsReceiptStatus.Posted));
    }

    [Fact]
    public void A_document_line_count_must_be_within_the_allowed_range()
    {
        Assert.True(ProcurementRules.ValidLineCount(1));
        Assert.True(ProcurementRules.ValidLineCount(ProcurementRules.MaxLines));
        Assert.False(ProcurementRules.ValidLineCount(0));
        Assert.False(ProcurementRules.ValidLineCount(ProcurementRules.MaxLines + 1));
    }

    [Fact]
    public void A_cost_may_not_be_negative()
    {
        Assert.True(ProcurementRules.ValidCost(0m));
        Assert.True(ProcurementRules.ValidCost(12.5m));
        Assert.False(ProcurementRules.ValidCost(-0.01m));
    }
}
