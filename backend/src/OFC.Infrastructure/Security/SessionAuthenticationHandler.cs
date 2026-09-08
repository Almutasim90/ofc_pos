using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OFC.Infrastructure.Persistence;

namespace OFC.Infrastructure.Security;

public sealed class SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder, OFCDbContext db, TimeProvider timeProvider) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers.Authorization.ToString();
        // A browser WebSocket/SSE connection to a SignalR hub cannot set a custom Authorization header,
        // so the SignalR client instead appends the token as ?access_token=... on the negotiate/connect
        // request; only the /hubs path accepts that form, everything else still requires the header.
        string? token = value.StartsWith("Bearer ", StringComparison.Ordinal) ? value[7..]
            : Request.Path.StartsWithSegments("/hubs") && Request.Query.TryGetValue("access_token", out var qs) ? qs.ToString()
            : null;
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > timeProvider.GetUtcNow(), Context.RequestAborted);
        if (session is null) return AuthenticateResult.Fail("Invalid session.");
        var user = await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive, Context.RequestAborted);
        if (user is null) return AuthenticateResult.Fail("Inactive user.");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName), new("branch_id", session.BranchId?.ToString() ?? ""), new("device_id", session.DeviceId?.ToString() ?? "") };
        claims.AddRange(user.Roles.Select(x => new Claim(ClaimTypes.Role, x.Role.Name)));
        claims.AddRange(user.Roles.SelectMany(x => x.Role.Permissions).Select(x => new Claim("permission", x.Permission.Code)).DistinctBy(x => x.Value));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }
}
