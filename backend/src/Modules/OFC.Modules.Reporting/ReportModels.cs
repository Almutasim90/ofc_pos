namespace OFC.Modules.Reporting;

public enum ReportExportStatus
{
    Pending = 0,
    Generating = 1,
    Generated = 2,
    Failed = 3
}

public sealed class ReportExport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public string Format { get; set; } = "csv";
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public ReportExportStatus Status { get; set; } = ReportExportStatus.Pending;
    public int RowCount { get; set; }
    public string? Summary { get; set; }
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
