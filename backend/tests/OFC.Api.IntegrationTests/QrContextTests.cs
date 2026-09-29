using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OFC.Infrastructure.Persistence;
using OFC.Modules.QrOrdering;
using Xunit;
using Xunit.Abstractions;

namespace OFC.Api.IntegrationTests;

// The QR admin screen creates a table QR and lists the branch's QR codes; this sends exactly what the
// screen sends.
public class QrContextTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Admin_screen_payload_creates_a_qr_context_that_is_then_listed()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "qr-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "qr-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var channelId = (await (await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "DINEIN", nameAr = "محلي", nameEn = "Dine in", isActive = true })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync("/api/v1/qr/contexts", new
        {
            branchId,
            salesChannelId = channelId,
            kind = "Table",
            code = "QR-" + Guid.NewGuid().ToString()[..12],
            nameAr = "طاولة 1",
            nameEn = "Table 1",
            approvalMode = "AutoApprove",
            isActive = true,
        });
        var body = await created.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)created.StatusCode} {body}");
        Assert.True(created.StatusCode == HttpStatusCode.Created, body);

        var list = await client.GetAsync($"/api/v1/qr/contexts?branchId={branchId}");
        var listBody = await list.Content.ReadAsStringAsync();
        output.WriteLine($"{(int)list.StatusCode} {listBody}");
        Assert.True(list.StatusCode == HttpStatusCode.OK, listBody);
        Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    // Deleting a QR code: an unused one is removed; one that orders came from is hidden and stops working,
    // and its code can be used again for a new table.
    [Fact]
    public async Task Deleting_a_qr_code_hides_it_stops_it_and_frees_its_code()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "qr-delete", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "qr-delete", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var channelId = (await (await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "DINEIN", nameAr = "محلي", nameEn = "Dine in", isActive = true })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        async Task<Guid> Create(string code)
        {
            var created = await client.PostAsJsonAsync("/api/v1/qr/contexts", new { branchId, salesChannelId = channelId, kind = "Table", code, nameAr = "طاولة", nameEn = "Table", approvalMode = "AutoApprove", isActive = true });
            Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        async Task<int> Listed() => (await (await client.GetAsync($"/api/v1/qr/contexts?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();

        var unused = await Create("T1");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/qr/contexts/{unused}")).StatusCode);
        Assert.Equal(0, await Listed());

        var used = await Create("T1");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            db.QrOrderApprovals.Add(new QrOrderApproval { OrderId = Guid.NewGuid(), QrContextId = used });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/qr/contexts/{used}")).StatusCode);
        Assert.Equal(0, await Listed());
        Assert.Equal(HttpStatusCode.NotFound, (await factory.AnonymousClient().GetAsync("/api/v1/qr/T1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/qr/contexts/{used}")).StatusCode);

        await Create("T1");
        Assert.Equal(1, await Listed());
    }

    // The customer menu once showed every item as "currently unavailable": the product's own branch
    // availability was never loaded, so the flag always came back false. Sold-out items stay listed.
    [Fact]
    public async Task Customer_menu_marks_available_items_available_and_keeps_sold_out_items_listed()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "qr-menu", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "qr-menu", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var channelId = (await (await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "DINEIN", nameAr = "محلي", nameEn = "Dine in", isActive = true })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var categoryId = (await (await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "وجبات", nameEn = "Meals", sortOrder = 0 })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        async Task Product(string sku, bool available)
        {
            var created = await client.PostAsJsonAsync("/api/v1/products", new { sku, barcode = (string?)null, nameAr = sku, nameEn = sku, categoryId, type = "Simple", basePrice = 2m, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = available } } });
            Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        }
        await Product("BURGER", true);
        await Product("SOLDOUT", false);
        var qr = await client.PostAsJsonAsync("/api/v1/qr/contexts", new { branchId, salesChannelId = channelId, kind = "Table", code = "M1", nameAr = "طاولة", nameEn = "Table", approvalMode = "AutoApprove", isActive = true });
        Assert.True(qr.StatusCode == HttpStatusCode.Created, await qr.Content.ReadAsStringAsync());

        var menu = await factory.AnonymousClient().GetFromJsonAsync<JsonElement>("/api/v1/qr/M1/menu");
        var available = menu.GetProperty("products").EnumerateArray().ToDictionary(p => p.GetProperty("sku").GetString()!, p => p.GetProperty("isAvailable").GetBoolean());
        Assert.True(available["BURGER"]);
        Assert.False(available["SOLDOUT"]);
    }
}
