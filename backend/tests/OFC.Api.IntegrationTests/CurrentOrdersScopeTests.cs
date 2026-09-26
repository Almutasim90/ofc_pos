using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Ordering;
using OFC.Modules.Shifts;
using Xunit;

namespace OFC.Api.IntegrationTests;

// The POS "current orders" panel asks for scope=shift: unfinished orders always stay visible (a held
// order from an earlier shift must still be completable), finished ones only since the open shift began.
public class CurrentOrdersScopeTests
{
    private static async Task<(HttpClient Client, Guid BranchId, Guid ChannelId, Guid ProductId)> Seed(ApiFactory factory)
    {
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "scope-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "scope-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var branches = await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>();
        var branchId = branches[0].GetProperty("id").GetGuid();
        var channel = await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "POS", nameAr = "الصالة", nameEn = "Dine-in", isActive = true });
        var channelId = (await channel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var category = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "مشروبات", nameEn = "Drinks", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var product = await client.PostAsJsonAsync("/api/v1/products", new { sku = "SCOPE-COLA", barcode = (string?)null, nameAr = "كولا", nameEn = "Cola", descriptionAr = (string?)null, descriptionEn = (string?)null, categoryId, type = "Simple", taxCategoryId = (Guid?)null, preparationStationId = (Guid?)null, basePrice = 1m, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = true } } });
        var productId = (await product.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (client, branchId, channelId, productId);
    }

    private static async Task<Guid> CreateOrder(HttpClient client, Guid branchId, Guid channelId, Guid productId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { branchId, salesChannelId = channelId, clientRequestId = Guid.NewGuid(), source = "Pos", note = (string?)null, lines = new[] { new { productId, quantity = 1, note = (string?)null, selections = Array.Empty<object>() } } });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> ListIds(HttpClient client, string url)
    {
        var body = await (await client.GetAsync(url)).Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task Shift_scope_keeps_unfinished_orders_and_only_this_shifts_finished_ones()
    {
        using var factory = new ApiFactory();
        var (client, branchId, channelId, productId) = await Seed(factory);
        var oldPaid = await CreateOrder(client, branchId, channelId, productId);
        var oldOpen = await CreateOrder(client, branchId, channelId, productId);
        var newPaid = await CreateOrder(client, branchId, channelId, productId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            var now = DateTimeOffset.UtcNow;
            var orders = db.Orders.Where(x => x.BranchId == branchId).ToList();
            void Set(Guid id, OrderStatus status, DateTimeOffset createdAt)
            {
                var order = orders.Single(x => x.Id == id);
                order.Status = status;
                order.CreatedAt = createdAt;
            }
            Set(oldPaid, OrderStatus.Paid, now.AddHours(-5));
            Set(oldOpen, OrderStatus.Pending, now.AddHours(-5));
            Set(newPaid, OrderStatus.Paid, now.AddMinutes(-10));
            db.Shifts.Add(new Shift { BranchId = branchId, OpenedByUserId = Guid.NewGuid(), OpenedAt = now.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var scoped = await ListIds(client, $"/api/v1/orders?branchId={branchId}&scope=shift");
        Assert.Contains(oldOpen, scoped);
        Assert.Contains(newPaid, scoped);
        Assert.DoesNotContain(oldPaid, scoped);

        // Other screens (cancellations, kitchen) keep the unscoped list.
        var all = await ListIds(client, $"/api/v1/orders?branchId={branchId}");
        Assert.Contains(oldPaid, all);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            foreach (var shift in db.Shifts.Where(x => x.BranchId == branchId)) shift.Status = ShiftStatus.Closed;
            await db.SaveChangesAsync();
        }
        var noShift = await ListIds(client, $"/api/v1/orders?branchId={branchId}&scope=shift");
        Assert.Equal(new[] { oldOpen }, noShift);
    }
}
