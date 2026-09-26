using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Customers keep adding items to an open order; a held or unpaid order is edited in place and repriced by
// the same engine as a new order, but once the kitchen has it (or money was taken) it is frozen.
public class OrderEditTests
{
    private static async Task<(HttpClient Client, Guid BranchId, Guid ChannelId, Guid ColaId, Guid BurgerId)> Seed(ApiFactory factory, string username)
    {
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username, displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var channelId = (await (await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "POS", nameAr = "الصالة", nameEn = "Dine-in", isActive = true })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var categoryId = (await (await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "وجبات", nameEn = "Meals", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        async Task<Guid> Product(string sku, decimal price) => (await (await client.PostAsJsonAsync("/api/v1/products", new { sku, barcode = (string?)null, nameAr = sku, nameEn = sku, descriptionAr = (string?)null, descriptionEn = (string?)null, categoryId, type = "Simple", taxCategoryId = (Guid?)null, preparationStationId = (Guid?)null, basePrice = price, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = true } } })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (client, branchId, channelId, await Product("EDIT-COLA", 1m), await Product("EDIT-BURGER", 3m));
    }

    private static object Line(Guid productId, int quantity) => new { productId, quantity, note = (string?)null, selections = Array.Empty<object>() };

    private static async Task<Guid> CreateOrder(HttpClient client, Guid branchId, Guid channelId, Guid productId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { branchId, salesChannelId = channelId, clientRequestId = Guid.NewGuid(), source = "Pos", note = (string?)null, lines = new[] { Line(productId, 1) } });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Held_order_can_gain_items_and_is_repriced_in_place()
    {
        using var factory = new ApiFactory();
        var (client, branchId, channelId, cola, burger) = await Seed(factory, "edit-admin-1");
        var orderId = await CreateOrder(client, branchId, channelId, cola);

        var edit = await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/lines", new { lines = new[] { Line(cola, 2), Line(burger, 1) } });
        Assert.True(edit.StatusCode == HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());

        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", order.GetProperty("status").GetString());
        Assert.Equal(2, order.GetProperty("lines").GetArrayLength());
        Assert.Equal(5m, order.GetProperty("grossAmount").GetDecimal());
    }

    [Fact]
    public async Task Order_already_in_the_kitchen_cannot_be_edited()
    {
        using var factory = new ApiFactory();
        var (client, branchId, channelId, cola, burger) = await Seed(factory, "edit-admin-2");
        var orderId = await CreateOrder(client, branchId, channelId, cola);
        (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Pending", note = (string?)null })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { branchId, orderId, clientDispatchId = Guid.NewGuid(), orderNumber = (string?)null, note = (string?)null, targetMinutes = (int?)null })).EnsureSuccessStatusCode();

        var edit = await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/lines", new { lines = new[] { Line(cola, 1), Line(burger, 1) } });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
    }
}
