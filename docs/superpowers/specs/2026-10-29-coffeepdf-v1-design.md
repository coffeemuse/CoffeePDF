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
CoffeePDF saves in place to Copies/<name> #1234 v2.pdf ──Helper watches folder──▶ snapshot upload
```

| From | To | How |
|---|---|---|
| Healdan's web page (Open in App) | The Helper | A `healdan://open/<doc #>` link, as today |
| The Helper | CoffeePDF | Launches it with the copy's path (`open -b <bundle id> <path>` on macOS) |
| Finder, Open Recent | CoffeePDF | macOS open-file events |
| CoffeePDF | The Helper | The channel: HTTP on `127.0.0.1`, with the secret from the Helper's `channel.json` |
| CoffeePDF's saves | The Helper | Writes in place to the copy, which the Helper's folder watch picks up |
| The Helper | Healdan | `/api/v1` with the device token, as today |

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
8. **A check for that spec:** how the Helper's close watch treats a copy whose lock file hasn't appeared yet. CoffeePDF writes its lock once the window has the PDF loaded (§6.8), which may take longer than LibreOffice's.

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

- **⌘S** writes the PDF in place, to the copy's path.
  1. The Helper's folder watch counts the write once its size has stopped changing, and uploads it as a snapshot.
  2. CoffeePDF then calls `/v1/saved {path}` and sets the title from the answer, such as `#1234 v2.2`.
- **While the answer says `pending`,** the title shows a small *Saving to Healdan…* indicator.
  - CoffeePDF asks again after 2, 5 and 15 seconds, then 30, then every 60, as the LibreOffice extension does. It keeps asking the same way while the Helper can't be reached.
  - Following ends when the window closes, when a newer save takes over, or when the file is saved elsewhere.
- **The PdfViewer's AutoSave stays off,** because each edit would otherwise become a snapshot. (call)

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
  - It uses a `release` environment holding the Apple signing and notarising secrets, as the Helper's `helper-release` does. The Avalonia licence key is a repository secret instead, since every run's tests need it (changed in Phase 0).
  - Without those secrets, the package builds unsigned and is only uploaded as an artifact.
- **The hardened runtime** needs `allow-jit` for .NET, as the Helper's does. PDFium's native library is signed with the owner's Developer ID.
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
