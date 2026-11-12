# Third-party notices

Testo includes the third-party material listed here. NuGet packages are not listed; their
licenses are part of each package.

## Fonts

The fonts are served from our own origin (`src/Testo.UI.Shared/wwwroot/fonts/`). No font CDN
is used, so no visitor data goes to a third party.

| Family | Version | Files | License |
|---|---|---|---|
| Inter (variable, weight 100–900) | 4.001 | `inter-4.001-latin.woff2`, `inter-4.001-latin-ext.woff2` | SIL Open Font License 1.1 (`OFL-Inter.txt`) |
| JetBrains Mono (variable, weight 100–800) | 2.211 | `jetbrains-mono-2.211-latin.woff2`, `jetbrains-mono-2.211-latin-ext.woff2` | SIL Open Font License 1.1 (`OFL-JetBrainsMono.txt`) |

- **Source:** the unmodified subset files of the Fontsource packages
  `@fontsource-variable/inter` 5.3.0 and `@fontsource-variable/jetbrains-mono` 5.3.0, renamed.
- **Subsets:** "latin" covers English and German (umlauts, ß, „ “, €). The browser only
  downloads "latin-ext" when a page needs it, for example for names with other diacritics.
- **OpenType features in these files:** tabular numbers (`tnum`) are available. Inter's
  stylistic sets and character variants (`ss01`, `cv11`, …) are not part of this build.
- **Updating a font:** the version is part of the file name because the files are cached
  for a year (`immutable`). Add the new files under their new names, update
  `wwwroot/css/fonts.css`, the preload link in `App.razor`, and this table.

## Code

- The reconnect dialog of the app (`ReconnectModal.razor`, `.razor.js`) is based on the
  Blazor Web App project template of the .NET SDK (MIT License, © .NET Foundation and Contributors).
