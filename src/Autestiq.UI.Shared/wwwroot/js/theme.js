// Instant theme switching (ADR-0004).
//
// The theme switch is a plain form. Without this script it posts to the server, which
// stores the choice in a cookie and reloads the page. With this script the choice is
// applied at once and the same cookie is written here, so nothing reloads and no state is
// lost. The server keeps rendering the right theme on later requests from that cookie.

const root = document.documentElement;

function isExplicit(choice) {
  return choice === "light" || choice === "dark";
}

function apply(choice) {
  if (isExplicit(choice)) {
    root.dataset.theme = choice;
  } else {
    delete root.dataset.theme;
  }

  const meta = document.querySelector('meta[name="color-scheme"]');
  if (meta) {
    meta.content = isExplicit(choice) ? choice : "light dark";
  }

  for (const button of document.querySelectorAll('[data-theme-switch] button[name="theme"]')) {
    button.setAttribute("aria-pressed", String(button.value === choice));
  }

  // Fluent UI components (app pages only) keep their own theme state. Tell them, then
  // drop what they stored: the cookie stays the single source of truth.
  const fluent = globalThis.Blazor?.theme;
  if (fluent) {
    fluent.setThemeMode(choice);
    fluent.clearStoredThemeSettings();
  }
}

function store(cookieName, choice) {
  const secure = location.protocol === "https:" ? "; Secure" : "";
  // "Same as system" is the default and needs no cookie at all.
  const value = isExplicit(choice) ? `${choice}; Max-Age=31536000` : "; Max-Age=0";
  document.cookie = `${cookieName}=${value}; Path=/; SameSite=Lax${secure}`;
}

document.addEventListener("submit", (event) => {
  const form = event.target;
  const choice = event.submitter?.value;
  if (!(form instanceof HTMLFormElement) || !form.matches("[data-theme-switch]") || !choice) {
    return;
  }

  event.preventDefault();
  store(form.dataset.themeCookie, choice);
  apply(choice);
});
