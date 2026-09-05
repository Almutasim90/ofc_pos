using OFC.Modules.Ordering;

namespace OFC.Modules.QrOrdering;

public static class QrRules
{
    public const int CodeMax = 50;
    public const int NameMax = 160;
    public const int NoteMax = 500;
    public const int PhoneMax = 30;
    public const int ExternalIdMax = 80;
    public const int LoyaltyReferenceMax = 80;

    public static readonly string[] ReservedCodes = ["contexts", "orders", "approvals", "menu"];

    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= CodeMax && !ReservedCodes.Contains(code.Trim().ToLowerInvariant());
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMax;
    public static bool ValidOptionalName(string? name) => string.IsNullOrWhiteSpace(name) || name.Trim().Length <= NameMax;
    public static bool ValidNote(string? note) => string.IsNullOrWhiteSpace(note) || note.Trim().Length <= NoteMax;
    public static bool ValidPhone(string? phone) => string.IsNullOrWhiteSpace(phone) || phone.Trim().Length <= PhoneMax;
    public static bool ValidNameOrAnonymity(string? nameAr, string? nameEn) => (string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length <= NameMax) && (string.IsNullOrWhiteSpace(nameEn) || nameEn.Trim().Length <= NameMax);
    public static bool ValidExternalId(string? externalId) => string.IsNullOrWhiteSpace(externalId) || externalId.Trim().Length <= ExternalIdMax;
    public static bool ValidLoyaltyReference(string? reference) => string.IsNullOrWhiteSpace(reference) || reference.Trim().Length <= LoyaltyReferenceMax;
    public static bool ValidContextKind(QrContextKind kind) => kind is QrContextKind.Table or QrContextKind.Parking or QrContextKind.Branch;
    public static bool ValidApprovalMode(QrApprovalMode mode) => mode is QrApprovalMode.None or QrApprovalMode.AutoApprove or QrApprovalMode.RequiresStaffApproval;

    public static bool RequiresStaffApproval(QrApprovalMode mode) => mode == QrApprovalMode.RequiresStaffApproval;

    public static bool CanReviewApproval(QrOrderApprovalStatus status) => status == QrOrderApprovalStatus.Pending;

    public static OrderStatus ResolutionToOrderStatus(QrOrderApprovalStatus status, OrderStatus current) => status switch
    {
        QrOrderApprovalStatus.Approved => current == OrderStatus.Pending ? OrderStatus.Confirmed : current,
        QrOrderApprovalStatus.Rejected => current == OrderStatus.Pending ? OrderStatus.Cancelled : current,
        _ => current
    };
}
