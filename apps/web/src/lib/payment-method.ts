export function paymentMethodName(
  language: "ar" | "en",
  method: { nameAr?: string | null; nameEn?: string | null; code?: string | null; kind?: string | null },
) {
  const isLegacyOmanNet = method.code?.toUpperCase() === "OMANNET"
    || method.kind === "OmanNet"
    || method.nameEn?.replace(/\s/g, "").toLowerCase() === "omannet"
    || method.nameAr?.replace(/\s/g, "") === "عماننت";

  if (isLegacyOmanNet) return language === "ar" ? "بطاقة" : "Card";
  return language === "ar"
    ? (method.nameAr ?? method.nameEn ?? "—")
    : (method.nameEn ?? method.nameAr ?? "—");
}
