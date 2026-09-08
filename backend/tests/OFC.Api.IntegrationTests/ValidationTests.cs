using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

public class ValidationTests
{
    private static async Task<System.Net.Http.HttpClient> AdminClient(ApiFactory factory)
    {
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "validation-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "validation-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Creating_a_category_with_a_blank_name_returns_a_validation_problem()
    {
        using var factory = new ApiFactory();
        var client = await AdminClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "", nameEn = "", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("nameAr", out _));
    }

    [Fact]
    public async Task Creating_a_category_with_valid_names_succeeds()
    {
        using var factory = new ApiFactory();
        var client = await AdminClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "مشروبات", nameEn = "Beverages", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
