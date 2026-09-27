// Applies the saved theme and language direction before the first paint, so the page never flashes the
// wrong colours or reading direction. It is a same-origin file (not inline) so the production
// Content-Security-Policy `script-src 'self'` allows it.
(function () {
  var root = document.documentElement;
  try {
    var saved = JSON.parse(localStorage.getItem("ofc:theme") || '"system"');
    var mode = saved === "light" || saved === "dark" ? saved : "system";
    var resolved =
      mode === "system"
        ? matchMedia("(prefers-color-scheme: dark)").matches
          ? "dark"
          : "light"
        : mode;
    root.dataset.themeMode = mode;
    root.dataset.theme = resolved;
  } catch (error) {
    root.dataset.theme = "light";
  }
  try {
    if (JSON.parse(localStorage.getItem("ofc:language") || '"ar"') === "en") {
      root.lang = "en";
      root.dir = "ltr";
    }
  } catch (error) {
    /* Arabic RTL stays the default. */
  }
})();
