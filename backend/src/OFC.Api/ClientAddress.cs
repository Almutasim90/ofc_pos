using System.Net;

namespace OFC.Api;

// Requests reach the API through Cloudflare -> Traefik -> nginx, so Connection.RemoteIpAddress is the
// nginx container for every caller and per-IP rate limits collapsed into one shared bucket (one person
// failing logins locked everyone out). Cloudflare puts the real client address in CF-Connecting-IP and
// the proxies pass it through. Someone reaching the origin without Cloudflare could forge it, which only
// moves them to a bucket of their choosing; keep the origin reachable through Cloudflare only.
public static class ClientAddress
{
    public const string CloudflareHeader = "CF-Connecting-IP";

    public static string Of(HttpContext context)
    {
        var forwarded = context.Request.Headers[CloudflareHeader].ToString().Trim();
        if (IPAddress.TryParse(forwarded, out var address)) return address.ToString();
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
