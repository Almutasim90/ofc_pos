using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

public class UsabilityWorkflowTests
{
    private static async Task<(HttpClient Client, Guid BranchId)> Login(ApiFactory factory)
    {
        var client = factory.AnonymousClient();
        var bootstrap = await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "اختبار", organizationNameEn = "Test", branchNameAr = "فرع", branchNameEn = "Branch", username = "admin", displayName = "Admin", password = "password1234" });
        bootstrap.EnsureSuccessStatusCode();
        var (authenticated, _) = await factory.AuthenticatedClientAsync("admin", "password1234");
        var branches = await authenticated.GetFromJsonAsync<JsonElement>("/api/v1/branches");
        return (authenticated, branches[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Product_can_be_edited_with_images_and_keeps_its_generated_barcode()
    {
        using var factory = new ApiFactory(); var (client, branchId) = await Login(factory);
        var category = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "وجبات", nameEn = "Meals", sortOrder = 0 });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        object Request(string name, string image, bool available) => new { sku = "BURGER", barcode = (string?)null, nameAr = name, nameEn = "Burger", categoryId, type = "Simple", basePrice = 2.75m, isActive = true, images = new[] { new { url = image, sortOrder = 0 } }, availability = new[] { new { branchId, isAvailable = available } } };
        var created = await client.PostAsJsonAsync("/api/v1/products", Request("برجر", "/menu/old.jpg", true));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/products");
        var barcode = before[0].GetProperty("barcode").GetString(); Assert.False(string.IsNullOrWhiteSpace(barcode));
        var updated = await client.PutAsJsonAsync($"/api/v1/products/{id}", Request("برجر جديد", "/menu/new.jpg", false));
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/products");
        Assert.Equal(barcode, after[0].GetProperty("barcode").GetString());
        Assert.Equal("برجر جديد", after[0].GetProperty("nameAr").GetString());
        Assert.Equal("/menu/new.jpg", after[0].GetProperty("images")[0].GetProperty("url").GetString());
        Assert.False(after[0].GetProperty("availability")[0].GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Editing_user_preserves_password_when_blank_and_revokes_old_sessions()
    {
        using var factory = new ApiFactory(); var (client, branchId) = await Login(factory);
        var created = await client.PostAsJsonAsync("/api/v1/users", new { username = "staff", displayName = "Staff", password = "password1234", roleIds = Array.Empty<Guid>(), branchIds = new[] { branchId } });
        created.EnsureSuccessStatusCode(); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var (staff, _) = await factory.AuthenticatedClientAsync("staff", "password1234");
        var updated = await client.PutAsJsonAsync($"/api/v1/users/{id}", new { username = "staff", displayName = "Updated", email = "staff@example.com", password = (string?)null, isActive = true, branchIds = new[] { branchId } });
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/branches")).StatusCode);
        var login = await factory.AnonymousClient().PostAsJsonAsync("/api/v1/auth/login", new { username = "staff", password = "password1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Updated", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("displayName").GetString());
        var forbidden = await staff.PutAsJsonAsync($"/api/v1/users/{id}", new { username = "staff", displayName = "No", isActive = true, branchIds = new[] { branchId } });
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
    }

    [Fact]
    public async Task Inventory_item_can_be_edited_and_deactivated_but_not_reinterpreted_in_a_new_unit()
    {
        using var factory = new ApiFactory(); var (client, _) = await Login(factory);
        var unit = await client.PostAsJsonAsync("/api/v1/inventory/uoms", new { code = "KG", nameAr = "كيلوجرام", nameEn = "Kilogram", sortOrder = 0 }); unit.EnsureSuccessStatusCode();
        var unitId = (await unit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/api/v1/inventory/items", new { sku = "CHICKEN", nameAr = "دجاج", nameEn = "Chicken", type = "RawMaterial", baseUnitId = unitId, unitCost = 1m }); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var updated = await client.PutAsJsonAsync($"/api/v1/inventory/items/{id}", new { sku = "CHICKEN", nameAr = "دجاج طازج", nameEn = "Fresh chicken", type = "RawMaterial", baseUnitId = unitId, unitCost = 1.25m, isActive = false });
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        var items = await client.GetFromJsonAsync<JsonElement>("/api/v1/inventory/items"); Assert.False(items[0].GetProperty("isActive").GetBoolean()); Assert.Equal("Fresh chicken", items[0].GetProperty("nameEn").GetString());
        var secondUnit = await client.PostAsJsonAsync("/api/v1/inventory/uoms", new { code = "G", nameAr = "جرام", nameEn = "Gram", sortOrder = 1 });
        var secondId = (await secondUnit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var invalid = await client.PutAsJsonAsync($"/api/v1/inventory/items/{id}", new { sku = "CHICKEN", nameAr = "دجاج", nameEn = "Chicken", type = "RawMaterial", baseUnitId = secondId, unitCost = 1m, isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task User_cannot_deactivate_their_own_account()
    {
        using var factory = new ApiFactory(); var (client, branchId) = await Login(factory);
        var users = await client.GetFromJsonAsync<JsonElement>("/api/v1/users"); var id = users[0].GetProperty("id").GetGuid();
        var response = await client.PutAsJsonAsync($"/api/v1/users/{id}", new { username = "admin", displayName = "Admin", isActive = false, branchIds = new[] { branchId } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
