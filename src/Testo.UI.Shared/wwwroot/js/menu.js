// The public header's mega menu (ADR-0004).
//
// The menu already works without this file: each section is a <details>, which the browser opens,
// closes and makes keyboard-operable on its own. This adds the three things a <details> does not
// do, and nothing else:
//
//   1. Only one panel open at a time, so two panels never overlap.
//   2. Escape closes the open panel and returns focus to its summary.
//   3. A click or a focus outside the menu closes it.
//
// Nothing here is required for the site to be usable, which is the test to apply to any line
// somebody wants to add.

const menus = () => document.querySelectorAll("[data-site-menu]");

function close(except) {
  for (const menu of menus()) {
    if (menu !== except) {
      menu.open = false;
    }
  }
}

function summaryOf(menu) {
  return menu.querySelector("summary");
}

for (const menu of menus()) {
  // "toggle" fires after the browser has already opened or closed it, so this only tidies up
  // around a state the browser owns.
  menu.addEventListener("toggle", () => {
    if (menu.open) {
      close(menu);
    }
  });
}

document.addEventListener("keydown", (event) => {
  if (event.key !== "Escape") {
    return;
  }

  for (const menu of menus()) {
    if (menu.open) {
      menu.open = false;

      // Focus goes back to where it came from, or it is left stranded in a panel that is no
      // longer there.
      summaryOf(menu)?.focus();
    }
  }
});

document.addEventListener("pointerdown", (event) => {
  const inside = event.target instanceof Element && event.target.closest("[data-site-menu]");

  if (!inside) {
    close(null);
  }
});

// Tabbing out of the last link in a panel should close it, the same as clicking away.
document.addEventListener("focusin", (event) => {
  const inside = event.target instanceof Element && event.target.closest("[data-site-nav]");

  if (!inside) {
    close(null);
  }
});
