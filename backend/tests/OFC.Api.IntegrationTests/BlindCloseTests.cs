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

    // Closing with a counted drawer used to fail every time (the denomination rows were UPDATEd instead of
    // INSERTed), leaving the shift open so the next one could not be opened either.
    [Fact]
    public async Task A_shift_closes_with_counted_cash_and_a_new_one_can_then_be_opened()
    {
        using var factory = new ApiFactory();
        var admin = factory.AnonymousClient();
        await admin.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "close-admin", displayName = "Admin", password = "password1234" });
        var adminLogin = await admin.PostAsJsonAsync("/api/v1/auth/login", new { username = "close-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        admin.DefaultRequestHeaders.Authorization = new("Bearer", (await adminLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await admin.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();

        var opened = await admin.PostAsJsonAsync("/api/v1/shifts", new { branchId, openingCash = 20m });
        Assert.True(opened.IsSuccessStatusCode, await opened.Content.ReadAsStringAsync());
        var shiftId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var closed = await admin.PostAsJsonAsync($"/api/v1/shifts/{shiftId}/blind-close", new { actualCash = 25m, actualCardTotal = 0m, denominations = new[] { new { denomination = 20m, count = 1 }, new { denomination = 5m, count = 1 } } });
        Assert.True(closed.IsSuccessStatusCode, await closed.Content.ReadAsStringAsync());
        var current = (await (await admin.GetAsync($"/api/v1/shifts/current?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shift");
        Assert.Equal(JsonValueKind.Null, current.ValueKind);

        var reopened = await admin.PostAsJsonAsync("/api/v1/shifts", new { branchId, openingCash = 25m });
        Assert.True(reopened.IsSuccessStatusCode, await reopened.Content.ReadAsStringAsync());
    }
}
