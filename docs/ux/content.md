# Writing in the product

Every string a person reads. Text is designed, not added at the end.

Everything here exists in **English and German**, always together.

---

## Voice

Calm, precise, and on the reader's side. The tone of a good colleague who knows the
subject: never excited, never apologetic, never clever.

| Do | Don't |
|---|---|
| "No test cases yet. Generate them from a requirement, or add one by hand." | "Oops! Nothing here yet 🎉" |
| "Could not reach the test environment." | "Something went wrong!" |
| "47 of 52 tests passed." | "Great job! Almost all tests passed!" |
| "This run stopped after step 7." | "Unfortunately, we had to stop your run." |

Specifically:

- **No exclamation marks.** None.
- **No apologising.** "Could not save the test case" is more useful than "Sorry, something went wrong".
- **No blame.** "That date is before the start date", not "You entered an invalid date".
- **No hype.** Never "powerful", "seamless", "effortless" — not even on the marketing site.
- **No emoji** in the product.
- The product does not call itself "we", and does not refer to itself in the first person.
  It describes what happened: "The run stopped", not "We stopped your run".

## Microcopy rules

- **Buttons are verbs**, naming the action: "Create defect", "Start run", "Invite people".
  Never "OK", "Submit" or "Yes".
- **Titles are nouns**: "Test cases", "Release readiness".
- **Sentence case everywhere** — headings, buttons, labels, table headers. Never Title Case.
- **Numbers**: digits always, even under ten. "3 open defects".
- **Dates** follow the reader's language. Never a bare `03/04/2026` — it is ambiguous.
  Relative time for recent things ("12 minutes ago") with the exact time on hover.
- **Empty states** have three parts: what belongs here, why it is empty, and the one action
  that fills it.
- **Error messages** have three parts: what failed, what it means, what to do. Technical
  detail goes behind "Details", for Jonas and Nadia; the first line is for everyone.
- **Truncation**: never in the middle of meaning. Full text on hover and to screen readers.

## German

- **"Sie", always.** Enterprise software sold to German businesses; "du" would be wrong for
  this audience. The existing texts already follow this.
- **Translate, do not transliterate.** German is roughly 30 % longer: the layout is built
  for it, and the German text is written as German, not as a word-for-word copy.
- **Keep established English terms** where the industry uses them. German QA teams say
  "Bug", "Release", "Sprint", "Dashboard" and "Deployment" in German sentences; forcing
  "Fehlerzustand" or "Veröffentlichung" reads as a bad translation.
- Compound nouns are written as one word: "Testfallbeschreibung". Watch the line length in
  narrow columns.
- Formal register, but not bureaucratic. "Bitte wählen Sie ein Testobjekt" — not
  "Es ist ein Testobjekt auszuwählen".

## Terminology

One word per concept, everywhere: in the interface, the documentation and the code. These
follow **ISTQB** usage, which our users already know from their certification.

| English | German | Not |
|---|---|---|
| Test case | Testfall | Test, Testskript |
| Test condition | Testbedingung | Testziel |
| Test suite | Testsuite | Testpaket |
| Test run | Testlauf | Ausführung, Durchlauf |
| Test plan | Testplan | — |
| Test level | Teststufe | Testebene |
| Test environment | Testumgebung | System, Stage |
| Defect | Fehlerzustand *(short form: Fehler)* | Bug in the product; "Bug" is fine in conversation |
| Failure | Fehlerwirkung | — |
| Severity | Schweregrad | Kritikalität |
| Priority | Priorität | Dringlichkeit |
| Requirement | Anforderung | Spezifikation |
| Traceability | Nachverfolgbarkeit | Rückverfolgbarkeit |
| Coverage | Abdeckung | Überdeckung |
| Entry / exit criteria | Eingangs- / Endekriterien | Start- / Abbruchkriterien |
| Release readiness | Release-Bereitschaft | Freigabereife |
| Test report | Testbericht | Auswertung |
| Project | Projekt | Produkt, Workspace |
| Organization | Organisation | Firma, Mandant, Tenant |

Status words, which appear constantly and must never drift:

| English | German |
|---|---|
| Passed | Bestanden |
| Failed | Fehlgeschlagen |
| Blocked | Blockiert |
| Skipped | Übersprungen |
| Not run | Nicht ausgeführt |
| Running | Läuft |
| Flaky | Instabil |

## How this is enforced

- Every text lives in a `.resx` file next to the code that uses it, in both languages.
- `ResourceFileTests` fails the build if German is missing a key, if a key is unused, or if
  the placeholders differ between the two languages.
- Terminology itself is not machine-checkable. It is a review item: a pull request that
  introduces a new concept adds it to the table above.
