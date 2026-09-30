using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Behind Cloudflare -> Traefik -> nginx every request came from the nginx container, so one person's
// failed logins used up the login limit for the whole restaurant. Limits are now per real client
// address (CF-Connecting-IP).
public class ClientAddressRateLimitTests
{
    [Fact]
    public async Task One_client_hitting_the_login_limit_does_not_lock_out_another()
    {
        using var factory = new ApiFactory();
        async Task<HttpStatusCode> Attempt(string ip)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { username = "nobody", password = "wrong-password" }) };
            request.Headers.Add("CF-Connecting-IP", ip);
            return (await factory.AnonymousClient().SendAsync(request)).StatusCode;
        }

        for (var i = 0; i < 10; i++) Assert.NotEqual(HttpStatusCode.TooManyRequests, await Attempt("203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Attempt("203.0.113.7"));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await Attempt("198.51.100.20"));
    }
}
