import { useEffect, useState } from "react";
import QRCode from "qrcode";

export function QrCodeCard({ code, name, language }: { code: string; name: string; language: "ar" | "en" }) {
  const [image, setImage] = useState("");
  const [failed, setFailed] = useState(false);
  const url = `${window.location.origin}/#/qr/${encodeURIComponent(code)}`;
  const ar = language === "ar";
  useEffect(() => {
    let live = true; setFailed(false); setImage("");
    QRCode.toDataURL(url, { width: 512, margin: 4, errorCorrectionLevel: "M" }).then(value => { if (live) setImage(value); }).catch(() => { if (live) setFailed(true); });
    return () => { live = false; };
  }, [url]);
  return <div className="flex w-full flex-wrap items-center gap-4 rounded-xl border bg-white p-3">
    {image && <img src={image} alt={`${ar ? "رمز الطلب" : "Ordering QR"}: ${name}`} width={144} height={144} className="h-36 w-36" />}
    <div className="min-w-0 flex-1"><p className="text-sm text-[#64716b]">{ar ? "يمسح العميل الرمز لعرض القائمة وإرسال طلبه." : "Customers scan to browse the menu and place an order."}</p><div className="mt-3 flex flex-wrap gap-2"><a href={url} target="_blank" rel="noreferrer" className="inline-flex min-h-11 items-center rounded-lg border px-3 text-sm font-semibold text-[#0e5a4f]">{ar ? "فتح قائمة العميل" : "Open customer menu"}</a>{image && <a href={image} download={`OFC-QR-${code}.png`} className="inline-flex min-h-11 items-center rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{ar ? "تنزيل QR للطباعة" : "Download printable QR"}</a>}</div></div>
    {failed && <p role="alert">{ar ? "تعذر إنشاء الصورة. حدّث الصفحة وحاول مجددًا." : "Unable to generate the image. Refresh and retry."}</p>}
  </div>;
}
