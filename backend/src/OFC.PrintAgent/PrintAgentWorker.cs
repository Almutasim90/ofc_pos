using Microsoft.Extensions.Options;

namespace OFC.PrintAgent;

public sealed class PrintAgentWorker(PrintApiClient api, IOptions<PrintAgentOptions> options, ILogger<PrintAgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        logger.LogInformation("Print agent started for branch {BranchId}, polling every {Interval}s.", options.Value.BranchId, interval.TotalSeconds);
        do
        {
            try { await PollOnce(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Print agent poll failed."); }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnce(CancellationToken ct)
    {
        var configs = (await api.GetConfigsAsync(ct)).ToDictionary(x => x.Id);
        // Heartbeat every active printer every cycle, whether or not there's a job — this is what makes
        // "online" a real, timed-out signal (PrintingRules.IsHealthy) instead of a manually-toggled badge.
        foreach (var config in configs.Values.Where(x => x.IsActive))
        {
            var transportExists = PrinterTransportFactory.Create(config.DeviceName) is not null;
            await api.HeartbeatAsync(config.Id, transportExists ? null : $"Cannot address printer device '{config.DeviceName}'.", ct);
        }

        var jobs = await api.GetActionableJobsAsync(ct);
        if (jobs.Count == 0) return;
        var templates = (await api.GetTemplatesAsync(ct)).ToDictionary(x => x.Code);

        foreach (var job in jobs)
        {
            if (!await api.ClaimAsync(job.Id, ct)) continue; // another agent instance claimed it first, or it's not due yet
            try
            {
                var config = job.PrinterConfigurationId is Guid configId && configs.TryGetValue(configId, out var found) ? found : null;
                var transport = PrinterTransportFactory.Create(config?.DeviceName);
                if (transport is null) { await api.FailAsync(job.Id, config is null ? "No printer is routed for this job." : $"Cannot address printer device '{config.DeviceName}'.", ct); continue; }

                var template = job.TemplateCode is not null && templates.TryGetValue(job.TemplateCode, out var t) ? t : null;
                var bytes = EscPosRenderer.Render(job.Kind, template?.Content, template?.WidthChars ?? 42, job.Payload);
                await transport.SendAsync(bytes, ct);
                await api.CompleteAsync(job.Id, ct);
                logger.LogInformation("Printed job {JobId} ({Kind}) to {Device}.", job.Id, job.Kind, config?.DeviceName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to print job {JobId}.", job.Id);
                await api.FailAsync(job.Id, ex.Message, ct);
            }
        }
    }
}
