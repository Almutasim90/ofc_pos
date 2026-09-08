using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace OFC.PrintAgent;

public sealed record PrintJobDto(Guid Id, Guid BranchId, Guid? PrinterConfigurationId, string Kind, string Status, string? TemplateCode, string Payload, int AttemptCount, int MaxAttempts);
public sealed record PrinterConfigDto(Guid Id, string Code, string NameAr, string NameEn, string Kind, string? DeviceName, bool IsActive);
public sealed record PrintTemplateDto(Guid Id, string Code, string NameAr, string NameEn, string Kind, int WidthChars, string Content, bool IsActive);

public sealed class PrintApiClient
{
    private readonly HttpClient http;
    private readonly Guid branchId;

    public PrintApiClient(HttpClient http, IOptions<PrintAgentOptions> options)
    {
        var opts = options.Value;
        http.BaseAddress = new Uri(opts.ApiBaseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.SessionToken);
        this.http = http;
        branchId = opts.BranchId;
    }

    public async Task<List<PrintJobDto>> GetActionableJobsAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<PrintJobDto>>($"api/v1/print/jobs?branchId={branchId}&actionable=true", ct) ?? [];

    public async Task<List<PrinterConfigDto>> GetConfigsAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<PrinterConfigDto>>($"api/v1/print/configs?branchId={branchId}", ct) ?? [];

    public async Task<List<PrintTemplateDto>> GetTemplatesAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<PrintTemplateDto>>($"api/v1/print/templates?branchId={branchId}", ct) ?? [];

    public async Task<bool> ClaimAsync(Guid jobId, CancellationToken ct) =>
        (await http.PostAsync($"api/v1/print/jobs/{jobId}/claim", null, ct)).IsSuccessStatusCode;

    public Task CompleteAsync(Guid jobId, CancellationToken ct) =>
        http.PostAsync($"api/v1/print/jobs/{jobId}/complete", null, ct);

    public Task FailAsync(Guid jobId, string error, CancellationToken ct) =>
        http.PostAsJsonAsync($"api/v1/print/jobs/{jobId}/fail", new { error }, ct);

    public Task HeartbeatAsync(Guid printerConfigId, string? error, CancellationToken ct) =>
        http.PostAsJsonAsync($"api/v1/print/configs/{printerConfigId}/heartbeat", new { error }, ct);
}
