using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OFC.Api;

/// <summary>
/// Tracks whether EF Core migrations have been applied successfully. Used to gate the
/// readiness health check so that dependents (the seed job, the web container) only start
/// once the schema actually exists, instead of racing the startup migration.
/// </summary>
public sealed class MigrationReadiness
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;
}

public sealed class MigrationReadinessCheck(MigrationReadiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(readiness.IsReady
            ? HealthCheckResult.Healthy("Database migrations applied.")
            : HealthCheckResult.Unhealthy("Database migrations not yet applied."));
}
