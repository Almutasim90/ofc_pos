using OFC.Modules.Catalog;
using OFC.Modules.Ordering;
using OFC.Modules.QrOrdering;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class QrRulesTests
{
    private const string Reserved = "orders";
    private static readonly DateTimeOffset At = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("T-01", true)]
    [InlineData("P-12", true)]
    [InlineData("orders", false)]
    [InlineData("menu", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Code_must_be_present_unique_and_not_reserved(string code, bool expected) => Assert.Equal(expected, QrRules.ValidCode(code));

    [Fact]
    public void Staff_approval_is_only_for_requires_staff_approval_mode()
    {
        Assert.False(QrRules.RequiresStaffApproval(QrApprovalMode.None));
        Assert.False(QrRules.RequiresStaffApproval(QrApprovalMode.AutoApprove));
        Assert.True(QrRules.RequiresStaffApproval(QrApprovalMode.RequiresStaffApproval));
    }

    [Fact]
    public void Only_pending_approval_can_be_reviewed()
    {
        Assert.True(QrRules.CanReviewApproval(QrOrderApprovalStatus.Pending));
        Assert.False(QrRules.CanReviewApproval(QrOrderApprovalStatus.Approved));
        Assert.False(QrRules.CanReviewApproval(QrOrderApprovalStatus.Rejected));
    }

    [Theory]
    [InlineData(QrOrderApprovalStatus.Approved, OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(QrOrderApprovalStatus.Rejected, OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(QrOrderApprovalStatus.Approved, OrderStatus.Paid, OrderStatus.Paid)]
    public void Resolution_maps_approval_to_a_valid_next_status(QrOrderApprovalStatus approval, OrderStatus current, OrderStatus expected) => Assert.Equal(expected, QrRules.ResolutionToOrderStatus(approval, current));

    [Fact]
    public void Build_uses_the_shared_pricing_engine_with_promotion_and_selection()
    {
        var productId = Guid.CreateVersion7();
        var optionProductId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var optionId = Guid.CreateVersion7();
        var group = new SelectionGroup { Id = groupId, NameAr = "مشروب", NameEn = "Drink", IsRequired = true, MinSelections = 1, MaxSelections = 1, Options = [new SelectionOption { Id = optionId, ProductId = optionProductId, PriceAdjustment = .5m, MaxQuantity = 1 }] };
        var product = new Product { Id = productId, Sku = "ZINGER", NameAr = "زنجر", NameEn = "Zinger", CategoryId = Guid.CreateVersion7(), BasePrice = 10m, SelectionGroups = [new ProductSelectionGroup { ProductId = productId, SelectionGroupId = groupId, SelectionGroup = group }] };
        var products = new Dictionary<Guid, Product> { [productId] = product };
        var branch = Guid.CreateVersion7();
        var channel = Guid.CreateVersion7();

        var result = OrderingEngine.Build(branch, channel, OrderSource.Qr, null, Guid.CreateVersion7(), null, null, Guid.CreateVersion7(), At, [new OrderLineInput(productId, 1, null, [new GroupSelectionInput(groupId, [new ChoiceInput(optionId, 1)])])], products, [], [new Promotion { Code = "TEN", NameAr = "خصم", NameEn = "Discount", DiscountType = PromotionDiscountType.Percentage, DiscountValue = 10m, EffectiveFrom = At.AddDays(-1) }], [], null);

        Assert.True(result.Succeeded);
        var order = result.Order!;
        Assert.Equal(OrderSource.Qr, order.Source);
        Assert.Equal(9.45m, order.GrossAmount);
        Assert.Equal(9.45m, order.Lines.Single().UnitGrossAmount);
    }

    [Fact]
    public void Build_rejects_a_missing_required_selection_group()
    {
        var productId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var group = new SelectionGroup { Id = groupId, NameAr = "مشروب", NameEn = "Drink", IsRequired = true, MinSelections = 1, MaxSelections = 1 };
        var product = new Product { Id = productId, NameAr = "زنجر", NameEn = "Zinger", Sku = "ZINGER", CategoryId = Guid.CreateVersion7(), BasePrice = 10m, SelectionGroups = [new ProductSelectionGroup { ProductId = productId, SelectionGroupId = groupId, SelectionGroup = group }] };
        var result = OrderingEngine.Build(Guid.CreateVersion7(), Guid.CreateVersion7(), OrderSource.Qr, null, null, null, null, Guid.CreateVersion7(), At, [new OrderLineInput(productId, 1, null, [])], new Dictionary<Guid, Product> { [productId] = product }, [], [], [], null);

        Assert.False(result.Succeeded);
        Assert.Equal("selections", result.Field);
        Assert.Equal("A required selection group is missing.", result.Error);
    }

    [Fact]
    public void Build_rejects_a_product_unavailable_at_the_branch()
    {
        var result = OrderingEngine.Build(Guid.CreateVersion7(), Guid.CreateVersion7(), OrderSource.Qr, null, null, null, null, Guid.CreateVersion7(), At, [new OrderLineInput(Guid.CreateVersion7(), 1, null, null)], new Dictionary<Guid, Product>(), [], [], [], null);

        Assert.False(result.Succeeded);
        Assert.Equal("lines", result.Field);
        Assert.Equal("One or more products are unavailable at this branch.", result.Error);
    }
}
