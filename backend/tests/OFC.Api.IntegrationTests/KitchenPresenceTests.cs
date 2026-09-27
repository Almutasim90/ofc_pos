using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OFC.Api.Features;
using Xunit;

namespace OFC.Api.IntegrationTests;

public class KitchenPresenceTests
{
    [Fact]
    public void Tracks_connected_kitchen_screens_per_branch_and_when_the_last_one_left()
    {
        var presence = new KitchenPresence(TimeProvider.System);
        var branch = Guid.NewGuid();
        var other = Guid.NewGuid();
        Assert.Equal((0, (DateTimeOffset?)null), presence.Snapshot(branch));

        presence.Joined("tablet-1", branch);
        presence.Joined("tablet-2", branch);
        presence.Joined("elsewhere", other);
        Assert.Equal(2, presence.Snapshot(branch).Screens);

        presence.Left("tablet-1");
        Assert.Equal(1, presence.Snapshot(branch).Screens);
        presence.Left("tablet-2");
        presence.Left("tablet-2"); // a second disconnect notification is harmless
        var (screens, lastSeen) = presence.Snapshot(branch);
        Assert.Equal(0, screens);
        Assert.NotNull(lastSeen);
        Assert.Equal(1, presence.Snapshot(other).Screens);
    }

    [Fact]
    public async Task Register_can_read_kitchen_screen_presence_for_its_branch()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "presence-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "presence-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/v1/kitchen/presence?branchId={branchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("screens").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastSeenAt").ValueKind);

        var anonymous = factory.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/kitchen/presence?branchId={branchId}")).StatusCode);
    }
}
