using System.Text.Json;
using OFC.Modules.Catalog;

namespace OFC.Modules.Ordering;

public sealed record OrderLineInput(Guid ProductId, int Quantity, string? Note, List<GroupSelectionInput>? Selections);
public sealed record GroupSelectionInput(Guid SelectionGroupId, List<ChoiceInput> Choices);
public sealed record ChoiceInput(Guid OptionId, int Quantity);

public sealed record OrderingBuildResult(Order? Order, string? Field, string? Error)
{
    public bool Succeeded => Order is not null;
    public static OrderingBuildResult Fail(string field, string error) => new(null, field, error);
}

public static class OrderingEngine
{
    public static OrderingBuildResult Build(
        Guid branchId,
        Guid salesChannelId,
        OrderSource source,
        Guid? createdByUserId,
        Guid? customerId,
        Guid? deviceId,
        string? note,
        Guid clientRequestId,
        DateTimeOffset at,
        IReadOnlyList<OrderLineInput> lines,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyList<PriceRule> prices,
        IReadOnlyList<Promotion> promotions,
        IReadOnlyList<TaxRule> taxes,
        CatalogVersion? version)
    {
        var order = new Order
        {
            BranchId = branchId,
            SalesChannelId = salesChannelId,
            DeviceId = deviceId,
            CreatedByUserId = createdByUserId,
            CustomerId = customerId,
            ClientRequestId = clientRequestId,
            Source = source,
            Note = note?.Trim()
        };

        foreach (var requestLine in lines)
        {
            if (requestLine.Quantity is < 1 or > 99 || requestLine.Note?.Trim().Length > OrderRules.NoteMax)
                return OrderingBuildResult.Fail("lines", "Line quantity must be between 1 and 99 and notes must be valid.");
            if (!products.TryGetValue(requestLine.ProductId, out var product))
                return OrderingBuildResult.Fail("lines", "One or more products are unavailable at this branch.");
            var selections = ValidateSelections(product, requestLine.Selections, branchId);
            if (selections.Error is not null)
                return OrderingBuildResult.Fail("selections", selections.Error);
            var snapshot = PricingRules.Resolve(product, branchId, salesChannelId, at, prices, promotions, taxes, version, selectionAdjustment: selections.Adjustment);
            var line = new OrderLine
            {
                ProductId = product.Id,
                ProductNameAr = product.NameAr,
                ProductNameEn = product.NameEn,
                Quantity = requestLine.Quantity,
                Note = requestLine.Note?.Trim(),
                SelectionsSnapshot = JsonSerializer.Serialize(selections.Snapshot),
                UnitListAmount = snapshot.ListPrice,
                UnitDiscountAmount = snapshot.DiscountAmount,
                UnitNetAmount = snapshot.UnitNetAmount,
                UnitTaxAmount = snapshot.UnitTaxAmount,
                UnitGrossAmount = snapshot.UnitGrossAmount,
                TaxRate = snapshot.TaxRate,
                TaxCalculationMode = snapshot.TaxCalculationMode,
                PriceSource = snapshot.PriceSource,
                PriceRuleId = snapshot.PriceRuleId,
                PromotionId = snapshot.PromotionId,
                TaxRuleId = snapshot.TaxRuleId,
                CatalogVersionId = snapshot.CatalogVersionId,
                CatalogVersionNumber = snapshot.CatalogVersionNumber
            };
            order.Lines.Add(line);
            order.NetAmount += line.UnitNetAmount * line.Quantity;
            order.TaxAmount += line.UnitTaxAmount * line.Quantity;
            order.GrossAmount += line.UnitGrossAmount * line.Quantity;
        }

        order.NetAmount = PricingRules.RoundMoney(order.NetAmount);
        order.TaxAmount = PricingRules.RoundMoney(order.TaxAmount);
        order.GrossAmount = PricingRules.RoundMoney(order.GrossAmount);
        return new OrderingBuildResult(order, null, null);
    }

    private static (decimal Adjustment, object Snapshot, string? Error) ValidateSelections(Product product, List<GroupSelectionInput>? requested, Guid branchId)
    {
        requested ??= [];
        var configured = product.SelectionGroups
            .Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable)))
            .Select(x => x.SelectionGroup!)
            .ToDictionary(x => x.Id);
        if (requested.Select(x => x.SelectionGroupId).Distinct().Count() != requested.Count || requested.Any(x => !configured.ContainsKey(x.SelectionGroupId)))
            return (0m, Array.Empty<object>(), "An invalid selection group was supplied.");
        var snapshots = new List<object>();
        decimal adjustment = 0;
        foreach (var group in configured.Values)
        {
            var choices = requested.SingleOrDefault(x => x.SelectionGroupId == group.Id)?.Choices ?? (group.IsRequired ? null : []);
            if (choices is null) return (0m, Array.Empty<object>(), "A required selection group is missing.");
            var calculated = CatalogRules.Calculate(group, choices.Select(x => new SelectionChoice(x.OptionId, x.Quantity)));
            if (!calculated.IsValid) return (0m, Array.Empty<object>(), calculated.Error);
            adjustment += calculated.PriceAdjustment;
            snapshots.Add(new { group.Id, group.Kind, group.NameAr, group.NameEn, choices = choices.Select(choice => new { choice.OptionId, choice.Quantity, priceAdjustment = group.Options.Single(x => x.Id == choice.OptionId).PriceAdjustment }) });
        }
        return (adjustment, snapshots, null);
    }
}
