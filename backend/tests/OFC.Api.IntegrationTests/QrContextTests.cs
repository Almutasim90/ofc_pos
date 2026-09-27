using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
}
