// Fullscreen kiosk helpers. Kiosk mode hides the app shell and asks the browser for
// fullscreen (user-gesture driven), hides the cursor, disables the context menu and
// best-effort blocks refresh/close shortcuts. Never relied on for anything authoritative.
const preventContext = (event: Event) => event.preventDefault();
const preventKeys = (event: KeyboardEvent) => {
  if (event.key === "F11" || (event.ctrlKey && (event.key.toLowerCase() === "w" || event.key.toLowerCase() === "r"))) event.preventDefault();
};

export async function enterKiosk(): Promise<boolean> {
  try {
    const el = document.documentElement as HTMLElement & { webkitRequestFullscreen?: () => Promise<void> | void };
    if (typeof el.requestFullscreen === "function") {
      await el.requestFullscreen();
    } else if (typeof el.webkitRequestFullscreen === "function") {
      await el.webkitRequestFullscreen();
    } else {
      return false;
    }
    document.documentElement.style.cursor = "none";
    window.addEventListener("contextmenu", preventContext, { passive: false });
    window.addEventListener("keydown", preventKeys, { capture: true });
    return true;
  } catch (error) {
    console.error("Failed to enter kiosk/fullscreen:", error);
    return false;
  }
}

export function exitKiosk(): void {
  try {
    if (document.fullscreenElement && typeof document.exitFullscreen === "function") void document.exitFullscreen();
    else if (typeof (document as unknown as { webkitExitFullscreen?: () => void }).webkitExitFullscreen === "function") (document as unknown as { webkitExitFullscreen: () => void }).webkitExitFullscreen();
  } catch {
    /* ignore */
  }
  document.documentElement.style.cursor = "";
  window.removeEventListener("contextmenu", preventContext);
  window.removeEventListener("keydown", preventKeys, true);
}
