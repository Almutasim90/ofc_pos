namespace OFC.Api;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    // A client-supplied value flows straight into logs and the response header; without validation, an
    // attacker could use it to forge/pollute log entries or send an unbounded string (security review
    // finding L5). Only accept it when it already looks like a GUID we generate ourselves; otherwise
    // mint a fresh one, exactly as if the header had been absent.
    public async Task InvokeAsync(HttpContext context)
    {
        var requested = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(requested) && requested.Length <= 64 && Guid.TryParse(requested, out _)
            ? requested
            : Guid.CreateVersion7().ToString();

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
