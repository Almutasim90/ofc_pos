using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Each test gets its own ApiFactory (own InMemory database), never a shared IClassFixture — bootstrap
// can only ever succeed once per database, so sharing one factory across tests would make every test
// but the first fail with 409 Conflict depending on xUnit's (unspecified) method execution order.
public class AuthenticationTests
{
    [Fact]
    public async Task Health_endpoint_is_reachable_without_authentication()
    {
        using var factory = new ApiFactory();
        var response = await factory.AnonymousClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_a_request_with_no_bearer_token()
    {
        using var factory = new ApiFactory();
        var response = await factory.AnonymousClient().GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_then_login_returns_a_usable_session_token()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        var bootstrap = await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة الاختبار", organizationNameEn = "Test Org", branchNameAr = "الفرع الرئيسي", branchNameEn = "Main Branch", username = "auth-flow-admin", displayName = "Auth Flow Admin", password = "correct-horse-battery" });
        Assert.Equal(HttpStatusCode.Created, bootstrap.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "auth-flow-admin", password = "correct-horse-battery", branchId = (Guid?)null, deviceId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("token").GetString()!.Length > 0);
    }

    [Fact]
    public async Task Login_with_the_wrong_password_is_rejected()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org2", branchNameAr = "فرع", branchNameEn = "Branch2", username = "wrong-pw-admin", displayName = "Admin", password = "the-real-password" });

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "wrong-pw-admin", password = "not-the-real-password", branchId = (Guid?)null, deviceId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task A_second_bootstrap_attempt_is_rejected_once_a_user_exists()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "First", branchNameAr = "فرع", branchNameEn = "Branch", username = "first-admin", displayName = "First Admin", password = "password1234" });

        var second = await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "أخرى", organizationNameEn = "Second", branchNameAr = "فرع2", branchNameEn = "Branch2", username = "second-admin", displayName = "Second Admin", password = "password5678" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
