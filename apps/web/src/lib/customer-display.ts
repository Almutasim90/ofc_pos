// Customer-facing display (SRS §57): a second window on the same till mirrors the cart. The register and
// the display talk over a same-origin BroadcastChannel, so nothing leaves the device and no server call or
// login is involved; the display only ever renders what the register sends.

export type DisplayLine = {
  key: string;
  name: string;
  quantity: number;
  unit: number;
  amount: number;
  details: string[];
};

export type DisplayMessage =
  | {
      kind: "cart";
      language: "ar" | "en";
      lines: DisplayLine[];
      subtotal: number;
      discount: number;
      tax: number;
      total: number;
    }
  | { kind: "idle"; language: "ar" | "en" }
  | { kind: "paid"; language: "ar" | "en"; total: number }
  | { kind: "hello" };

const CHANNEL = "ofc-customer-display";

export function openDisplayChannel(
  onMessage: (message: DisplayMessage) => void,
): { post: (message: DisplayMessage) => void; close: () => void } | null {
  if (typeof BroadcastChannel === "undefined") return null;
  const channel = new BroadcastChannel(CHANNEL);
  channel.onmessage = (event: MessageEvent<DisplayMessage>) =>
    onMessage(event.data);
  return {
    post: (message) => channel.postMessage(message),
    close: () => channel.close(),
  };
}

export function openCustomerDisplayWindow() {
  // A named window is reused, so pressing the button again just focuses the existing display.
  const display = window.open(
    "/#/customer-display",
    "ofc-customer-display",
    "popup,width=1280,height=800",
  );
  display?.focus();
  return display !== null;
}
