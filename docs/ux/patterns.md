# Interaction patterns

How the interface behaves, so that every screen behaves the same way. Check this list
before inventing anything; adding a pattern here is a decision, and it needs a reason.

The visual side of these — colors, spacing, components — is in
[`../design-system.md`](../design-system.md).

---

## 1. The five states of anything that loads

Every view that fetches data has all five designed, not just the happy one. A view shipped
with only the data state is not finished.

| State | What it shows |
|---|---|
| **Loading** | A skeleton in the shape of the content, not a spinner. The page does not jump when data arrives. |
| **Empty, first time** | What this screen is for, and the one action that fills it. Never just "No data". |
| **Empty, filtered** | "No defects match these filters", with a way to clear them. Different from above: the data exists. |
| **Error** | What failed, whether it is worth retrying, and a retry. Never a raw status code. |
| **Data** | The content. |

Two more that matter in a tool this size:

- **Partial** — some of the page loaded and one region failed. That region shows its own
  error; the rest stays usable. One failing widget never takes the dashboard down.
- **Stale** — data older than the live run or defect it describes is marked as such, with
  a refresh. Quietly showing old numbers is worse than showing none.

## 2. Forms

- **Labels are always visible**, above the field. Never a placeholder as a label.
- **Validate on leaving a field**, not on every keystroke; re-validate as they fix it.
- The error sits **under the field**, in words that say what to do: "Enter a date on or
  after the start date", not "Invalid input".
- On a failed submit, focus moves to the first field with an error and a summary at the
  top links to each one.
- **Never ask twice** for something the product already knows (3.3.7 Redundant Entry).
- **Long forms save drafts.** A test case half written survives a reload.
- **Submit is never the only way out.** Cancel says what happens to unsaved work.

## 3. Tables and grids

The product is full of lists: test cases, runs, defects, requirements.

- **Sort, filter and page in the URL**, so a filtered view can be shared and bookmarked.
- The **header stays visible** when scrolling. Wide tables scroll inside their own region,
  never the whole page.
- **Bulk actions** appear only when rows are selected, and say how many are affected
  before doing anything.
- **Row click opens the detail.** Actions inside a row are buttons, and never sit where a
  mis-click would hit them.
- **Density is a user setting**, remembered per person. Nadia wants more rows than Claudia.
- Numbers are tabular-aligned and right-aligned; IDs are monospaced.

## 4. Status, severity and other categories

- **Never color alone.** Always an icon or a word with it. A red dot means nothing to a
  colorblind reader or in a printed report.
- The same status looks the same everywhere: a failed test is the same badge in a list, a
  detail page and a dashboard.
- Status wording comes from [`content.md`](content.md) and matches ISTQB terminology.

## 5. Feedback after an action

| What happened | How it is shown |
|---|---|
| Small, successful, visible on screen | Nothing. The change itself is the feedback. |
| Successful, effect off screen | A toast, with the object named and a link to it |
| Failed | Inline, next to what failed, with what to do |
| Slow (over about a second) | Progress where the action was triggered, and the control is disabled with a reason |
| Destructive, reversible | A toast with **Undo**, for about ten seconds |
| Destructive, irreversible | A confirmation that names the object and its consequence |

A toast never carries the only copy of information, and never carries an error that
needs action — those stay on the page.

## 6. Destructive and irreversible actions

- Confirmation names **the thing**: "Delete test suite *Checkout — regression*?", not
  "Are you sure?".
- It states the consequence, including what else is affected: "47 test cases stay; their
  membership of this suite is removed."
- The confirming button is the **verb** ("Delete suite"), never "OK".
- **Prefer undo to confirmation** where it is possible. Confirmation dialogs stop being
  read; undo always works.
- Removing a person's access, deleting a project, and anything affecting another user is
  always confirmed and always written to the audit log.

## 7. AI suggestions

The product's reason for existing, and the easiest thing to get wrong.

- Anything generated carries a **visible AI badge** until a person accepts it.
- Every suggestion says **what it was based on** — the requirement, the run, the history —
  as a link, not a claim.
- **Accept, edit, dismiss** are all one action away. Editing then accepting is the normal
  path, not an exception.
- Accepting records **who** and **when**. The badge disappears; the history does not.
- Generating is **interruptible**, and partial results are kept.
- When AI is unavailable, the manual path is visible and complete. The product never
  becomes unusable because a model is down.
- The product **never states a confidence it does not have**. No invented percentages.

## 8. Navigation and context

- **Where am I**: the current module is marked in the navigation, and breadcrumbs show the
  path within a project.
- **Which project**: always visible in the top bar. Switching keeps you on the equivalent
  page where one exists, rather than dumping you on the dashboard.
- **Deep links always work.** Every page is addressable, and reaching it signed-out routes
  through sign-in and back. Jonas depends on this entirely.
- Opening a detail from a list offers a way **back to that list with its filters intact**.

## 9. Permissions

- **Hide what a person cannot use; disable only when the absence would be confusing.** A
  disabled control always says why on hover and to a screen reader.
- A page someone may not see returns a clear "you do not have access, ask your
  administrator" — never a blank page and never a 500.
- Permissions are checked on the server. The interface reflects them; it never enforces them.

## 10. Long-running work

- The live test run is the model: show progress as it happens, never a frozen screen.
- Leaving the page does not stop the work. Coming back shows the current state.
- Connection lost is shown honestly, with reconnection attempted and the state caught up
  rather than restarted.

## 11. Keyboard and accessibility

Beyond the WCAG rules in [`../design-system.md`](../design-system.md) §10:

- Every list has a keyboard path to open a row and move between rows.
- A global search opens with `Ctrl`/`⌘ + K` from anywhere.
- Dialogs trap focus, close on `Escape`, and return focus to what opened them.
- No action requires a hover: anything on hover is also reachable by keyboard and visible
  on touch.
