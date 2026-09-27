// Kitchen slip printed from the cashier's browser: the paper fallback when the kitchen tablet is down or
// the kitchen asks for a copy. Sized for 80mm thermal paper but prints fine on A4. Chrome started with
// --kiosk-printing sends it straight to the default printer without a dialog.

export type KitchenSlipLine = {
  quantity: number;
  name: string;
  choices: string[];
  note: string | null;
};

export type KitchenSlip = {
  title: string;
  reference: string;
  table: string | null;
  createdAt: string;
  note: string | null;
  lines: KitchenSlipLine[];
  language: "ar" | "en";
};

type SnapshotChoice = {
  NameAr?: string;
  NameEn?: string;
  nameAr?: string;
  nameEn?: string;
  Quantity?: number;
  quantity?: number;
};
type SnapshotGroup = { choices?: SnapshotChoice[]; Choices?: SnapshotChoice[] };

// Order lines store their chosen modifiers as a JSON snapshot; the server may serialize it in either case.
export function snapshotChoices(
  snapshot: string | null | undefined,
  language: "ar" | "en",
) {
  if (!snapshot) return [];
  try {
    const groups = JSON.parse(snapshot) as SnapshotGroup[];
    return groups.flatMap((group) =>
      (group.choices ?? group.Choices ?? []).flatMap((choice) => {
        const name =
          language === "ar"
            ? (choice.NameAr ?? choice.nameAr ?? choice.NameEn ?? choice.nameEn)
            : (choice.NameEn ??
              choice.nameEn ??
              choice.NameAr ??
              choice.nameAr);
        if (!name) return [];
        const quantity = choice.Quantity ?? choice.quantity ?? 1;
        return [quantity > 1 ? `${quantity} × ${name}` : name];
      }),
    );
  } catch {
    return [];
  }
}

const escape = (value: string) =>
  value.replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ]!,
  );

function slipBody(slip: KitchenSlip) {
  const ar = slip.language === "ar";
  const time = new Date(slip.createdAt).toLocaleString(ar ? "ar-OM" : "en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
  });
  const lines = slip.lines
    .map(
      (line) => `<li>
        <div class="row"><strong class="qty">${line.quantity}×</strong><strong>${escape(line.name)}</strong></div>
        ${line.choices.map((c) => `<div class="sub">+ ${escape(c)}</div>`).join("")}
        ${line.note ? `<div class="sub note">✎ ${escape(line.note)}</div>` : ""}
      </li>`,
    )
    .join("");
  return `<section class="slip">
  <h1>${escape(slip.title)}</h1>
  <div class="ref">${escape(slip.reference)}</div>
  <div class="meta"><span>${slip.table ? escape(slip.table) : ""}</span><span>${escape(time)}</span></div>
  <ul>${lines}</ul>
  ${slip.note ? `<div class="order-note">✎ ${escape(slip.note)}</div>` : ""}
</section>`;
}

export function printKitchenSlip(slip: KitchenSlip) {
  return printKitchenSlips([slip]);
}

// Several slips go out as ONE print job (one dialog), each on its own page/cut.
export function printKitchenSlips(slips: KitchenSlip[]) {
  if (!slips.length) return false;
  const first = slips[0];
  const ar = first.language === "ar";
  const html = `<!doctype html><html lang="${first.language}" dir="${ar ? "rtl" : "ltr"}"><head><meta charset="utf-8">
<title>${escape(slips.map((slip) => slip.reference).join(" "))}</title>
<style>
  @page { size: 80mm auto; margin: 4mm; }
  * { box-sizing: border-box; }
  body { margin: 0; font-family: Tahoma, Arial, sans-serif; color: #000; font-size: 14px; }
  .slip + .slip { break-before: page; page-break-before: always; }
  h1 { margin: 0; font-size: 16px; text-align: center; }
  .ref { margin: 4px 0; font-size: 28px; font-weight: 900; text-align: center; }
  .meta { display: flex; justify-content: space-between; font-size: 12px; border-bottom: 2px dashed #000; padding-bottom: 6px; }
  ul { list-style: none; margin: 8px 0; padding: 0; }
  li { padding: 6px 0; border-bottom: 1px dashed #000; }
  .row { display: flex; gap: 8px; font-size: 16px; }
  .qty { min-width: 32px; }
  .sub { padding-inline-start: 40px; font-size: 13px; }
  .note { font-weight: 700; }
  .order-note { margin-top: 6px; font-weight: 700; }
</style></head><body>
${slips.map(slipBody).join("\n")}
</body></html>`;

  const frame = document.createElement("iframe");
  frame.setAttribute("aria-hidden", "true");
  frame.style.cssText =
    "position:fixed;width:0;height:0;border:0;inset-inline-start:-9999px";
  document.body.appendChild(frame);
  const doc = frame.contentDocument;
  if (!doc) {
    frame.remove();
    return false;
  }
  doc.open();
  doc.write(html);
  doc.close();
  // Give the frame a tick to lay out before printing, then clean up once the dialog closes.
  setTimeout(() => {
    frame.contentWindow?.focus();
    frame.contentWindow?.print();
    setTimeout(() => frame.remove(), 1000);
  }, 50);
  return true;
}
