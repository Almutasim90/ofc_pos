using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Exercises the exact gap every sprint audit finding flagged: authorization branches (who can and
// can't call a permission-gated endpoint) had zero automated coverage anywhere in the repo, only
// pure unit tests of the Rules classes that never touch the HTTP pipeline or the permission claims.
public class AuthorizationTests
{
    [Fact]
    public async Task Admin_created_at_bootstrap_can_list_branches()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "admin1", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin1", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branches = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(branches.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task A_user_with_no_roles_is_forbidden_from_a_permission_gated_endpoint()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "admin2", displayName = "Admin", password = "password1234" });
        var adminLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin2", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var adminToken = (await adminLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var branchesResponse = await client.GetAsync("/api/v1/branches");
        var branchId = (await branchesResponse.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();

        // A brand-new user with zero roles assigned — an active login, but with no permission claims.
        var createUser = await client.PostAsJsonAsync("/api/v1/users", new { username = "no-permissions-user", email = (string?)null, displayName = "No Permissions", password = "password1234", roleIds = Array.Empty<Guid>(), branchIds = new[] { branchId } });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        var plainClient = factory.AnonymousClient();
        var plainLogin = await plainClient.PostAsJsonAsync("/api/v1/auth/login", new { username = "no-permissions-user", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, plainLogin.StatusCode);
        var plainToken = (await plainLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        plainClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", plainToken);

        var response = await plainClient.GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
