using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// SRS §26 blind close: while a shift is open the cashier must not see the running sales totals (they
// reveal the expected cash); reviewers do.
public class BlindCloseTests
{
    [Fact]
    public async Task Cashier_does_not_see_running_totals_of_an_open_shift_but_a_manager_does()
    {
        using var factory = new ApiFactory();
        var admin = factory.AnonymousClient();
        await admin.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "blind-admin", displayName = "Admin", password = "password1234" });
        var adminLogin = await admin.PostAsJsonAsync("/api/v1/auth/login", new { username = "blind-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        admin.DefaultRequestHeaders.Authorization = new("Bearer", (await adminLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await admin.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();

        var roles = await (await admin.GetAsync("/api/v1/roles")).Content.ReadFromJsonAsync<JsonElement>();
        var cashierRole = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Cashier").GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync("/api/v1/users", new { username = "blind-cashier", email = (string?)null, displayName = "Cashier", password = "password1234", roleIds = new[] { cashierRole }, branchIds = new[] { branchId } })).EnsureSuccessStatusCode();
        var cashier = factory.AnonymousClient();
        var cashierLogin = await cashier.PostAsJsonAsync("/api/v1/auth/login", new { username = "blind-cashier", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        cashier.DefaultRequestHeaders.Authorization = new("Bearer", (await cashierLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());

        var opened = await cashier.PostAsJsonAsync("/api/v1/shifts", new { branchId, openingCash = 20m });
        Assert.True(opened.IsSuccessStatusCode, await opened.Content.ReadAsStringAsync());

        var asCashier = (await (await cashier.GetAsync($"/api/v1/shifts/current?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shift");
        Assert.Equal(JsonValueKind.Null, asCashier.GetProperty("cashSales").ValueKind);
        Assert.Equal(JsonValueKind.Null, asCashier.GetProperty("cardSales").ValueKind);
        Assert.Equal(20m, asCashier.GetProperty("openingCash").GetDecimal());

        var asManager = (await (await admin.GetAsync($"/api/v1/shifts/current?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shift");
        Assert.Equal(JsonValueKind.Number, asManager.GetProperty("cashSales").ValueKind);

        var byId = await cashier.GetAsync($"/api/v1/shifts/{asCashier.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await byId.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSales").ValueKind);
    }
}
