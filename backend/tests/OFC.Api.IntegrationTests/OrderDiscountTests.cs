using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// The cashier-facing manual discount (POS cart -> "+ Add discount") is gated behind the
// "orders.discount" permission and a server-enforced cap (OrderRules.ManualDiscountMaxPercent), because
// the client's estimate is only ever a preview: the server is the sole authority on both who may apply
// a discount and how large it may be.
public class OrderDiscountTests
{
    private static async Task<(HttpClient Admin, Guid BranchId, Guid ProductId, Guid ChannelId)> SeedAdmin(ApiFactory factory, string username)
    {
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username, displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var branches = await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>();
        var branchId = branches[0].GetProperty("id").GetGuid();

        var channelResponse = await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "POS", nameAr = "الصالة", nameEn = "Dine-in", isActive = true });
        var channelId = (await channelResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "مشروبات", nameEn = "Drinks", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null });
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var productResponse = await client.PostAsJsonAsync("/api/v1/products", new { sku = "DISC-COLA", barcode = (string?)null, nameAr = "كولا", nameEn = "Cola", descriptionAr = (string?)null, descriptionEn = (string?)null, categoryId, type = "Simple", taxCategoryId = (Guid?)null, preparationStationId = (Guid?)null, basePrice = 10m, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = true } } });
        var productId = (await productResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return (client, branchId, productId, channelId);
    }

    private static async Task<HttpClient> LoginAsRole(ApiFactory factory, HttpClient admin, Guid branchId, string roleName, string username)
    {
        var roles = await (await admin.GetAsync("/api/v1/roles")).Content.ReadFromJsonAsync<JsonElement>();
        var roleId = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == roleName).GetProperty("id").GetGuid();
        var createUser = await admin.PostAsJsonAsync("/api/v1/users", new { username, email = (string?)null, displayName = roleName, password = "password1234", roleIds = new[] { roleId }, branchIds = new[] { branchId } });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        var client = factory.AnonymousClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object OrderBody(Guid branchId, Guid channelId, Guid productId, object? discount = null) =>
        new { branchId, salesChannelId = channelId, clientRequestId = Guid.NewGuid(), source = "Pos", note = (string?)null, discount, lines = new[] { new { productId, quantity = 1, note = (string?)null, selections = Array.Empty<object>() } } };

    [Fact]
    public async Task Cashier_without_the_discount_permission_is_forbidden_from_applying_one()
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, channelId) = await SeedAdmin(factory, "disc-admin-1");
        var cashier = await LoginAsRole(factory, admin, branchId, "Cashier", "disc-cashier-1");

        var response = await cashier.PostAsJsonAsync("/api/v1/orders", OrderBody(branchId, channelId, productId, new { type = "Percentage", value = 10m }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cashier_without_the_discount_permission_can_still_create_a_plain_order()
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, channelId) = await SeedAdmin(factory, "disc-admin-2");
        var cashier = await LoginAsRole(factory, admin, branchId, "Cashier", "disc-cashier-2");

        var response = await cashier.PostAsJsonAsync("/api/v1/orders", OrderBody(branchId, channelId, productId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_apply_a_percentage_discount_and_totals_stay_consistent()
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, channelId) = await SeedAdmin(factory, "disc-admin-3");

        var response = await admin.PostAsJsonAsync("/api/v1/orders", OrderBody(branchId, channelId, productId, new { type = "Percentage", value = 10m }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1m, order.GetProperty("manualDiscountAmount").GetDecimal());
        Assert.Equal(9m, order.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(order.GetProperty("netAmount").GetDecimal() + order.GetProperty("taxAmount").GetDecimal(), order.GetProperty("grossAmount").GetDecimal());
    }

    [Fact]
    public async Task A_fixed_amount_discount_beyond_the_cap_is_rejected()
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, channelId) = await SeedAdmin(factory, "disc-admin-4");

        // The order totals 10 OMR; the cap is 20%, so anything above 2 OMR must be rejected outright
        // rather than silently clamped.
        var response = await admin.PostAsJsonAsync("/api/v1/orders", OrderBody(branchId, channelId, productId, new { type = "Amount", value = 5m }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_zero_or_negative_discount_value_is_rejected()
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, channelId) = await SeedAdmin(factory, "disc-admin-5");

        var response = await admin.PostAsJsonAsync("/api/v1/orders", OrderBody(branchId, channelId, productId, new { type = "Percentage", value = 0m }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
