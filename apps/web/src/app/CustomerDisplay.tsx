import { useEffect, useRef, useState } from "react";
import { CheckCircle2 } from "lucide-react";
import {
  openDisplayChannel,
  type DisplayMessage,
} from "@/lib/customer-display";
import { store } from "@/lib/local-store";

const copy = {
  ar: {
    welcome: "أهلاً بكم",
    welcomeNote: "سيظهر طلبك هنا أثناء تسجيله",
    order: "طلبك",
    subtotal: "المجموع الفرعي",
    discount: "الخصم",
    tax: "الضريبة ضمن الإجمالي",
    total: "الإجمالي",
    thanks: "شكراً لكم",
    paid: "المبلغ المدفوع",
    items: "صنف",
  },
  en: {
    welcome: "Welcome",
    welcomeNote: "Your order will appear here as it is rung up",
    order: "Your order",
    subtotal: "Subtotal",
    discount: "Discount",
    tax: "Tax included",
    total: "Total",
    thanks: "Thank you",
    paid: "Amount paid",
    items: "items",
  },
};

// How long the thank-you screen stays before returning to the welcome screen.
const THANKS_MS = 7000;

export function CustomerDisplay() {
  const [state, setState] = useState<
    Exclude<DisplayMessage, { kind: "hello" }>
  >(() => ({
    kind: "idle",
    language: store.get<string>("language") === "en" ? "en" : "ar",
  }));
  const thanksUntil = useRef(0);
  const listRef = useRef<HTMLOListElement>(null);

  useEffect(() => {
    const channel = openDisplayChannel((message) => {
      if (message.kind === "hello") return;
      // Keep the thank-you screen up while the register clears its cart after payment.
      if (
        message.kind !== "paid" &&
        Date.now() < thanksUntil.current &&
        !(message.kind === "cart" && message.lines.length)
      )
        return;
      if (message.kind === "paid") thanksUntil.current = Date.now() + THANKS_MS;
      setState(message);
    });
    // Ask the register for its current cart so a freshly opened display is correct immediately.
    channel?.post({ kind: "hello" });
    return () => channel?.close();
  }, []);

  useEffect(() => {
    if (state.kind !== "paid") return;
    const timer = setTimeout(
      () => setState({ kind: "idle", language: state.language }),
      THANKS_MS,
    );
    return () => clearTimeout(timer);
  }, [state]);

  const language = state.language;
  const t = copy[language];
  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = language === "ar" ? "rtl" : "ltr";
    document.title = `OFC · ${t.welcome}`;
  }, [language, t.welcome]);

  // Keep the newest line in view.
  const lineCount = state.kind === "cart" ? state.lines.length : 0;
  useEffect(() => {
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [lineCount]);

  const money = (value: number) =>
    new Intl.NumberFormat(language, {
      style: "currency",
      currency: "OMR",
      minimumFractionDigits: 3,
      maximumFractionDigits: 3,
    }).format(value);

  return (
    <main className="cd-shell">
      <header className="cd-header">
        <span className="cd-mark" aria-hidden="true">
          OFC
        </span>
        <span className="cd-brand">Oman Fried Chicken</span>
      </header>

      {state.kind === "idle" && (
        <section className="cd-center" aria-live="polite">
          <span className="cd-mark cd-mark-large" aria-hidden="true">
            OFC
          </span>
          <h1>{t.welcome}</h1>
          <p>{t.welcomeNote}</p>
        </section>
      )}

      {state.kind === "paid" && (
        <section className="cd-center cd-thanks" aria-live="polite">
          <CheckCircle2 size={96} strokeWidth={1.5} aria-hidden="true" />
          <h1>{t.thanks}</h1>
          <p>
            {t.paid}: <strong dir="ltr">{money(state.total)}</strong>
          </p>
        </section>
      )}

      {state.kind === "cart" && (
        <section className="cd-order" aria-live="polite">
          <div className="cd-lines">
            <h2>
              {t.order}
              <span>
                {state.lines.reduce((sum, line) => sum + line.quantity, 0)}{" "}
                {t.items}
              </span>
            </h2>
            <ol ref={listRef}>
              {state.lines.map((line, index) => (
                <li
                  key={line.key}
                  className={
                    index === state.lines.length - 1 ? "is-latest" : ""
                  }
                >
                  <span className="cd-qty">{line.quantity}×</span>
                  <span className="cd-name">
                    {line.name}
                    {line.details.length > 0 && (
                      <small>{line.details.join(" · ")}</small>
                    )}
                  </span>
                  <span className="cd-amount" dir="ltr">
                    {money(line.amount)}
                  </span>
                </li>
              ))}
            </ol>
          </div>
          <aside className="cd-totals">
            <dl>
              <div>
                <dt>{t.subtotal}</dt>
                <dd dir="ltr">{money(state.subtotal)}</dd>
              </div>
              {state.discount > 0 && (
                <div>
                  <dt>{t.discount}</dt>
                  <dd dir="ltr">− {money(state.discount)}</dd>
                </div>
              )}
              <div>
                <dt>{t.tax}</dt>
                <dd dir="ltr">{money(state.tax)}</dd>
              </div>
            </dl>
            <div className="cd-total">
              <span>{t.total}</span>
              <strong dir="ltr">{money(state.total)}</strong>
            </div>
          </aside>
        </section>
      )}
    </main>
  );
}
