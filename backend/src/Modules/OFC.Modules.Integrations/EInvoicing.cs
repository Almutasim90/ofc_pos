namespace OFC.Modules.Integrations;

// E-invoicing readiness (SRS §74–75). The core POS never talks to a tax authority: a background worker
// issues one invoice record per paid order once a branch enables e-invoicing, and hands it to whichever
// IEInvoiceProvider is registered. No provider is bound by default, so records wait as Pending until one
// is configured — Oman's e-invoicing specifics stay inside a provider, never in the core.

public enum EInvoiceStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Failed = 3
}

public sealed class EInvoiceRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid OrderId { get; set; }
    public Guid InvoiceUuid { get; set; } = Guid.NewGuid();
    public required string InvoiceNumber { get; set; }
    public string? SellerVatin { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal TaxableAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = EInvoiceRules.Currency;
    public EInvoiceStatus Status { get; set; } = EInvoiceStatus.Pending;
    public string? Provider { get; set; }
    public string? ExternalReference { get; set; }
    public string? LastError { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record EInvoiceDocument(
    Guid InvoiceUuid,
    string InvoiceNumber,
    string? SellerVatin,
    DateTimeOffset IssuedAt,
    decimal TaxableAmount,
    decimal VatAmount,
    decimal TotalAmount,
    string Currency,
    Guid BranchId,
    Guid OrderId);

public sealed record EInvoiceSubmission(bool Accepted, bool Retryable, string? ExternalReference, string? Error);

public interface IEInvoiceProvider
{
    string Code { get; }
    Task<EInvoiceSubmission> SubmitAsync(EInvoiceDocument document, CancellationToken ct);
}

public static class EInvoiceRules
{
    public const string Currency = "OMR";
    public const string SellerVatinKey = "einvoice.seller_vatin";
    public const string ProviderKey = "einvoice.provider";
    // Written by the worker the first time it sees a branch enabled: only orders paid after that moment are
    // invoiced, so turning the feature on never back-fills a branch's whole history.
    public const string EnabledSinceKey = "einvoice.enabled_since";
    public const int MaxVatinLength = 30;
    public const int MaxErrorLength = 1000;
    public const int MaxReferenceLength = 120;
    public const int MaxAttempts = 5;
    public const int NoProviderRetryMinutes = 5;

    public static string EnabledKey => IntegrationRules.PreferenceKeyFor(IntegrationKind.EInvoicing);

    // Letters, digits and a few separators; the exact national format is the provider's concern.
    public static bool ValidVatin(string? vatin) =>
        vatin is null
        || (vatin.Trim().Length is > 0 and <= MaxVatinLength && vatin.Trim().All(c => char.IsLetterOrDigit(c) || c is '-' or ' ' or '/'));

    public static EInvoiceDocument Document(EInvoiceRecord record) => new(
        record.InvoiceUuid, record.InvoiceNumber, record.SellerVatin, record.IssuedAt,
        record.TaxableAmount, record.VatAmount, record.TotalAmount, record.Currency, record.BranchId, record.OrderId);

    public static bool CanSubmit(EInvoiceRecord record, DateTimeOffset now) =>
        record.Status == EInvoiceStatus.Pending && (record.NextAttemptAt is null || record.NextAttemptAt <= now);

    public static void Apply(EInvoiceRecord record, string provider, EInvoiceSubmission result, DateTimeOffset now)
    {
        record.Provider = provider;
        record.Attempts += 1;
        record.UpdatedAt = now;
        if (result.Accepted)
        {
            record.Status = EInvoiceStatus.Accepted;
            record.ExternalReference = Truncate(result.ExternalReference, MaxReferenceLength);
            record.SubmittedAt = now;
            record.LastError = null;
            record.NextAttemptAt = null;
            return;
        }
        record.LastError = Truncate(result.Error ?? "Rejected by the e-invoicing provider.", MaxErrorLength);
        if (!result.Retryable) { record.Status = EInvoiceStatus.Rejected; record.NextAttemptAt = null; return; }
        if (record.Attempts >= MaxAttempts) { record.Status = EInvoiceStatus.Failed; record.NextAttemptAt = null; return; }
        record.NextAttemptAt = now.AddSeconds(IntegrationRules.RetryDelaySeconds(record.Attempts));
    }

    public static void WaitForProvider(EInvoiceRecord record, DateTimeOffset now)
    {
        record.LastError = "No e-invoicing provider is configured.";
        record.NextAttemptAt = now.AddMinutes(NoProviderRetryMinutes);
        record.UpdatedAt = now;
    }

    // A failed or rejected invoice can be put back in the queue by staff after fixing the cause.
    public static bool CanRetry(EInvoiceRecord record) => record.Status is EInvoiceStatus.Failed or EInvoiceStatus.Rejected or EInvoiceStatus.Pending;

    public static void Requeue(EInvoiceRecord record, DateTimeOffset now)
    {
        record.Status = EInvoiceStatus.Pending;
        record.Attempts = 0;
        record.NextAttemptAt = null;
        record.UpdatedAt = now;
    }

    private static string? Truncate(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
