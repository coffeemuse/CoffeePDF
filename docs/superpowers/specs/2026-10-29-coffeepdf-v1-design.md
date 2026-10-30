# CoffeePDF v1 design

- **Date:** 2026-10-29
- **Status:** agreed with the owner section by section in a brainstorming session on 2026-10-29. Awaiting the owner's review of this written spec. The implementation plan follows in `docs/superpowers/plans/`.
- **Related:** Healdan ([Healdan-Project/healdan](https://github.com/Healdan-Project/healdan)). The relevant parts are its `docs/ROADMAP.md`, M3's spec (`docs/superpowers/specs/2026-10-24-m3-office-integration-design.md`), and the Developer Guide's Desktop Clients page (`site/content/developer/desktop-clients.md`), which defines the Helper's channel and the open-save-close cycle that this spec relies on.

Throughout, **(owner)** marks something the owner decided. **(call)** marks a proposal the owner heard and approved as part of a section.

## 1. What CoffeePDF is

CoffeePDF is an everyday PDF app in the spirit of Apple Preview, with Acrobat's place in a law firm as its long-term reference. It works directly with documents stored and versioned in Healdan. (owner)

- **It's a standalone app too.** It opens any local PDF and can be the Mac's default PDF app. Healdan support is an optional preference. (owner)
- **It reaches Healdan through the Healdan Helper.** For Healdan documents, CoffeePDF works alongside LibreOffice. The Helper holds the sign-in, the check-out cycle and the local copies, and draws the Open, Save and Properties windows. CoffeePDF holds no token and calls no server. (owner chose approach A, §4.1)
- **The PDF engine is Avalonia's `PdfViewer`** (`Avalonia.Controls.PdfViewer`, built on PDFium), under the owner's Avalonia Pro licence. It needs .NET 10 and Avalonia 12, the same stack as the Helper. (owner)
- **Platforms follow Healdan's order:** macOS (Apple silicon) first, then Linux once the Helper's Linux phase lands, then Windows. (call, following Healdan's principle 7)

## 2. Scope

### 2.1 v1: read and mark up (owner)

- **Local files:** open from Finder or ⌘O, with Open Recent and one window per document. CoffeePDF can be made the default PDF app.
- **Viewing:** thumbnails, outline and bookmarks, zoom and fit modes, single, continuous and two-up pages, text search, and text selection and copy.
- **Markup:** the PdfViewer's annotation tools (highlight, underline, notes, ink, shapes and text boxes, as far as the control offers them), with undo and redo.
- **Forms:** filling in interactive forms.
- **Printing.**
- **Healdan,** with the preference on:
  - Open from Healdan, and the web page's Open in App.
  - Check-out on open.
  - An ordinary save as a snapshot.
  - Save as New Version and Save as New Document.
  - Save to Healdan for a local PDF.
  - Properties.
  - The title bar's `#1234 v2.1`.
  - Check-in on close.

### 2.2 v2 roadmap (owner)

- **Page operations:** rotate, delete, reorder and insert pages, combine PDFs, split and extract.
- **Signatures and redaction:** placing a signature or initials, and true redaction.
- **Legal tooling:** Bates numbering, exhibit stamps, a doc # footer stamp, and bundles built from several Healdan documents.

Each of these gets its own design session before it's built.

### 2.3 Non-goals (call)

- Editing a PDF's text and images in place, as Acrobat does.
- Converting Office files to PDF: Healdan's server already converts them for its previews.
- OCR and comparing versions. They go in the backlog, since they may belong on the server (Healdan's backlog already has redlining).
- Mobile.
- Annotations stored apart from the file. The owner decided they live in the PDF (§3).

### 2.4 Success (call)

The owner uses CoffeePDF instead of Preview and Acrobat for day-to-day PDF reading and markup, on local files and Healdan documents alike.

### 2.5 Done when

The acceptance walkthrough runs on the owner's Mac, against the owner's Healdan server:

1. Open in App on a PDF in Healdan's web page opens it in CoffeePDF, checked out, with `#1234 v2.1` in the title bar.
2. Marking it up and pressing ⌘S makes v2.2 in Healdan, and the title follows.
3. Save as New Version makes v3.
4. Closing the window checks the document in, and the Helper deletes its copy.
5. While it's checked out, a second account opening it gets a read-only copy, with a bar that names who holds it.
6. A PDF double-clicked in Finder opens in CoffeePDF. Save to Healdan files it, and it gets a doc #.
7. With Healdan support off, CoffeePDF shows no Healdan menu, ⌘O and ⇧⌘S are the Mac's own boxes, and the Helper opens PDFs read-only in the default app, as it does today.

## 3. Decisions from the session (owner, 2026-10-29)

| Question | Decision |
|---|---|
| v1 scope | Read & mark up. Page operations, sign & redact, and legal tooling go to v2. |
| Standalone | Yes: a full PDF app for any local file. Healdan support is an on/off preference. |
| Where markups live | In the PDF, as standard annotations, saved through the Helper as snapshots and versions. |
| When to check out | On open, as LibreOffice does and as iManage does with Acrobat Pro DC: firms open PDFs to review, redact, stamp, Bates-number, combine and split them. An explicit read-only open remains, and a document held elsewhere opens read-only. |
| Approach | A standalone app that uses the Helper over its channel (§4.1). |
| Local boxes | Local Open and Save stay available, through the way back to the Mac's own boxes that the Helper's windows already offer. |
| Markup author | Defaults to the macOS full name, and can always be changed in Preferences. |
| Licence and visibility | Proprietary and private for now. MIT is the intent. It's settled before the repository goes public, once the app works fully and its dependencies are settled (§12). |

## 4. Architecture

### 4.1 Approaches considered

- **A. A standalone app that uses the Helper over the channel (chosen).**
- **B. A PDF window inside the Helper (rejected).**
  - The Helper is a menu-bar app (`LSUIElement`), with no Dock icon or File menu, and it can't be a default PDF app.
  - The licensed PdfViewer inside Healdan's LGPL Helper would stop anyone from building Healdan without an Avalonia Pro licence.
- **C. A standalone app that calls Healdan's API itself (rejected).**
  - It means a second sign-in and token on each computer.
  - It duplicates the Helper's hardest work (the check-out cycle, repeat-safe saves and crash recovery), against Healdan's rule that the Helper does it.

### 4.2 How the pieces talk

```
Web page "Open in App" ──healdan:// link──▶ Healdan Helper ──launches──▶ CoffeePDF window
Finder, Open Recent ─────────────────────────────────────────────────▶ CoffeePDF window
CoffeePDF ──channel (127.0.0.1 + secret)──▶ Helper ──/api/v1 + device token──▶ Healdan server
CoffeePDF saves over Copies/<name> #1234 v2.pdf ──Helper watches folder──▶ snapshot upload
```

| From | To | How |
|---|---|---|
| Healdan's web page (Open in App) | The Helper | A `healdan://open/<doc #>` link, as today |
| The Helper | CoffeePDF | Launches it with the copy's path (`open -b <bundle id> <path>` on macOS) |
| Finder, Open Recent | CoffeePDF | macOS open-file events |
| CoffeePDF | The Helper | The channel: HTTP on `127.0.0.1`, with the secret from the Helper's `channel.json` |
| CoffeePDF's saves | The Helper | A temporary file renamed over the copy, which the Helper's folder watch picks up |
| The Helper | Healdan | `/api/v1` with the device token, as today |

Phase 0 found that the PdfViewer's save replaces the file through a temporary file and a rename (§16 row 2). The Helper's watch learns that pattern (owner, 2026-10-30; §4.4 item 8).

The server's API needs no change: a check-out's `app` is any string of up to 100 characters. Every channel route CoffeePDF needs already exists.

### 4.3 This repository (call)

It uses .NET 10, Avalonia 12.1 (matching the Helper), and `Avalonia.Controls.PdfViewer`. The layout mirrors the Helper's, with central package versions in `Directory.Packages.props` and the SDK pinned in `global.json`:

- **`CoffeePdf.sln`**
- **`CoffeePdf.Core/`** has no Avalonia, so everything in it is testable:
  - `Helper/`: the channel client.
    - It finds `channel.json` in the Helper's support folder: `~/Library/Application Support/Healdan Helper/` on macOS, and `$XDG_DATA_HOME/healdan-helper/` (or `~/.local/share/healdan-helper/`) on Linux. When `HEALDAN_HELPER_DEV_ROOT` is set, it looks there instead.
    - It sends the secret as `X-Healdan-Secret`, and never times out on the routes that wait for a window.
    - The routes it calls are listed in one place, `ChannelRoutes` (§9).
  - `Documents/`: a window's mode (§6.1), its title, which commands are enabled, and the save-follow loop (§6.4).
  - `Locks/`: the lock file beside an editable copy (§6.8).
  - `Settings/`: the preferences (§5) and Open Recent.
- **`CoffeePdf/`** is the Avalonia app:
  - document windows hosting the PdfViewer;
  - the menu bar, with its Healdan menu, and the toolbar;
  - Preferences;
  - the platform hooks: open-file events, the `.pdf` document type, and becoming the default PDF app.
- **`CoffeePdf.Core.Tests/`:** xUnit, against the fake Helper (§9).
- **`CoffeePdf.Tests/`:** xUnit, with Avalonia headless.
- **`packaging/mac/`:** the app bundle and its signed, notarised disk image.

Where the PdfViewer lacks a feature, CoffeePDF calls PDFium itself, starting with the markup author (§7). It moves to the control's own when a newer PdfViewer adds the feature. (owner, 2026-10-30; §16 Decision 1) It uses its own copy of PDFium, separate from the PdfViewer's, and that code lives in `CoffeePdf.Core/Pdf/` (§16 Phase 1).

The bundle id is `com.healdan.coffeepdf` (call). macOS keeps one instance of an app bundle running and sends it every open-file event, so v1 needs no hand-over of its own. Linux's hand-over comes with the Linux phase.

### 4.4 Healdan's changes

Healdan's process gives every milestone a design spec in its own repository, so these changes get one there, referring to this spec (call). None of them changes anything until CoffeePDF is installed with Healdan support on.

1. **`CoffeePdfApp : IOfficeApp`** in `Healdan.Helper.Core/Cycle/`:
   - `Name` is `CoffeePDF`;
   - `Open` launches it by bundle id;
   - `IsRunning` checks for its process;
   - `LockExists` checks for its lock file (§6.8).
2. **An app per kind.** Today `CopyCycle` and `HelperSession` take one `IOfficeApp` (LibreOffice), plus the default app for other files. They choose by kind instead:
   - PDFs go to CoffeePDF when it's available (item 5);
   - otherwise PDFs go to the default app, read-only, as today.

   M3's spec §5.11 already foresees choosing an app per kind, for M3.5.
3. **Links and the Open window take `.pdf`** when CoffeePDF is available.
4. **`/v1/save-new` and `/v1/file` take `app: "CoffeePDF"`** with `formats: ["pdf"]`.
5. **What "available" means:** CoffeePDF is installed (found by its bundle id), and its `settings.json` has Healdan support on (§5).
   - The Helper reads that file the way it reads LibreOffice's registry for the extension: at start, and as the menu opens.
   - The Helper's Settings and menu show CoffeePDF's state.
6. **The web page's Open in App lists `pdf`** (`HealdanOpenInApp`), once a Helper that takes PDFs is released, as `.odg` did.
7. **Docs:** the Developer Guide's Desktop Clients page and the user manual describe CoffeePDF.
8. **A check for that spec:** how the Helper's close watch treats a copy whose lock file hasn't appeared yet. CoffeePDF writes its lock once the window has the PDF loaded (§6.8), which may take longer than LibreOffice's. A change for that spec: the Helper's folder watch counts a temporary file renamed over a copy as a save of that copy, and ignores the temporary file (§16 row 2; owner, 2026-10-30, §16 Decision 3). CoffeePDF names that file `.coffeepdf-` followed by 32 hex digits and `.tmp`, in the copy's folder, whatever the copy's name, so the longest names still fit (Phase 1's plan, `AtomicFile`).

## 5. Preferences

- **Healdan support:** on or off. It's off by default.
  - At first launch, if the Helper is installed, CoffeePDF asks once: *Use CoffeePDF with Healdan?* (call)
  - Turning support off while Healdan copies are open takes effect for each one as its window closes.
- **Markup author:** the name written into each annotation. It defaults to the macOS full name and can always be changed. (owner)
- **Default PDF app:** a button that makes CoffeePDF the Mac's default app for PDFs, through macOS's API.
- **Where they're stored:** `settings.json`, in `~/Library/Application Support/CoffeePDF/` on macOS and in `$XDG_CONFIG_HOME/coffeepdf/` on Linux. The Helper reads `healdan.enabled` from it (§4.4).

## 6. Flows

Everything in this section applies with Healdan support on, except where it says otherwise.

### 6.1 A window's mode

| Mode | When | Title | Markup | Healdan menu |
|---|---|---|---|---|
| Local | Any file outside the Helper's `Copies/` folder | The file's name | On | Save to Healdan; Properties (which runs Save to Healdan) |
| Healdan, editable | A copy checked out to this computer | `Settlement Agreement — #1234 v2.1` | On | Everything |
| Healdan, read-only | A read-only open, or a document held elsewhere | The same, plus a bar: *Read-only — checked out by Jane on MacBook-Pro* | Off | Save as New Document |
| Healdan, Helper away | A copy in `Copies/` while the channel doesn't answer | From the copy's file name (`<name> #1234 v2.1.pdf`) | On | Off, with a bar (§6.9) |

With Healdan support off, a file in the Helper's `Copies/` folder opens read-only, with a bar offering to turn Healdan support on. (owner's edge case, call)

### 6.2 Opening

1. The Helper checks the PDF out with `{"app": "CoffeePDF"}` (or makes a read-only copy), downloads the copy, and launches CoffeePDF with its path.
2. CoffeePDF opens the file and asks `POST /v1/state {path}`.
3. The answer's `title`, `readOnly` and `holdBy` set the mode (§6.1).
4. For an editable copy, CoffeePDF then writes its lock file (§6.8).

### 6.3 Open and Save As taken over (call, matching LibreOffice)

- While the Helper answers, ⌘O calls `/v1/open-dialog {"app": "CoffeePDF"}`, and ⇧⌘S calls `/v1/save-new` (§6.6).
  - The Helper's windows offer a way back to the Mac's own boxes: the `local` answer, which CoffeePDF follows with its own Open or Save panel.
  - The Helper opens what's chosen in the app for its kind, so a `.docx` chosen from CoffeePDF opens in LibreOffice.
- When the Helper doesn't answer, or Healdan support is off, ⌘O and ⇧⌘S are the Mac's own boxes.
- The window routes answer only once the user closes the window, so CoffeePDF waits without a timeout. A second request while a Helper window is open is answered `cancel`, and the Helper brings its open window forward.

### 6.4 Saving

- **⌘S** saves the PDF to the copy's path, writing a temporary file beside it and renaming it over the copy.
  1. The Helper's folder watch counts the save once the copy's size has stopped changing, and uploads it as a snapshot.
  2. CoffeePDF then calls `/v1/saved {path}` and sets the title from the answer, such as `#1234 v2.2`.
- **While the answer says `pending`,** the title shows a small *Saving to Healdan…* indicator.
  - CoffeePDF asks again after 2, 5 and 15 seconds, then 30, then every 60, as the LibreOffice extension does. It keeps asking the same way while the Helper can't be reached.
  - Following ends when the window closes, when a newer save takes over, or when the file is saved elsewhere.
- **The PdfViewer's AutoSave stays off,** because each edit would otherwise become a snapshot. (call)

Phase 0 found the temporary file and the rename (§16 row 2). The owner chose to have the Helper's watch handle them, rather than writing in place (§16 Decision 3).

### 6.5 Save as New Version

The Healdan menu's Save as New Version calls `/v1/new-version {path}`, then saves. The Helper sends that save as a version, giving `#1234 v3`.

### 6.6 Save as New Document, and Save to Healdan

These cover Save as New Document for a copy, and Save to Healdan for a local PDF, where `path` is left out:

1. `POST /v1/save-new {"app": "CoffeePDF", "name", "formats": ["pdf"], "path"}` shows the Helper's Save window, where the user picks a folder and fills in the required properties.
2. On `save`, CoffeePDF writes the PDF, with its markups, to the answer's `tempPath`. It then calls `/v1/file` with the answer's values unchanged, and gets the new copy's state.
3. The window moves to the new copy's path:
   - it reopens the file there;
   - it writes the new lock file;
   - it removes the old copy's lock file and calls `/v1/closed` for it, if there was one;
   - then it calls `/v1/adopted {path}`.

   All of this happens before the window takes input again.
4. On `local`, CoffeePDF shows the Mac's Save panel. On `cancel`, nothing happens.

### 6.7 Properties

The Healdan menu's Properties calls `/v1/properties {path}`. A `file` answer means the PDF isn't in Healdan, so CoffeePDF runs Save to Healdan (§6.6).

### 6.8 The lock file, and closing

- **While an editable copy's window is open,** CoffeePDF keeps `.~coffeepdf.<copy file name>#` beside it.
  - It holds CoffeePDF's process id and when the window opened, for diagnosis. The Helper only checks that it exists.
  - Its 13 added bytes fit within the 14 that Healdan's 241-byte limit on copy names leaves.
  - A read-only copy gets no lock file, as with LibreOffice.
- **Closing:**
  1. With unsaved changes, CoffeePDF shows the usual *Save changes?* prompt.
  2. It removes the lock file and calls `/v1/closed {path}`.
  3. The Helper sends anything unsent, checks the document in, and deletes the copy.
- **A crash** leaves the lock file with no CoffeePDF process running. The Helper's existing crash handling offers the copy back.

### 6.9 When things go wrong

- **The Helper isn't running.**
  - A Healdan copy still opens, and still saves locally.
  - A bar says the saves stay on this Mac until the Helper is back, and the Helper's resume at its next start sends them.
  - Open and Save use the Mac's own boxes. The bar goes once the Helper answers again.
- **A save is refused** (`409 held-elsewhere` or `not-held`). The Helper ends the copy's check-out, keeps the copy and tells the user. CoffeePDF's next `/v1/state` shows the copy as read-only, and CoffeePDF offers Save as New Document.
- **The Helper answers with an error** on a window route. CoffeePDF shows the error's message, and the user can carry on with the Mac's own boxes.
- **Logs:** `~/Library/Logs/CoffeePDF/coffeepdf.log`. Nothing logs the channel's secret or a file's contents.

### 6.10 Recent documents

Healdan copies stay out of Open Recent and out of the Mac's recent documents, since the Helper deletes each copy once it's checked in. The Helper's Open window has its own recent documents.

## 7. Markup author and annotations

- Annotations are standard PDF annotations, written into the file. (owner)
- Each new annotation's author is the Markup author preference (§5). (owner)
  - The PdfViewer can't set it, so CoffeePDF writes it through PDFium (owner, 2026-10-30; §16 Decision 1).
- Annotations already in a file, such as Acrobat's, are kept and shown. Whether a save round-trips them untouched is a Phase 0 check (§14).

## 8. Testing (call)

- **Test-first.** All logic lives in `CoffeePdf.Core` and is tested there.
- **Unit tests** cover the channel client, the modes and their titles, the save-follow back-off, the lock file, and the preferences (including the file the Helper reads).
- **The flows** (§6) run against the fake Helper (§9). The tests wait for conditions, never for fixed times, as the Helper's tests do.
- **App tests** run Avalonia headless.
  - Tests that need the PdfViewer carry `[Trait("Category", "PdfViewer")]`, and skip when the build has no licence key.
  - Pull requests without the key still run everything else.
- **Checked by hand:** the look, focus, macOS integration and the Helper's windows. Each pull request that changes them lists its checks for the owner.

## 9. The channel contract (call)

The channel has no machine-readable contract the way the API's `openapi.yaml` is one.

- `ChannelRoutes` in `CoffeePdf.Core/Helper/` lists every route CoffeePDF calls, with its body and answer, following the Desktop Clients page's table.
- The fake Helper serves exactly those routes. A test fails if the client calls a route `ChannelRoutes` doesn't list.
- Phase 4's acceptance runs against the real Helper. Any change to the channel in Healdan updates `ChannelRoutes` in the same release.

## 10. CI and releases (call)

- **Pull requests** run `dotnet test` on a macOS runner (Linux too, for the core).
- **A `v1.2.3` tag** builds the app, signs it with the owner's Developer ID, notarises it, and attaches `CoffeePDF-1.2.3.dmg` to the tag's GitHub release.
  - It uses a `release` environment holding the Apple signing and notarising secrets, as the Helper's `helper-release` does. The Avalonia licence key is a repository secret instead, since the macOS runs' tests need it (changed in Phase 0).
  - Without those secrets, the package builds unsigned and is only uploaded as an artifact.
- **The hardened runtime** needs `allow-jit` for .NET, as the Helper's does. PDFium's native libraries, the PdfViewer's and CoffeePDF's own copy, are signed with the owner's Developer ID.
  - Only builds signed with an identity get the hardened runtime. An unsigned local package is ad-hoc signed without it, so it runs on the Mac that built it (§16 Decision 4).
- **Releasing:** `docs/RELEASING.md` covers the secrets and each release.
- **The Avalonia licence key** comes from the environment at build time: a CI secret, or the owner's own machine. It's never committed.

## 11. Repository setup (call)

- `README.md`: what CoffeePDF is, its status, and how to build it, including the licence key.
- `CLAUDE.md`: the repository's conventions, kept to the essentials, in the style of Healdan's.
- `docs/ROADMAP.md`: v1, v2 and the backlog, from §2.
- `docs/superpowers/specs/` and `docs/superpowers/plans/`.
- `.gitignore`: add macOS's `.DS_Store` and `packaging/mac/out/` to the Visual Studio template already there.
- `LICENSE`: a proprietary, all-rights-reserved notice until §12 is settled.

## 12. Licence and distribution (owner)

- **For now:** the repository is private and the code proprietary.
- **The intent is MIT,** reconsidered if later dependencies need it. The owner settles it before the repository goes public, once the app works fully and its dependencies are settled.
- **Signed builds:** the owner distributes signed, notarised builds. Anyone who later builds from source needs their own Avalonia licence that covers the PdfViewer.

## 13. Build phases

| Phase | What | Why this order |
|---|---|---|
| 0. Foundation spike | The solution, CI, the licence key, and a window that opens one local PDF in the PdfViewer, packaged, signed and notarised. The checks in §14. | Proves the riskiest parts first. |
| 1. Standalone app | Viewing, markup, forms, printing, Preferences, Open Recent, and becoming the default PDF app. | Useful on its own, and doesn't depend on Healdan. |
| 2. Healdan in CoffeePDF | The channel client, the modes, and every flow in §6, against the fake Helper. | Built and tested without touching Healdan. |
| 3. Healdan's milestone | §4.4, through its own spec in the Healdan repository, and then the web page's Open in App. | Changes nothing until CoffeePDF is installed with support on. |
| 4. Acceptance | §2.5 on the owner's Mac, against the owner's server, then the v1.0 release. | |

Each phase gets its own plan in `docs/superpowers/plans/`. Phase 0 is a spike: its findings are recorded in this spec, and any decision they force is put to the owner, before Phase 1's plan is written. (call)

## 14. Risks, and what Phase 0 checks

1. **How the PdfViewer saves.** Rewriting the whole file, rather than appending changes, invalidates digital signatures in the PDF, which matters for legal documents.
   - Check whether its saves are incremental.
   - Check that a signed PDF stays valid after a markup and a save.
   - Check that Acrobat's annotations and filled forms survive a round trip.
   - If saves aren't incremental, the owner decides how to handle signed PDFs before Phase 1 (for example, a warning before the first markup).
2. **How a save reaches the disk.** It has to look like an ordinary save to the Helper's folder watch, which waits for the size to stop changing for 1.2 seconds. Check whether the PdfViewer writes in place or through a temporary file and a rename, and whether the Helper's watch handles that pattern.
3. **Setting the markup author.** Check that the control lets the app set the author written into new annotations.
4. **The licence key.** Check how Avalonia takes it at build time, and how the PdfViewer behaves in headless tests with and without it.
5. **macOS integration in Avalonia 12.** Open-file events at launch and while running, a native menu bar, and becoming the default PDF app.
6. **Text layout.** San Francisco is measured narrower than it's drawn in Avalonia 12, which the Helper fixed in `Platform/SystemFont.cs`. The owner owns that code, so CoffeePDF can reuse it under its own licence.
7. **Notarisation** with PDFium's native library, under the hardened runtime.
8. **Depending on one vendor's control.** The PdfViewer is the heart of the app. `CoffeePdf.Core` never references it, so the logic doesn't depend on it, and only the app's document window does.

## 15. Open items

- **The licence** before the repository goes public (owner, §12).
- **Healdan's milestone** gets its name and number in Healdan's roadmap during its own design session (§4.4).
- **The Linux phase** starts after the Helper's Linux phase (M3's phase 2) lands.

## 16. Phase 0 findings (2026-10-29)

Phase 0 is built, and the keyed run is done (2026-10-29). The owner answered its decisions on 2026-10-30. Gatekeeper passed on the first release (`v0.0.1-spike4`, 2026-10-30). Only the two Acrobat checks are still open, and they're best effort. The first build had no Avalonia licence key, so the tests and probes that need a licensed PdfViewer were skipped. The keyed run then ran all that need the key, without changing any code or test. The owner's checks on the Mac followed (2026-10-29). Each Result separates what was observed in Phase 0 (keyless first, then the keyed run), what comes from the docs, and what is still pending.

| §14 item | Question | Result | Consequence |
|---|---|---|---|
| 1 | Does a save keep a signature, and other apps' annotations and filled forms? | From the online docs (https://docs.avaloniaui.net/controls/data-display/pdfviewer/loading-and-saving; the 12.0.1 package's own docs don't say it): signed files are saved as incremental updates, unsigned ones are fully rewritten. Observed: the signed fixture is a valid detached PKCS#7 (.NET verifies it, and openssl did), so a signature that fails after a save is the save's doing. Observed: the fixture for another app's note is a bare `/Text` annotation (contents, author and flags, with no appearance stream or popup), so `A_save_keeps_another_apps_note` shows only that a plain note survives. Observed (keyed run, 2026-10-29): `Saving_a_signed_pdf_appends_and_keeps_the_signature` and `A_save_keeps_another_apps_note` passed in every keyed run. A save of the signed fixture is an incremental update, as the docs say: the original 17,518 bytes come back unchanged at the start of the saved file (checked again on `signed-after.pdf`), the trailer has a `/Prev`, and the signature's `/ByteRange` ends at the original end of file. .NET still verifies the signature in the saved file, and the reopened file lists the new note. The update re-writes the catalog, the page tree, the page (with the new note added to its `Annots`), its content stream, a font and the signature's field (objects 1 to 5 and 7); the signature's own dictionary (object 8) is in the unchanged part. After a save, the reopened file lists the other app's note (its author and contents intact) and the new one. Those are .NET's check and the PdfViewer's own reading of the file, not Acrobat's verdict. Observed (the owner's check on the Mac, 2026-10-29, in Preview, on a generated form with two text fields): a value filled in Preview survived opening the file in CoffeePDF, adding a sticky note and saving with ⌘S, and Preview then showed both the value and the note. A value filled in CoffeePDF and saved showed in Preview. The file's last save was Preview's, a full rewrite, so this doesn't show the shape of CoffeePDF's own save of a form. Pending, left so by the owner because Acrobat isn't installed and Preview doesn't show whether a signature still holds: `signed-after.pdf` in Acrobat (which also shows whether Acrobat accepts the re-written objects as allowed changes), and a save over Acrobat's own comments (which carries the question for real Acrobat comments). | Filled forms pass. The signature and Acrobat-comment checks need Acrobat, so they're best effort (owner, 2026-10-30). They're followed up in [issue #3](https://github.com/coffeemuse/CoffeePDF/issues/3) and hold no phase. The keyed run found the save incremental and the signature intact by .NET's check, so it forces no decision yet. If Acrobat disagrees, or a save loses an Acrobat comment or a filled form, the owner decides how to handle signed PDFs before Phase 1, such as a warning before the first markup (§14 item 1). |
| 2 | How does a save reach the disk (in place or replaced, events, time)? | Observed (keyed run, 2026-10-29; the probe `How_a_save_reaches_the_disk`, run in the full suite and again alone, on macOS): the file is replaced, not written in place. The PdfViewer writes `<name>.<id>.tmp` beside the PDF and renames it over the PDF, so the inode changes (plain 89013008 -> 89013055, signed 89013070 -> 89013071 in the suite run; both changed in the run alone too). The `FileSystemWatcher` events, from the run alone: for the plain file `Changed` and `Created` for the `.tmp` (twice), then `Renamed` `.tmp` -> `plain.pdf` and `Created plain.pdf`; for the signed file `Changed` and `Created` for the `.tmp`, `Renamed` `.tmp` -> `signed.pdf`, then `Changed signed.pdf` and `Created signed.pdf`. In the suite run the plain file's `.tmp` pair came once, not twice, and the signed file's events matched, so the number of events varies. Neither plain-file run had a `Changed plain.pdf`. The events carry no sizes or times, so they don't show whether the signed file was written again after the rename. `SaveAsync` took 1 ms (suite) and 11 ms (alone) for the plain file and 1 ms in both runs for the signed one. The save tests reopen the file straight after `SaveAsync` returns and find the new note, so the content is complete by then. Sizes: plain 735 -> 3,403 bytes, signed 17,518 -> 21,101 bytes. Pending: the Helper's watch against this pattern (not checked here). | A save replaces the file, through a temporary file and a rename, and the events vary. §4.2 and §6.4 assumed an in-place write. There were two ways to meet this. CoffeePDF could write the saved bytes into the copy itself (for example `SaveDocumentAsync(Stream)` and then an in-place write), or the Helper's watch could handle a rename. The 12.0.1 package's API docs list `SaveDocumentAsync(Stream)`; the run didn't try it. The owner chose the Helper's watch (Decision 3, 2026-10-30). Healdan's spec makes that change, since the watch (1.2 seconds of unchanged size) expects a write in place (§4.4 item 8). |
| 3 | Can the app set the author of new annotations? | From the API (PdfViewer 12.0.1): `PdfAnnotationInfo.Author` reads an annotation's author, but nothing sets it for new ones. Observed (keyed run, 2026-10-29; the probe `What_author_a_new_note_gets`, in the suite and alone): a sticky note added through `AddStickyNoteAsync` gets no author. Other annotation types and the toolbar's tools weren't probed. After a save and reopen its `Author` was null (the probe printed `(none)`), and the saved bytes of the new note in `signed-after.pdf` have no `/T` entry, so the PdfViewer wrote none; it isn't a gap in reading. The macOS full name (`id -F`) was non-empty, and it is the Markup author's default in §5, so the PdfViewer's default (none) differs from it. The probe shows only the default, not a way to set one. | The Markup author preference (§5, §7) has no way, through the control's public API, to set the author of new annotations, and a sticky note's default is no author at all. CoffeePDF writes the author through PDFium itself (Decision 1, owner, 2026-10-30). |
| 4 | How does the licence key reach the build and the tests? | Observed: a missing key is a build error, AVLIC0001 (AvaloniaUI.Licensing 3.1.3), not a warning, so `WarningsNotAsErrors` can't soften it. Keyless builds set `AvaloniaUILicenseDisableTask=true`, the licensing package's own switch, in `AvaloniaLicense.props`. They compile, and the PdfViewer's tests skip. With `AVALONIA_LICENSE_KEY` set, evaluating the project (a placeholder, no target run) showed the licence task enabled, `AVALONIA_LICENSED` defined and the key item present. Observed (the probe `What_the_viewer_does_without_a_licence`, run without the key in the headless test host): constructing the app's `DocumentWindow` throws, before any load. The PdfViewer's static initialiser fails with a `TypeInitializationException`, whose inner exception says "No AvaloniaUI license key found. Please ensure the `<AvaloniaUILicenseKey />` item is defined in your executable project with a valid license key from the Avalonia Portal." The inner exception's type has an obfuscated name (`eg`). Inferred, not observed: there was no `LoadError` and no `ErrorMessage`, since construction threw before any handler existed. Observed: no half-working view, since the control can't be created. Expected, not run: the app creates its windows the same way, so a keyless app fails when it opens its first window. Observed (keyed run, 2026-10-29): `dotnet build CoffeePdf.sln` with the key gave 0 warnings and 0 errors, with `AVALONIA_LICENSED` defined, so the PdfViewer's tests ran instead of skipping. The headless test host counts as licensed: `A_window_shows_the_pdf_it_loads`, the three save tests and the two keyed probes all created and used the PdfViewer, with no `AvaloniaLicensingException` in any run. The licensing task wrote a ticket into the test project's `CoffeePdf.Tests.runtimeconfig.json` (the option `AvaloniaUI.Licensing.RuntimeKey_0`, 760 characters); that this is how the host is licensed is inferred, not traced. The test host is a different application from `CoffeePDF`, so this doesn't show whether the licence checks the application name or the bundle id, or whether it covers `CoffeePDF` (to-do 3). | Observed here, keyless: the build and the tests that don't need the PdfViewer work, but only through that switch, which is in the package's targets file and not its README. Recheck it on a package upgrade. Expected, not seen: CI does the same for a run without the secret, but it hasn't run. A keyless build can't run the app at all (expected, not run), since the PdfViewer throws in the first window, so it is for compiling and testing only. `package.sh` stops a signed build without the key (observed), and the workflow fails a `v*` tag's package without it (parsed, not run). The owner accepted the switch (Decision 2, 2026-10-30). The test host is licensed with the key (observed, keyed run), so the fallback of a `--probe` switch in the app isn't needed. |
| 5 | macOS: open-file events (cold and warm), menus, the default PDF app | Observed: `WindowChoice` (new, empty or existing window) passes its 6 tests. Written: file activation through `IActivatableLifetime`, launch arguments, a 500 ms wait before an empty window, a File menu (Open… ⌘O, Save ⌘S, Close ⌘W), and an Info.plist that declares `com.adobe.pdf` (Editor role, Alternate rank). Observed (the owner's check on the Mac, 2026-10-29, on the Developer ID-signed 0.0.1-spike3 build installed in Applications): Open With at a cold start opened one window with the PDF and no empty window. While running, Open With on a second PDF opened a second window, and on the first again brought its window forward, with no third. Made the default PDF app, a double-clicked PDF opened in CoffeePDF. File showed Open… ⌘O, Save ⌘S and Close ⌘W, and each worked (⌘S wrote a markup to the file); the CoffeePDF menu has Quit ⌘Q. | Passes. The 500 ms wait held at a cold start on the owner's Mac. On a slower Mac an empty window may still appear first; `WindowChoice` then loads the file into it. |
| 6 | Text layout with San Francisco | Observed: the app doesn't carry the Helper's `Platform/SystemFont.cs`. Observed (the owner's check on the Mac, 2026-10-29): no clipped or overlapping text in the PdfViewer's toolbar and sidebar labels, against Preview's. | Passes. The Helper's `SystemFont.cs` isn't needed for the PdfViewer's own labels. Recheck when CoffeePDF adds text of its own, such as Preferences (§14 item 6). |
| 7 | Notarisation with PDFium under the hardened runtime | Observed: `packaging/mac/package.sh` built a keyless package, ad-hoc signed with the hardened runtime and `allow-jit` as the only entitlement. `codesign --verify --strict --deep` passed. Its five Mach-O files are all arm64 (CoffeePDF, libAvaloniaNative, libHarfBuzzSharp, libSkiaSharp, libpdfium), and there was no non-native file. Observed (keyed package, 2026-10-29): `packaging/mac/package.sh 0.0.1-spike2`, with the key set and no signing identity, gave no "WITHOUT the Avalonia licence key" warning, only the NOT SIGNED one (ad-hoc, not notarised). Its closing lines include: "Built …/packaging/mac/out/CoffeePDF-0.0.1-spike2.dmg" and "sha256 486a18a0e20cfbbf9977f0db4358b0e4e2f76b7dc114b5598698bbd0fdcdb9a5". The disk image is 42,215,094 bytes. The app passed `codesign --verify --strict --deep` (ad-hoc, hardened runtime, `allow-jit` as the only entitlement) and has the same five arm64 Mach-O files. The key's raw text is in no file of the app (searched as ASCII, UTF-8 and UTF-16). The licence ticket is there, as expected: the option `AvaloniaUI.Licensing.RuntimeKey_0` (760 characters) in the runtime config embedded in the `CoffeePDF` binary. The run didn't open the app. Observed (the owner's check on the Mac, 2026-10-29): that ad-hoc build can't launch. Run from Terminal, it exits at start-up because dyld refuses `libSkiaSharp.dylib`: "code signature … not valid for use in process: mapping process and mapped file (non-platform) have different Team IDs". Under the hardened runtime, library validation loads only libraries signed by the app's own team, and an ad-hoc signature has none. Observed: `package.sh 0.0.1-spike3`, signed with the owner's Developer ID (not notarised, since the notary's key isn't set), gives the app and all five binaries one team ID. Run from Terminal, it launched, logged no dyld or AMFI errors and showed the PDF, so Skia and `libpdfium` load under the hardened runtime with `allow-jit` alone. Observed (the first release, `v0.0.1-spike4`, 2026-10-30, set up as `docs/RELEASING.md` says): CI signed the app and the disk image with the owner's Developer ID, Apple notarised both, and both tickets were stapled. The first attempt failed after the app's ticket was stapled, when `stapler validate` lost its connection to Apple (`NSURLErrorDomain -1005`, exit 68). A re-run of the job passed, and `package.sh` now tries stapling three times. The release's image was downloaded with `gh`. `stapler validate` passes on the image and on its app, and `spctl` accepts both as `source=Notarized Developer ID`. The app passes `codesign --verify --strict --deep`, runs under the hardened runtime, and has the same team ID as the owner's local Developer ID build. Observed (the owner's check on the Mac, 2026-10-30): the release's disk image was downloaded in a browser, so macOS quarantined it. CoffeePDF was dragged to Applications, replacing spike3, and opened. Gatekeeper let it open, and it worked as expected. | Developer ID builds load their libraries under the hardened runtime. Unsigned local packages couldn't run at all. `package.sh` now leaves them without the hardened runtime, and they launch (Decision 4, 2026-10-30). Apple notarises the app and its disk image, with PDFium, under the hardened runtime (`v0.0.1-spike4`). Gatekeeper accepts a quarantined download. Passes. |
| 8 | The core free of Avalonia | Observed: `ArchitectureTests` passes. `CoffeePdf.Core` references no Avalonia assembly. | none |

- **What ran, without the key.** `dotnet test CoffeePdf.sln` without the key: 20 passed, 6 skipped, with no warnings. The skips are `A_window_shows_the_pdf_it_loads`, the three save tests and the two probes that need the key. The probe `What_the_viewer_does_without_a_licence` is the opposite: it runs only without the key, and is skipped with it.
- **What ran, with the key (2026-10-29).** `dotnet build CoffeePdf.sln`: 0 warnings, 0 errors. `dotnet test CoffeePdf.sln`: 26 tests, 25 passed, 1 skipped, no warnings, about 7 seconds of test time and no hang (`CoffeePdf.Core.Tests` 14 passed; `CoffeePdf.Tests` 11 passed, 1 skipped). The skip is `What_the_viewer_does_without_a_licence`, as expected. Passed: `A_window_shows_the_pdf_it_loads`, the three `ViewerSaveTests`, and the probes `How_a_save_reaches_the_disk` and `What_author_a_new_note_gets`. The probes run alone (`--filter Category=Probe`) gave the same findings (replaced via a temporary file and a rename; no author), though events and timings varied. Three more runs of the PdfViewer's tests (`--filter Category=PdfViewer`) all gave 6 passed, 1 skipped. No code or test changed.
- **The signed fixture.** openssl checks the signature's digest against the signed bytes. It doesn't show that Acrobat accepts the file; the signature check on the Mac does.
- **What the keyed run turned up.** The headless wiring (`Headless`, `Viewers`, the window's PdfViewer theme) worked on the first keyed run: nothing failed or hung. `A_save_clears_the_dirty_flag` read `IsDirty` straight after an await and didn't flake: it was true after the edit and false after the save, in four runs. Probe timings: `SaveAsync` took 1 ms in the suite and 11 ms alone for the plain file, and 1 ms both times for the signed one, so running alone mattered little. Not shown: whether `SaveAsync` rewrites a document with no change, since every test and probe adds a note before it saves. The result that most needs follow-up is row 2's save pattern: a temporary file and a rename, not a write in place.
- **No key in any file (checked 2026-10-29).** After the keyed run and the keyed package, the key's text (as ASCII, UTF-8 and UTF-16) is in no file of the worktree: 1,196 files, including `bin/`, `obj/`, `probe-out/` and the built app in `packaging/mac/out/`. `grep -rlF` over the whole directory (`.git` too) and `git grep -lF` over the tracked files found no match. The `.dmg` is compressed, so searching it shows nothing; the app inside it is the one searched. What the build leaves is the licence ticket: the option `AvaloniaUI.Licensing.RuntimeKey_0` (760 characters) in each keyed `*.runtimeconfig.json` under `bin/`, embedded in the single-file app. Those are git-ignored build outputs, and the ticket is meant to ship in the app. No run used `-v:d`, `-v:diag` or `-bl`, and CI must not either, since the key would be logged.
- **CI.** Ubuntu runs only `CoffeePdf.Core.Tests`, as §10 says. The licence key is a repository secret, and §10 now says so. It goes to the macOS legs only. A push to main or a tag without it warns. A `v*` tag's package fails without it. The probes are left out of CI. The workflow has been parsed, not run: nothing has gone to GitHub yet.
- **The package.** `LSMinimumSystemVersion` is 26.0, so the app won't start on an older macOS. `hdiutil create` is deprecated on the build Mac. It still works, but a later macOS may remove it; `diskutil image create` is the replacement it names.

### Decisions for the owner (answered 2026-10-30)

1. **The markup author (item 3).** The PdfViewer can't be told an author for new annotations (its 12.0.1 public API has no setter). Observed (keyed run, 2026-10-29): a sticky note added through `AddStickyNoteAsync` got none of its own either (no `/T` entry), which differs from the macOS full name (non-empty) that §5 makes the default. Other annotation types and the toolbar's tools weren't probed. So option (c) means v1's sticky notes carry no author, and the other annotation types are unknown. The options were:
   - (a) Ask Avalonia, under the Pro licence, for an author setting on the viewer, and keep the Markup author preference waiting on it.
   - (b) After each save, write the author into the new annotations through PDFium directly, as a second, incremental write. That adds complexity, and a second save for the Helper to see.
   - (c) Ship v1 with the control's default author, and move the preference to when (a) lands.

   **Answer (owner, 2026-10-30): (b), through PDFium.** Assume Avalonia won't add an author setter, or will take a long time. The roadmap has other features the PdfViewer lacks, and they take the same route: CoffeePDF calls PDFium itself. When a newer PdfViewer adds one of those features, CoffeePDF moves to the control's own. Phase 1 builds this, and its plan answers:
   - **Which PDFium.** The PdfViewer ships `libpdfium` (PDFium 157), and using that copy would keep one in the process. PDFium isn't thread-safe, so CoffeePDF's calls mustn't run alongside the control's.
   - **When the author is written.** Either a second, incremental write after the control's save, or a save that CoffeePDF owns. In the second, the control saves to a stream (`SaveDocumentAsync(Stream)`), and CoffeePDF stamps the author and writes the file through a temporary file and a rename. That way the Helper sees one save, in the pattern of Decision 3.
   - **Which annotations are new.** Only those added in this window get the author, not another app's notes that have none.
   - **Keeping the author.** The control's own copy of the document still has no author after a stamp, so a later full rewrite (any unsigned file) drops it unless every save stamps again.
   - **What the toolbar's tools write.** The probe saw only `AddStickyNoteAsync`, so Phase 1 probes the toolbar's tools as well.

   **Settled in Phase 1's plan (2026-10-30)**, from the planning probe below (call):
   - **Which PDFium.** CoffeePDF's own copy, `libcoffeepdfium`, which `CoffeePdf.Core` binds as `"coffeepdfium"` (call,
     changed in the build, 2026-10-30). The plan chose the PdfViewer's own `libpdfium`, so that the process held one
     copy, with a lock of CoffeePDF's own and calls only while saving. The build found that failed two ways:
     - When the PdfViewer's last document closed, it shut PDFium down (`FPDF_DestroyLibrary`), and CoffeePDF's next call
       segfaulted.
     - CoffeePDF's save on a background thread, while the PdfViewer rendered, now and then crashed PDFium's font loading
       (`CPDF_Type1Font::Load`).

     The copy is the same bblanchon.PDFium 157 binary, copied at build time, so the two load as separate images. What
     the build observed is in Phase 1 (built 2026-10-30), below.
   - **When the author is written.** In a save CoffeePDF owns. The PdfViewer saves to memory, CoffeePDF writes the author as an incremental update, and a temporary file is renamed over the file. That is one write, in Decision 3's pattern, with the original bytes kept.
   - **Which annotations are new.** Markup annotations on the pages `AnnotationAdded` reported, with a `/NM` and no `/T`, whose `/NM` isn't an authorless annotation's in the file the save replaces.
   - **Keeping the author.** Every save writes it again, since the PdfViewer's own copy never has it.
   - **The toolbar's tools.** The planning probe found notes, stamps and highlights alike: a `/NM` and no `/T`. The rule covers every markup type.
2. **A build without the key (item 4).** Does the owner accept `AvaloniaUILicenseDisableTask=true` for builds without the key? Without it, such a build fails with AVLIC0001. With it, contributors without a licence can still build and test everything but the PdfViewer (observed here: 20 passed, 6 skipped), and CI without the secret is expected to (it hasn't run). Observed (the probe `What_the_viewer_does_without_a_licence`): without the key the PdfViewer doesn't work in a reduced way. It throws when a window creates it, so a keyless build can't run the app or show a PDF (expected, not run). That raises no licensing question, since the control is unusable unlicensed. The plan's `WarningsNotAsErrors` can't work, because the missing key is an error. If the owner declines, keyless builds fail until another route is found.

   **Answer (owner, 2026-10-30): accepted.** Recheck the switch whenever `AvaloniaUI.Licensing` is upgraded, since it's in the package's targets file and not its README.
3. **Anything else, and the Phase 2 save.** Apart from Decision 4, no check has failed, so no other decision is forced yet. If the pending rows fail, they add some, such as how to handle signed PDFs when saves aren't incremental (§14 item 1). From row 2, the save pattern: CoffeePDF writes the saved bytes into the copy itself, or the Helper's watch handles a rename.

   **Answer (owner, 2026-10-30): the Helper's watch handles a rename.** The save stays atomic, so a crash part-way through can't leave a half-written copy for the Helper to upload. The Helper's watch also copes with other apps that save this way. Healdan's spec makes the change (§4.4 item 8). Phase 2 tests against the fake Helper, so it needn't wait for that change. If Decision 1 makes CoffeePDF write its own saves, it keeps this pattern: a temporary file renamed over the copy.
4. **Unsigned local packages (row 7).** `package.sh` without a signing identity makes an app that can't launch, because the hardened runtime refuses its ad-hoc-signed libraries. The options were:
   - (a) Apply the hardened runtime only when signing with an identity, so unsigned local packages run. Release builds keep it. It's a one-line change in `package.sh`, and notarisation needs a Developer ID anyway.
   - (b) Keep it as is, and always sign local packages with a Developer ID, as for the checks on 2026-10-29.

   **Answer (owner, 2026-10-30): (a), done.** `package.sh` now applies the hardened runtime only with a signing identity. Observed (2026-10-30): a keyed package without one, `0.0.1-spike5`, got an ad-hoc signature without the runtime flag (`flags=0x2(adhoc)`) and passed `codesign --verify --strict --deep`. Run from Terminal with a PDF, it stayed up and loaded `libAvaloniaNative`, `libHarfBuzzSharp`, `libSkiaSharp` and `libpdfium`, with no dyld, AMFI or signature errors.

The decisions are answered, and Gatekeeper passed (the owner, 2026-10-30, on `v0.0.1-spike4`), so Phase 1's plan is written (2026-10-30, `docs/superpowers/plans/2026-10-30-phase-1-standalone-app.md`; §13). The Acrobat checks don't hold it, since they're best effort (owner, 2026-10-30; issue #3).

### The planning probe (2026-10-30)

These are the facts Phase 1's plan builds on. Throwaway tests, not committed, ran with the key, in the headless test host, against the PdfViewer 12.0.1.
- **Saving to a stream:** `SaveDocumentAsync(Stream)` clears `IsDirty`, and keeps `Source` and the undo history.
  - With `AllowDocumentSaving` off, it still works, and the PdfViewer's own `SaveAsync` does nothing.
  - A signed file's stream save is an incremental update on the original bytes. It was again after the file was replaced with the first save's bytes, a second note was added, and it was saved again: that save started with the original, not with the first save. The signature verified after both.
- **Undo after a save:** an undo past a save leaves `IsDirty` false, though the document no longer matches its file. The PdfViewer's docs say only that undoing back to the saved state doesn't clear it.
- **New annotations:** `AnnotationAdded` fired for a sticky note, a stamp and a highlight, each with its page and index. None had an author.
  - In the saved bytes each had a `/NM` (the form `av-` and 32 hex digits) and no `/T`.
  - Another app's note without a `/NM` still had none after a save.
- **How the PdfViewer calls PDFium:** through an obfuscated class of 240 imports from `pdfium` (bblanchon.PDFium 157.0.8086). Its locks are per instance, not global.
  - PDFium's own source returns at once from `FPDF_InitLibrary` when it's already initialised.
  - The bundled `libpdfium` exports everything the author needs: loading from memory and from a file, reading and setting an annotation's strings, and `FPDF_SaveAsCopy`.
- **A prototype, outside the repository:** it set `/T` on an annotation through that library, and saved with `FPDF_INCREMENTAL`. The original bytes came back as the file's start, the author read back, and an annotation with an author, or without a `/NM`, was left as it was.
- **The rest of the PdfViewer's API:** it has native printing on macOS (`PrintAsync`, through a replaceable `IPrintService`), its own password prompt, and `SetToolCommand` and `ActiveTool` for a host's own menus.
  - Its keyboard shortcuts cover copy, select-all, zoom, save, the arrow keys, Home and End, but not undo.

### Phase 1 (built 2026-10-30)

The plan's tasks (`docs/superpowers/plans/2026-10-30-phase-1-standalone-app.md`) stand as written, as the record of
what was planned. This is what the build showed, and what it changed.

- **The tests (observed 2026-10-30).** No test failed in either run.
  - Without the key, `CoffeePdf.Core.Tests` passed 59, and `CoffeePdf.Tests` passed 12 and skipped the 38 that need the
    PdfViewer.
  - With the key, the core's 59 passed, and the app's 49 passed and 1 skipped: the probe
    `What_the_viewer_does_without_a_licence`.
- **The package (observed 2026-10-30).** `packaging/mac/package.sh 0.1.0-phase1`, with the key and no signing identity
  (ad-hoc, without the hardened runtime; Decision 4), made `CoffeePDF-0.1.0-phase1.dmg`, 45,486,893 bytes, sha256
  `8d81d7484ab0420d6bbf754a0800892df31c5781ad460339105c3ee9cb030c92`. `Contents/MacOS` holds six files, all native and
  arm64: `CoffeePDF`, `libAvaloniaNative`, `libHarfBuzzSharp`, `libSkiaSharp`, `libpdfium` and `libcoffeepdfium`. It runs
  on the Mac that built it. The build didn't open the app; the owner's checks do, below.
- **CoffeePDF has its own copy of PDFium (Decision 1, call).** This reverses the plan's one shared copy.
  - Sharing the PdfViewer's copy failed two ways, as Decision 1 says: a segfault after the PdfViewer's last document
    closed and shut PDFium down, and an occasional crash in font loading when a background save met a render.
  - A stress test of two threads loading pages showed the difference. Two renamed copies load as two separate images,
    and ran five rounds of 8 seconds, about 2,500 documents each, without a crash. One shared copy hung at 100% CPU.
  - With the fix, the app's test suite ran 50 times with parallel tests and never crashed. A regression test,
    `OwnPdfiumTests`, crashed 4 of 4 times on the old code.
  - `package.sh` passes `-p:CoffeePdfiumRid=osx-arm64`, and fails a package whose copy is missing or isn't arm64.
- **Saves run one at a time.** `DocumentWindow.SaveToAsync` holds a `SemaphoreSlim`.
  - A save in progress counts as unsaved changes: in the title's "— Edited", and in the close and quit prompts.
  - The unsaved state resets when the PdfViewer's copy is taken in memory, not after the write, so an undo during the
    write still counts.
  - A failure is reported after the save, and a failure to show that report can't escape.
- **Open Recent is best effort.** `RecentDocuments` absorbs its own write failures. A Save As adds its file only after
  the save has worked, outside the save's error handling, so a failure there can't turn a written save into a failed one.
- **Settings never crash the app.** A `settings.json` that can't be written leaves the typed author in the box, and the
  store keeps its last value. A settings section of the wrong shape reads as the defaults.
- **Print reports its failures** (`PrintAndReportAsync`) instead of letting them end the app.
- **Smaller fixes.**
  - Non-ASCII strings in the test fixture `AnnotatedPdf` are written as UTF-16 hex.
  - The Settings window saves on `TextBox.TextProperty` changes, which is synchronous on Avalonia 12.1.4.
  - The close prompt's guard against asking twice lasts through the save it asked for.
  - The tests wait with a 1 ms poll, since `Task.Yield` starved the dispatcher.
- **Personal details (owner, 2026-10-30).** A saved PDF carries the Markup author, which defaults to the macOS full
  name, so the repository keeps it out.
  - `.githooks/pre-commit` refuses any PDF, and any change holding the account's full name, account name or home folder,
    in UTF-8, UTF-16 or hex.
  - Tests save with the made-up author `Test Reviewer`, which `Headless` sets before the app starts.
  - CI masks the Developer ID identity in its logs.
- **A finding for the owner.** Two windows' PdfViewers share one PDFium, which the PdfViewer owns. Whether two documents
  rendering at once is safe is the PdfViewer's own concern, and worth raising with Avalonia under the Pro licence.
  CoffeePDF's own calls no longer share it.
- **A note for the owner.** A save replaces the file through a rename, so Finder tags and "where from" on a PDF are not
  kept after CoffeePDF saves it, as with the PdfViewer's own save before.
- **Left for the owner's checks, on the Mac, with the package above.** They are listed in the pull request (§8). They are
  the ones a headless test can't show: the Markup author as Preview shows it, Settings' layout (§14 item 6), the default
  PDF app, the close and quit prompts, the app with no window, Open Recent, Save As, printing, and each shortcut acting
  once.

### The owner's to-do list, to fill the pending rows

1. **Done (2026-10-29).** `AVALONIA_LICENSE_KEY` is in the shell profile. Claude then ran the PdfViewer's tests and probes, wrote the signed files for the signature check (`COFFEEPDF_PROBE_OUT="$PWD/probe-out"`, in `probe-out/` in the worktree, which git ignores) and built a keyed package (`packaging/mac/out/CoffeePDF-0.0.1-spike2.dmg`). Rows 1 to 4 and 7 no longer wait on the key.
2. **Done (2026-10-29 and 2026-10-30).** The pull request was opened and merged, and the repository secret `AVALONIA_LICENSE_KEY` is set: CI's re-run with it passed, with the PdfViewer's tests run, not skipped. On 2026-10-30 the owner set up the `release` environment and its six secrets, as `docs/RELEASING.md` describes. The first tag, `v0.0.1-spike4`, ran the notarisation path end to end, and its pre-release has the notarised disk image (row 7).
3. Confirm that the Avalonia licence covers the application name `CoffeePDF` and the bundle id `com.healdan.coffeepdf`. The licence is checked at runtime. From the licensing package's docs (AvaloniaUI.Licensing 3.1.3): a licence lists the applications it allows by entry-assembly name or macOS bundle identifier. Not seen: whether the PdfViewer checks the name, the bundle id or both. The test host is a different application name, and it counted as licensed in the keyed run (row 4), so that run doesn't show whether the name is checked.
4. **Done (2026-10-29 and 2026-10-30), except the Acrobat checks.** On the Developer ID-signed 0.0.1-spike3 build: Open With (cold and warm), the default PDF app, the menu bar, text layout and filled forms passed (rows 1, 5 and 6). Gatekeeper passed on the `v0.0.1-spike4` pre-release's disk image (row 7). The signature and Acrobat's notes need Acrobat, so they're best effort, in issue #3. Run these checks on the Mac, with a keyed build: the notarised `.dmg` from the release, or one from `packaging/mac/package.sh` with the key set. A keyless build can't be checked, since its PdfViewer throws when a window creates it (the probe `What_the_viewer_does_without_a_licence`).
   - **Gatekeeper:** use the notarised `.dmg` from the release, not a local unsigned build, since Gatekeeper's verdict is on the notarisation. Open the `.dmg`, drag CoffeePDF to Applications and open it. Expected: only macOS's usual first-open prompt for an app from the internet, then an empty window (`docs/RELEASING.md`).
   - **Open With at a cold start:** quit CoffeePDF, then right-click a PDF in Finder and choose Open With › CoffeePDF. Expected: one window with the PDF, and no empty window beside it.
   - **Open With while running:** with CoffeePDF running, open a second PDF this way (expected: a second window), then the first again (expected: its window comes forward, with no third window).
   - **The default PDF app:** Get Info › Open with › CoffeePDF › Change All…, then double-click a PDF. Expected: it opens in CoffeePDF. Put Preview back afterwards if preferred.
   - **The menu bar:** File shows Open… ⌘O, Save ⌘S and Close ⌘W, and each works. The CoffeePDF menu has Quit ⌘Q.
   - **Text layout:** compare the toolbar and sidebar labels with Preview's. Record yes or no for clipped or overlapping text, with a screenshot if yes.
   - **The signature:** open `probe-out/signed-after.pdf` in Acrobat Reader or Preview. Expected: the document hasn't been modified since it was signed, or only allowed changes (annotations) were made after. The certificate shows as untrusted, which is expected for a throwaway one. Compare with `signed-before.pdf`.
   - **Filled forms:** fill a form field in Acrobat or Preview and save. Open the file in CoffeePDF, add a sticky note, save with ⌘S, and reopen it in Acrobat or Preview. Expected: the value is still there, and so is the note. The note makes the save really write: the keyed run saved only after adding a note, so it doesn't show whether ⌘S on an unchanged document rewrites the file. Then fill a field in CoffeePDF, save, and reopen the file in Acrobat. Expected: the value shows. No test covers this, so this check carries §14 item 1's filled-form question.
   - **Acrobat's own notes:** take a PDF with comments made in Acrobat, add a highlight in CoffeePDF, save, and reopen in Acrobat. Expected: all of Acrobat's comments are still there. Replies lose their threading if CoffeePDF edits their comment (the PdfViewer's docs say so).
5. **Done (2026-10-29).** The ad-hoc build can't launch, and the Developer ID build does (row 7, Decision 4). Run `CoffeePDF.app/Contents/MacOS/CoffeePDF` from Terminal on the ad-hoc build, which is at `packaging/mac/out/CoffeePDF.app` in the worktree (git ignores it), and watch for dyld or AMFI errors about loading its libraries under the hardened runtime. The Developer ID build isn't expected to be affected. The ad-hoc build there was rebuilt with the key on 2026-10-29 (0.0.1-spike2, which replaced the keyless 0.0.1-spike1 app; that `.dmg` is still beside it), so its PdfViewer is expected to work. Open a PDF in it: a keyed run is what shows whether `libpdfium` loads. A keyless run wouldn't have, since the PdfViewer throws before using it (the probe saw that in the test host, not in the app).
