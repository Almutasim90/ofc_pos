import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "@/app/App";
// Tajawal is bundled (served from this origin, as the CSP font-src requires) in the weights the UI uses.
import "@fontsource/tajawal/400.css";
import "@fontsource/tajawal/500.css";
import "@fontsource/tajawal/700.css";
import "@fontsource/tajawal/800.css";
import "@fontsource/tajawal/900.css";
import "@/styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

// Installable, fast-starting app shell (SRS §4 PWA). Development keeps plain Vite HMR without a worker.
if (import.meta.env.PROD && "serviceWorker" in navigator) {
  window.addEventListener("load", () => {
    void navigator.serviceWorker.register("/sw.js").catch(() => undefined);
  });
}
