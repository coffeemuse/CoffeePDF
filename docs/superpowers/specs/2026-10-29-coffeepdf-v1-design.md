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

Phase 0 found that the PdfViewer's save replaces the file through a temporary file and a rename (§16 row 2). The owner chose to have the Helper's watch handle that pattern (owner, 2026-10-30), and it already did (§4.4 item 8).

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
  - `Files/`: `PdfFile`, which tells a PDF by its header, and `AtomicFile`, which writes a file whole through a temporary file renamed over it (§16 row 2).
  - `Locks/`: the lock file beside an editable copy (§6.8).
  - `Pdf/`: CoffeePDF's own PDFium (§16 Decision 1): the markup author (§7), written into the annotations made in CoffeePDF, and the full save of an unsigned PDF (§6.4).
  - `Settings/`: the preferences (§5) and Open Recent.
- **`CoffeePdf/`** is the Avalonia app:
  - document windows hosting the PdfViewer;
  - the menu bar, with its Healdan menu, and the toolbar;
  - Preferences;
  - the platform hooks: open-file events, the `.pdf` document type, and becoming the default PDF app.
- **`CoffeePdf.Core.Tests/`:** xUnit, against the fake Helper (§9).
- **`CoffeePdf.Tests/`:** xUnit, with Avalonia headless.
- **`packaging/mac/`:** the app bundle and its signed, notarised disk image.
- **`packaging/avalonia-native/`:** TEMPORARY. A patched build of Avalonia's macOS library, shipped until Avalonia
  fixes AvaloniaUI/Avalonia#22285 (§16, Issue #9).

Where the PdfViewer lacks a feature, CoffeePDF calls PDFium itself, starting with the markup author (§7). It moves to the control's own when a newer PdfViewer adds the feature. (owner, 2026-10-30; §16 Decision 1) It uses its own copy of PDFium, separate from the PdfViewer's, and that code lives in `CoffeePdf.Core/Pdf/` (§16 Phase 1).

The bundle id is `com.healdan.coffeepdf` (call). macOS keeps one instance of an app bundle running and sends it every open-file event, so v1 needs no hand-over of its own. Linux's hand-over comes with the Linux phase.

### 4.4 Healdan's changes

Healdan's process gives every milestone a design spec in its own repository, so these changes got one there, referring to this spec (call): Healdan's **M3.6, "PDFs in CoffeePDF"** ([spec](https://github.com/Healdan-Project/healdan/blob/master/docs/superpowers/specs/2026-11-01-m3-6-pdfs-in-coffeepdf-design.md)), built on 2026-11-01 for Healdan Helper 0.5.0 (§16, Phase 3). Its design checked this list against Healdan Helper 0.4.0 and corrected it, and the items below say what M3.6 settled. What depends on CoffeePDF changes nothing until CoffeePDF is installed with Healdan support on. Four changes reach every Helper user, with or without CoffeePDF (M3.6 §2.3): items 3, 4, 10 and 11.

1. **`CoffeePdfApp : IOfficeApp`** in `Healdan.Helper.Core/Cycle/`:
   - `Name` is `CoffeePDF`;
   - `Open` launches it by bundle id;
   - `IsRunning` checks for its process;
   - `LockExists` checks for its lock file (§6.8);
   - `CopyNames.IsAppFile` recognises that lock file's name, `.~coffeepdf.<copy file name>#`, as an app's file beside LibreOffice's `.~lock.<name>#` and Microsoft Office's `~$` (M3.6 §4.5).
2. **An app per kind.** M3.5 built it: `OfficeApps` and its `AppChooser` choose the app for a new copy by its kind. PDFs join them:
   - PDFs go to CoffeePDF when it's available (item 5);
   - otherwise PDFs go to the default app, read-only, as before.

   The Helper has no PDF chooser of its own: CoffeePDF's Healdan support preference (§5) is the switch (owner, M3.6 §2.2).
3. **Links and the Open window take `.pdf`.** With CoffeePDF available, a PDF opens in it checked out. Without it, a link opens a PDF read-only in the default app, where earlier Helpers refused it, and the Open window forces read-only (owner, M3.6 §5.1, §5.2).
4. **`/v1/save-new` and `/v1/file` take `app: "CoffeePDF"`** with `formats: ["pdf"]`. `/v1/save-new` needed no change. `/v1/file` ignored `app` and filed every copy with LibreOffice until M3.6 fixed it (M3.6 §6.2).
5. **What "available" means:** CoffeePDF is installed (found by its bundle id), and its `settings.json` has Healdan support on (§5).
   - The Helper reads that file at each choice of app.
   - The Helper's Settings show CoffeePDF's state, in a PDFs row. Its menu doesn't (owner, M3.6 §4.2, §4.4).
6. **The web page's Open in App lists `pdf`** (`HealdanOpenInApp`; Healdan-Project/healdan#151).
7. **Docs:** the Developer Guide's Desktop Clients page and the user manual describe CoffeePDF, and link to this repository.
8. **A save by rename, and a lock that comes late.** The Helper's save watch has counted a temporary file renamed over a copy as a save of that copy since M3, and never looks at the temporary file (§16 row 2; owner, 2026-10-30, §16 Decision 3). CoffeePDF writes its lock once the window has the PDF loaded (§6.8), which may take longer than LibreOffice's, and the close watch already waits for it. M3.6 added tests of both and changed neither (M3.6 §6.1, §11.1).
   - CoffeePDF names that file `.coffeepdf-` followed by 32 hex digits and `.tmp`, in the copy's folder, whatever the copy's name, so the longest names still fit (Phase 1's plan, `AtomicFile`).
   - The Helper leaves those files alone, and at its start deletes any left in `Copies` while CoffeePDF isn't running (M3.6 §6.4).
9. **The holder's computer in a copy's state: dropped** (owner, M3.6 §2.2). The API shows `hold.device` only to the user who holds the check-out, so a read-only copy's state can never name another user's computer. `holdBy` names the person (`hold.by.name`), and CoffeePDF's read-only bar shows that (§6.1).
10. **A copy whose saves have stopped answers `readOnly: true`** on the channel: after a refused save (`held-elsewhere`, `not-held`, `checked-out` or `locked`), a check-out ended some other way, or newer content stored. Its `title` doesn't change, since it's the copy's file name, and `holdBy` is null. The Helper's own windows read `ReadOnly` as before (owner, M3.6 §6.3). Phase 2's fake Helper already answered so.
11. **Healdan-Project/healdan#134 is fixed.** The Helper tracks each copy it opens in CoffeePDF, read-only ones too, and `/v1/closed` deletes a read-only copy at once. It does the same for a copy opened in the default app (owner, M3.6 §5.4, §5.5).

## 5. Preferences

- **Healdan support:** on or off. It's off by default.
  - At first launch, if the Helper is installed, CoffeePDF asks once: *Use CoffeePDF with Healdan?*, with Not Now and Use with Healdan. Either answer counts as asked. "Installed" means Launch Services knows `com.healdan.helper`. (owner, 2026-10-30)
  - Turning support off while Healdan copies are open takes effect for each one as its window closes. For local windows it takes effect at once.
  - Turning support on takes effect at once. Local windows get the Healdan menu, ⌘O and ⇧⌘S go to the Helper, and an open copy asks the Helper and takes its mode. (owner, 2026-10-30)
- **Markup author:** the name written into each annotation. It defaults to the macOS full name and can always be changed. (owner)
- **Default PDF app:** a button that makes CoffeePDF the Mac's default app for PDFs, through macOS's API.
- **Where they're stored:** `settings.json`, in `~/Library/Application Support/CoffeePDF/` on macOS and in `$XDG_CONFIG_HOME/coffeepdf/` on Linux. The Helper reads `healdan.enabled` from it (§4.4).

## 6. Flows

Everything in this section applies with Healdan support on, except where it says otherwise.

### 6.1 A window's mode

| Mode | When | Title | Markup | Healdan menu |
|---|---|---|---|---|
| Local | Any file outside the Helper's `Copies/` folder, or one in it that the Helper doesn't know | The file's name | On | Save to Healdan; Properties (which runs Save to Healdan) |
| Healdan, editable | A copy checked out to this computer | The Helper's `title`, as it is: `Settlement Agreement #1234 v2.1` | On | Everything |
| Healdan, read-only | A read-only open, a document held elsewhere, or a copy whose saves have stopped (§6.9) | The Helper's `title`, as it is: `Settlement Agreement #1234 v2.1 (read-only)` for a read-only copy, and unchanged for one whose saves have stopped. Plus a bar: *Read-only — checked out by Jane* | Off | Save as New Document |
| Healdan, Helper away | A copy in `Copies/` while the channel doesn't answer | The Helper's last `title` for this window, if it had an answer; otherwise the copy's file name without `.pdf`, which is what the Helper's `title` would be | On, except for a read-only copy | Off, with a bar (§6.9) |

- **The title** is the Helper's, as the LibreOffice extension shows it, with CoffeePDF's *Edited* and *Saving to Healdan…* after it. (owner, 2026-10-30)
- **The read-only bar** names the holder: the person only, since the API never names another user's computer (§4.4 item 9). (owner, 2026-10-30 and 2026-11-01)
- **A read-only copy with the Helper away** (`… (read-only).pdf`) stays read-only and gets no lock file. Only an editable copy gets markup, a lock file, and its saves followed. (owner, 2026-10-30)

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
- **From the save's write until `/v1/saved` answers other than `pending`,** the title shows a small *Saving to Healdan…* indicator, since the first answer can take 120 seconds. (owner, 2026-10-30)
  - CoffeePDF asks again after 2, 5 and 15 seconds, then 30, then every 60, as the LibreOffice extension does. It keeps asking the same way while the Helper can't be reached.
  - Following ends when the window closes, when a newer save takes over, when the file is saved elsewhere, or when the Helper answers with an error.
- **The PdfViewer's AutoSave stays off,** because each edit would otherwise become a snapshot. (call)
- **What a save writes:** the PdfViewer saves to memory, `MarkupAuthor` writes the author (§7), and an unsigned PDF
  then gets a full save in CoffeePDF's own PDFium, so the file holds only what the document uses: no markup that was
  undone, and no drawing the PdfViewer replaced. A signed PDF keeps its incremental save, so an undone markup may stay
  in the update a save appends to it, a known limit for v1. (owner, 2026-11-03; §16, finding 3's fix)

Phase 0 found the temporary file and the rename (§16 row 2). The owner chose to have the Helper's watch handle them, rather than writing in place (§16 Decision 3).

### 6.5 Save as New Version

The Healdan menu's Save as New Version, with unsaved changes, calls `/v1/state {path}`, saves, then calls `/v1/new-version {path}`. The Helper sends the copy's file as a version once it has settled, 1.2 seconds after `/v1/new-version`, so the save is written first: one written after, and slower than that, would miss the version. The save goes as the version, giving `#1234 v3.0`. A save that fails asks for no version, since the file as it is would go instead (§16, Phase 3).

With no unsaved changes it saves nothing: the Helper sends the file as it is as the version, and CoffeePDF follows it with `/v1/saved` as it follows a save (§6.4), so the title moves on to `#1234 v3.0`. Phase 2 had it say *The next save goes to Healdan as a new version.* and follow nothing, on a wrong reading of the Helper (§16, Phase 3).

With the Helper away, the version can't be asked for, so CoffeePDF says so and doesn't save: `/v1/state` finds it away before the save. The LibreOffice extension saves locally without saying. (owner, 2026-10-30 and 2026-11-01)

### 6.6 Save as New Document, and Save to Healdan

These cover Save as New Document for a copy, and Save to Healdan for a local PDF, where `path` is left out:

1. `POST /v1/save-new {"app": "CoffeePDF", "name", "formats": ["pdf"], "path"}` shows the Helper's Save window, where the user picks a folder and fills in the required properties.
2. On `save`, CoffeePDF writes the PDF, with its markups, to the answer's `tempPath`. It then calls `/v1/file` with the answer's values unchanged, and gets the new copy's state.
3. The window moves to the new copy's path. The Helper doesn't write that copy: its answer is the path the app is to
   save the document as, and the app's first write there is the copy's starting point (its `CopyCycle.AdoptNewAsync`;
   the owner's check 5, 2026-10-31). So the window:
   - writes the PDF it filed there, the same bytes, so Healdan already has them;
   - reopens the file there;
   - writes the new lock file;
   - removes the old copy's lock file and calls `/v1/closed` for it, if there was one;
   - then calls `/v1/adopted {path}`.

   All of this happens before the window takes input again. If the copy can't be written, the window stays on its file
   with its changes, and calls `/v1/closed` for the new copy so the Helper lets it go. CoffeePDF says the document is
   filed, and to open it from Healdan.
4. On `local`, CoffeePDF shows the Mac's Save panel. On `cancel`, nothing happens.

A save through the Mac's Save panel moves a copy's window to a local file. The window lets the copy go as in step 3: its lock file goes, and CoffeePDF calls `/v1/closed` for it. The window is then local. (owner, 2026-10-30)

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
  - The window asks the Helper again each time it comes forward, and each save asks too. Nothing polls. (owner, 2026-10-30)
- **A save is refused** (`409 held-elsewhere` or `not-held`). The Helper ends the copy's check-out, keeps the copy and tells the user. CoffeePDF's next `/v1/state` shows the copy as read-only, with its title unchanged and `holdBy` null, and CoffeePDF offers Save as New Document. Healdan Helper 0.5.0 answers so (§4.4 item 10), as the owner's check H4 showed (§16, Phase 3).
- **The Helper answers with an error** on a window route. The Helper's errors carry only a code, so CoffeePDF says in its own words what didn't work, with the status and code (such as *Healdan Helper answered 500 (internal-error)*). The user can carry on with the Mac's own boxes. (owner, 2026-10-30)
- **Logs:** `~/Library/Logs/CoffeePDF/coffeepdf.log`. Until the Linux phase, the log on Linux goes in the settings folder (owner, 2026-10-30). Nothing logs the channel's secret or a file's contents.

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
- Healdan Helper 0.5.0 (M3.6 §6.5) changes no route or shape, but two meanings, and `ChannelRoutes` says so:
  - `readOnly` is also true once a copy's saves have stopped (§4.4 item 10);
  - `/v1/file` honours `app` (§4.4 item 4).

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
| 3. Healdan's milestone | §4.4, through its own spec in the Healdan repository, and then the web page's Open in App: Healdan's M3.6, "PDFs in CoffeePDF". | Changes nothing until CoffeePDF is installed with support on. |
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
- **Healdan's milestone:** settled as M3.6, "PDFs in CoffeePDF", in its own design session (owner, 2026-11-01; §4.4).
- **The Linux phase** starts after the Helper's Linux phase (M3's phase 2) lands.

## 16. Phase 0 and Phase 1 findings

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
   - **Which annotations are new.** Markup annotations on the pages `AnnotationAdded` reported, with no `/T`, that aren't one of the authorless annotations in the file the save replaces. One with a `/NM` is matched by its `/NM`. One without must carry the PdfViewer's `/SNC` mark, and is matched by its subtype, rectangle and text (owner, 2026-10-30, after check 12b: the PdfViewer leaves an empty sticky note unnamed; Phase 1, below).
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
  arm64: `CoffeePDF`, `libAvaloniaNative`, `libHarfBuzzSharp`, `libSkiaSharp`, `libpdfium` and `libcoffeepdfium`. It is
  expected to run on the Mac that built it (Decision 4), but it has not been launched: the build didn't open the app,
  and the owner's checks do, below.
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
    in UTF-8, UTF-16, Latin-1 or hex. The hooks also check commit messages (`.githooks/commit-msg`), and refuse any PDF
    by its content, whatever its name; a file they can't read refuses the commit.
  - Tests save with the made-up author `Test Reviewer`, which `Headless` sets before the app starts.
  - CI masks the Developer ID identity in its logs.
- **Check 12b, a password-protected PDF (the owner's checks, 2026-10-30).** Its markups saved without an author, for
  two reasons, each now covered by a test.
  - The PdfViewer's own password prompt opens the file but leaves `Password` unset, so the save couldn't open the
    encrypted bytes to write the author. The window keeps what's typed into the prompt's box (`PART_PasswordTextBox` in
    the 12.0.1 template). A test drives the prompt, so a renamed part fails it.
  - The recheck's note was empty, and the PdfViewer names a sticky note (`/NM`) only when it has text. Observed: with
    text it got an `av-` name, and empty, through `AddStickyNoteAsync` or the Note tool, it got none, so the rule skipped
    it. Every other markup in the owner's check files had a name.
  - The rule now matches an unnamed note by its subtype, rectangle and text, and only one carrying `/SNC`, the key the
    PdfViewer writes into each sticky note it makes (owner's choice of matching by shape; Decision 1). The mark is needed
    because the PdfViewer's save resizes another app's note, below, so without it such a note would have looked new.
- **A finding for the owner: the PdfViewer's save changes another app's note that has no appearance.** Observed: it gave
  the note an appearance stream and the key `PDFIUM_HasGeneratedAP`, and cut its rectangle to the 20-point icon
  (`[72 600 96 624]` became `[72 600 92 620]`). Its text, and the absence of `/NM` and `/T`, were kept. Expected, not
  checked: Preview and Acrobat write an appearance with their notes, so this touches only notes from apps that write none.
- **A finding for the owner.** Two windows' PdfViewers share one PDFium, which the PdfViewer owns. Whether two documents
  rendering at once is safe is the PdfViewer's own concern, and worth raising with Avalonia under the Pro licence.
  CoffeePDF's own calls no longer share it.
- **A note for the owner.** A save replaces the file through a rename, so Finder tags and "where from" on a PDF are not
  kept after CoffeePDF saves it, as with the PdfViewer's own save before.
- **The owner's checks on the Mac (2026-10-30): all 13 passed, four after fixes.** They are the ones a headless test
  can't show, listed in the pull request (§8): the Markup author as Preview shows it, Settings (and its layout, §14
  item 6), the default PDF app, the close and quit prompts, the app with no window, Open Recent, Save As, printing, each
  shortcut acting once, forms, and encrypted PDFs. The builds were signed with the Developer ID and the hardened runtime,
  not notarised: `0.1.0-phase1.2` for the first round, and `0.1.0-phase1.4` for the last recheck.
  - Passed first time: 1 (the author, in Preview), 3 to 9, 11, and 12a (a PDF with only an owner password that allows
    comments: the markups carry the author, and the protection is kept).
  - 2, Settings: the CoffeePDF menu had no About, since Avalonia drops its About Avalonia once the app sets a menu of its
    own. About CoffeePDF now opens macOS's own About panel, which reads Info.plist.
  - 10, the shortcuts: each acted once, but ⌘Z in the search field undid the last markup, since the Edit menu's Undo
    takes the key first. Undo and Redo now go to a focused text field.
  - 13, ⌘W in Settings did nothing, since that window has no menu of its own to carry Close. A key binding closes it.
  - 12b, a PDF with an open password: the markups saved without an author, for the two reasons above. Observed in the
    recheck's file: still encrypted, with CoffeePDF's update appended, and a note with text, an empty note, a
    highlight and a drawing, each with the account's full name as its author.
  - With these fixes, the tests with the key: the core's 64 passed, and the app's 60 passed and 1 skipped (the keyless
    probe).

### Phase 2's questions (answered 2026-10-30)

Phase 2's plan (`docs/superpowers/plans/2026-10-30-phase-2-healdan-in-coffeepdf.md`) put these to the owner. It read
the Helper's channel in Healdan's repository at `96eb0b05b`, and four of the questions come from places where today's
Helper differs from this spec. The owner chose the plan's recommendation each time, and §4.4, §5 and §6 now say so.

1. **The title (§6.1):** the Helper's `title` as it is, `Settlement Agreement #1234 v2.1`, or `… (read-only)`, as the
   LibreOffice extension shows it. The spec had `Settlement Agreement — #1234 v2.1`, which CoffeePDF would have built
   from `title` and `docRef`.
2. **Who holds it (§6.1):** the holder's name now, since `holdBy` is `hold.by.name` only. The computer comes when the
   state names it (§4.4 item 9).
3. **A refused save's state (§6.9):** the Helper changes in Healdan's milestone (§4.4 item 10). Phase 2's fake Helper
   answers `readOnly: true` after a refusal. Until the real one does, a refused copy stays editable in CoffeePDF.
4. **The Helper's errors (§6.9):** CoffeePDF's own words, with the status and code, since an error carries no message.
5. **A read-only copy with the Helper away (§6.1):** it stays read-only, with no lock file.
6. **When the Helper-away bar goes (§6.9):** the window asks again as it comes forward and as it saves. Nothing polls.
7. **Turning Healdan support on (§5):** it takes effect at once.
8. **Save As from a copy to a local file (§6.6):** the window lets the copy go, as Save as New Document does.
9. **Save as New Version with the Helper away (§6.5):** CoffeePDF says so and doesn't save.
10. **The smaller ones:**
    - the first launch's words (§5);
    - the *Saving to Healdan…* indicator, from the save's write until an answer that isn't `pending` (§6.4);
    - the LibreOffice extension's time limits for the routes that aren't windows: 10 seconds, 135 for `/v1/saved`, and
      660 for `/v1/file`;
    - a file in `Copies/` that the Helper doesn't know opens as a local file, and stays out of Open Recent;
    - the Helper's Save window starts with a copy's name without its doc ref, or a local file's name without `.pdf`;
    - until the Linux phase, the log on Linux goes in the settings folder (§6.9).

A note for the acceptance (§2.5): Save as New Version on a document with no new changes marks the next save as a
version (`MarkNextVersion`), and `/v1/saved` answers `pending` until that save comes. So step 3 should add a markup
before Save as New Version.

### Phase 2 (built 2026-10-31)

The plan's tasks (`docs/superpowers/plans/2026-10-30-phase-2-healdan-in-coffeepdf.md`) stand as written, as the record of
what was planned. This is what the build showed, and what it changed. No owner's answer above changed.

- **The tests (observed 2026-10-31).** No test failed in any run.
  - Without the key, `CoffeePdf.Core.Tests` passed 163, and `CoffeePdf.Tests` passed 20 and skipped the 102 that need the
    PdfViewer.
  - With the key, the core's 163 passed, and the app's 121 passed and 1 skipped: the probe
    `What_the_viewer_does_without_a_licence`.
  - Five more runs of the app's PdfViewer tests (`--filter Category=PdfViewer`), which run a fake Helper's listener per
    test in parallel, each passed 102 and skipped that 1.
  - After check 5's fix, with one test added: with the key, the core's 163 passed, and the app's 122 passed and 1
    skipped. Without it, the app's 20 passed and 103 skipped. `SaveToHealdanTests` passed three more runs.
- **The package (observed 2026-10-31).** `packaging/mac/package.sh 0.2.0-phase2`, with the key and no signing identity
  (ad-hoc, without the hardened runtime; Decision 4), made `CoffeePDF-0.2.0-phase2.dmg`, 46,635,525 bytes, sha256
  `678491f03851a6817364fab9f1de59205575adc8f2285dbfa7b7c6d8baf5ac34`. `Contents/MacOS` holds the same six native arm64
  files as Phase 1's. It ran on the Mac that built it (Decision 4) for the owner's checks below. Check 5's fix came after
  it, and was checked from the build.
- **What the build changed beyond the plan's code.** Each change came from a review of the build.
  - *Closing and quitting (§6.8).*
    - A closed window never writes a lock file, even when the Helper's answer to `/v1/state` comes after the close.
    - A close waits for a save still writing before it lets the copy go. Each copy gets one lock removal and one
      `/v1/closed`, after its last write. A save queued behind another writes nothing once its window has closed or begun
      to move, and a move waits for a save still writing.
    - Quitting waits for every copy's `/v1/closed`, including those of windows that had already closed. A second ⌘Q
      while one waits does nothing.
    - Save at the close prompt of a window that can't save (a read-only copy, or one whose save Healdan refused) is
      Save As.
  - *Filing (§6.6).*
    - A `/v1/file` that gets no answer is "outcome unknown", not a failure. The window stays on its file with its changes
      unsaved, the temporary file is left to the Helper, the Helper is marked away, and CoffeePDF says the document may
      be in Healdan already. No Save panel is offered, since a second filing would make a duplicate.
    - Open, Save As and Save to Healdan do nothing in a window that is closed or moving. A window closed during the copy
      write files nothing and deletes the copy it wrote.
    - An encrypted copy's password is carried across the move, so the new copy's next save still writes the author
      (Phase 1's check 12b). A close during the move ends the wait for the new copy to load.
    - A filing answered without the new copy's path gives no copy to open, rather than the Helper's temporary file.
  - *Following a save (§6.4).* A follow that ends on an unexpected exception logs it by its type, and every ending
    clears *Saving to Healdan…*. A follow doesn't start in a closed window. Save as New Version with nothing to save
    says *The next save goes to Healdan as a new version.* It neither saves nor follows, since the Helper has no change
    to send until a save comes. The owner may prefer a save that writes the unchanged file, as §6.5 first had it. §2.5
    step 3 still adds a markup first.
  - *Modes and titles (§6.1).*
    - A window that had the Helper's answer and then lost the Helper keeps the Helper's last title, not the file name
      without `.pdf`. The row for Helper away describes a copy opened with no answer, and the last answer is the newer
      version (`v2.2` after a save).
    - A copy the Helper doesn't know is local, whether the window was asking or the Helper was away.
  - *Settings and the first launch (§5).* A Healdan box whose write fails goes back to the stored setting, with no
    dialog. An open Settings follows a change made elsewhere. The first launch writes the yes before it records the
    question as asked, so a failed second write can't lose it.
  - The log writes its times in the invariant culture.
- **The owner's checks (2026-10-31).** They ran on the `0.2.0-phase2` package, installed in Applications, with Healdan
  Helper 0.3.0-rc1 on macOS 27.0.1. All nine passed, check 5 after a fix.
  1. *The first launch's question.* Not Now left support off and recorded the question as asked, and the next launch
     didn't ask. Use with Healdan turned support on, and the Healdan menu showed.
  2. *Settings.* The Healdan section showed with no clipped labels, and the box shows and hides the Healdan menu.
     - One flaw: back in a document window after the box changes, only the CoffeePDF menu is drawn until the pointer
       reaches the menu bar. That's issue #9, a fix for after Phase 2. It turned out to be Avalonia's, and needs no
       change to the box (§16, Issue #9).
     - While Settings is in front the bar holds only the CoffeePDF menu, as Avalonia gives a window without a menu.
     - A change made in the window in front (check 7's Turn On Healdan Support) draws in full.
  3. *Support off.* No Healdan menu, and ⌘O and ⇧⌘S showed the Mac's own panels.
  4. *⌘O through the Helper.*
     - The Helper's Open window, its way back and its Cancel acted as expected.
     - A document chosen there opened as a read-only copy, with its bar and title, the markup tools off, and no lock
       file. Closing it sent `/v1/closed`.
     - Today's Helper keeps that copy until its next start: it opens a PDF in the default app without tracking it, so
       it drops the report (Healdan-Project/healdan#134).
  5. *⇧⌘S through the Helper.* The Save window and its way back acted as expected. The filing then filed the document,
     but the window showed its PDF as not found.
     - The cause: the Helper doesn't write the new copy, the app does (§6.6 step 3).
     - The fake Helper had written it itself, so no test saw this. It now files as the Helper does: it answers the
       copy's path and writes nothing there.
     - Fixed: the window writes the PDF it filed to that path, then opens it. If that write fails, it says so, keeps its
       changes, and lets the new copy go.
     - Re-checked with the fix, run from the build. The window moved to the new copy, which was written with its lock
       file and adopted. Closing it sent `/v1/closed`, and the Helper checked the copy in and deleted it.
     - The new document's title had ` (1)` after its name: Healdan makes a file name unique in its folder, and the
       first filing had the same name.
  6. *The Helper quit.* ⌘O and ⇧⌘S showed the Mac's own panels, with no message. The Helper's next start deleted
     check 4's copy.
  7. *A copy's bar, title and lock file, with a Helper folder of the check's own.*
     - With support on, the window showed the title `Check #1 v1`, the Helper-away bar and the markup tools on.
     - With support off, it showed the read-only bar, whose Turn On Healdan Support made it the Helper-away one.
     - The lock file was there only while the copy was editable, and went with its window.
  8. *The log.* No line held the channel's secret, and the log named every route checks 4 and 5 called.
  9. *Recent documents.* No Healdan copy was in File › Open Recent or the Apple menu's Recent Items.
- **Follow-ups the build left.**
  - Asked to quit through AppleScript, CoffeePDF quits but answers "User canceled" (-128), though its own handler
    cancels nothing there: Avalonia ends the app itself. Check whether that stops a logout or a restart.
  - Save at the close prompt, or at a quit's, while Save to Healdan writes its copy returns false, and the close or quit
    is dropped without a word. Defer the close, or tell the user. Until then ⌘W or ⌘Q again works.
  - A filing shows no progress, for up to 660 seconds, and ⌘Q during it does nothing.
  - A close, and so a quit (which has no overall time limit), also waits for a save still taking its PdfViewer snapshot.
    No run hung.
  - A `/v1/new-version` that times out is worded "isn't running". A refused filing is logged twice.
  - A second `Close()` on a window already closed is expected to do nothing; an `if (!_closed)` guard would say so.
  - A move that begins while Open or Save to Healdan waits on a Helper window isn't re-checked after the answer (narrow).
  - Two Save to Healdan flows at once need a Helper that breaks §6.3's rule, since it answers `cancel` to a second Save
    window.
- **For Healdan's milestone (Phase 3) and the acceptance (Phase 4).**
  - The Helper's `CopyNames.IsAppFile` must recognise `.~coffeepdf.<copy file name>#` beside LibreOffice's lock (§4.4
    item 1). It's Healdan's code, so nothing breaks in CoffeePDF without it, but the Helper may take the lock for a
    stray file when it sets a copy aside.
  - Confirm that the real Helper treats `"path": null` in `/v1/save-new` and `"replaces": null` in `/v1/file` as
    missing. Only the fake Helper has seen them.
  - The Helper should track each copy it opens in CoffeePDF, so that `/v1/closed` deletes a read-only one at once.
    Today's Helper opens a PDF in the default app without tracking it, drops the report, and deletes the copy only at
    its next start (the owner's check 4; Healdan-Project/healdan#134).
  - The menu bar hiding the Healdan item is only checked headless. §2.5 step 7's walkthrough checks it on the Mac.
  - Add a hand check of ⌘S then ⌘W on a large copy, since a close now waits for the save to finish.

### Issue #9: the macOS 27 menu bar (2026-10-31)

- **What's wrong.** Observed by the owner on macOS 27.0.1, with Avalonia 12.1.4. After a switch between CoffeePDF's
  windows, the menu bar shows only the CoffeePDF menu:
  - between two documents, for 2–3 seconds;
  - from Settings back to a document, until the pointer reaches the bar, with or without a change to the Healdan box.

  ⌘-Tab away and back draws the whole bar.
- **Ruled out.** Holding a background window's change to the Healdan menu until it came to the front made no
  difference on the Mac (built, checked by the owner, reverted). That fits the plain switch failing too.
- **The cause is Avalonia's:** [AvaloniaUI/Avalonia#22285](https://github.com/AvaloniaUI/Avalonia/issues/22285), open,
  with no fix released (12.1.4 is the latest).
  - On every change of key window, Avalonia gives `NSApp` a different `NSMenu` twice, about 6 ms apart: the application
    menu alone, then the new window's own. macOS 27 draws the first and holds it. Read in Avalonia 12.1.4's
    `AvnWindow.mm`: `windowDidResignKey:` calls `showAppMenuOnly`, and `becomeKeyWindow` calls
    `showWindowMenuWithAppMenu`.
  - The issue reports a second defect with the same cause: a window change that a keyboard shortcut makes, such as ⌘,,
    can stop that shortcut's top-level menu opening for the rest of the run. Not yet checked in CoffeePDF.
- **Decision (owner, 2026-10-31): a TEMPORARY patched library.** CoffeePDF ships Avalonia 12.1.4's macOS library built
  with the issue's native patch, in place of the package's, until an Avalonia release fixes the issue.
  - `packaging/avalonia-native/` holds the patch, `build.sh`, which builds the library with the Xcode command-line
    tools, the library, and the MSBuild file that swaps it in. Its README says why, and how to rebuild and remove it.
  - The build stops when Avalonia.Native isn't 12.1.4, so an upgrade can't ship the library under a newer Avalonia.
  - `package.sh` stops if the bundle's library isn't the patched one.
  - Removing it is in the roadmap's backlog.
  - The options set aside were a runtime workaround, which would replace Avalonia's `showAppMenuOnly` from C#, and
    waiting for Avalonia. The workaround couldn't give the bar fresh menu items, so it wouldn't fix the second defect,
    and nobody had measured it on its own.
- **The owner's check on the Mac passed (2026-10-31).** On an unsigned build with the patched library, macOS 27.0.1,
  the README's checks looked good, and the menu bar showed in full, consistently, after window switches. The build
  asked again for access to the documents' folder, as a build with another signature does; that isn't the patch's.

### The menu bar with no window (2026-11-01)

- **What was wrong.** With no document open, the menu bar had only the CoffeePDF menu, so opening a document needed an
  empty window first, from the Dock icon. (owner, 2026-11-01)
- **Built.** While no document window is in front, with none open or with Settings in front, the bar is the CoffeePDF
  menu, File, and Healdan while Healdan support is on (`Platform/AppMenuBar.cs`).
  - File has a window's items: Open… and Open Recent work, and the rest are greyed out. Healdan's items are greyed
    out, as in an empty window.
  - Open… goes to the Helper's Open window first, as a window's does (§6.3). Otherwise it shows the Mac's Open panel
    on its own (`Platform/OpenPanel.cs`), since Avalonia shows its panel only on a window. An error from the Helper
    shows in a message window of its own.
  - Avalonia.Native builds the bar it shows without a window's menu from the menu that holds the app's, so the app's
    menu now has one.
- **It relies on issue #9's patch.** Avalonia 12.1.4's own `showAppMenuOnly` puts the CoffeePDF menu back last in that
  bar, which would show File's menu under CoffeePDF's name; the patch puts it back first. The patch's README checks
  this before the patch goes.
- **Checked in the running app (2026-11-01),** by reading `NSApp`'s menu from a throwaway build, with settings, Helper
  and log folders of its own. With the window closed, the bar was CoffeePDF, File and Healdan (hidden with support
  off), with the items on and off as above. Turning support on showed Healdan. The Open panel showed on its own, and
  Cancel answered no files.
  - If CoffeePDF starts in the background and its window never comes forward, the bar stays the CoffeePDF menu alone
    until a window does, as before. Avalonia sets the bar only when a window comes forward or the last one closes.
- **The owner's check on the Mac passed (2026-11-01),** on an ad-hoc signed build, `0.2.2-nowindow.1`, with the
  installed CoffeePDF quit: the bar with every document closed, Open Recent, ⌘O with Healdan support on (Cancel, a
  Healdan document, and the way back to the Mac's panel) and off, Settings in front with the Healdan box ticked and
  unticked, and issue #9's window switches.
  - The Healdan menu stays greyed out with no document: opening from Healdan is File › Open…, which goes to the
    Helper's Open window. No Open item is wanted in the Healdan menu. (owner, 2026-11-01)

### Phase 3: Healdan's M3.6 (built 2026-11-01)

- **What it is.** Healdan's M3.6, "PDFs in CoffeePDF", is the Helper's side of §4.4, as corrected there
  ([spec](https://github.com/Healdan-Project/healdan/blob/master/docs/superpowers/specs/2026-11-01-m3-6-pdfs-in-coffeepdf-design.md),
  [plan](https://github.com/Healdan-Project/healdan/blob/master/docs/superpowers/plans/2026-11-01-m3-6-pdfs-in-coffeepdf.md)).
  The owner approved its design and plan on 2026-11-01. It was built the same day and merged in
  Healdan-Project/healdan#150 (the Helper and its docs) and Healdan-Project/healdan#151 (the web page's Open in App for
  PDFs). CoffeePDF's code didn't change for it.
- **The owner's checks (2026-11-01).** On Healdan Helper 0.5.0-rc1, with CoffeePDF 0.2.1 and its Healdan support on,
  against the owner's server. All twelve passed: §2.5's steps 1 to 7, M3.6's rows H1 to H4 (a crash, a restart,
  CoffeePDF unavailable and a refused save), and the Helper's Settings. M3.6's plan has the full record ("Hands-on checks
  on Healdan Helper 0.5.0-rc1").
  - Step 1 ran by a typed `healdan://` link, since the web page's Open in App for PDFs waited for #151 and the server's
    upgrade.
  - Step 2: ⌘S made v1.1, with no temporary file left behind. Step 5: closing the read-only copy deleted it at once
    (§4.4 item 11). Step 6: the filed copy's record named CoffeePDF (§4.4 item 4).
  - H4: the channel answered `readOnly: true` with the title unchanged, and CoffeePDF went read-only and offered Save as
    New Document (§6.9).
- **What they showed for CoffeePDF.**
  1. *Save as New Version with no unsaved changes.* The Helper sends the file as it is as the version at once, as it has
     since M3 (`CopyCycle.MarkNextVersion`, and the Desktop Clients page says so). CoffeePDF says *The next save goes to
     Healdan as a new version.* and follows nothing, so its title stays a version behind until the next save. Phase 2's
     reading (§6.5) was wrong. Fixed (2026-11-01): it follows `/v1/saved` after `/v1/new-version`, as after a save, and
     the message is gone. `Save_as_New_Version_with_nothing_to_save_follows_the_file_as_it_is_to_Healdan` replaces the
     test of the message. Not yet checked on the Mac.
     - The review of #11 found the same reading behind the order with unsaved changes: `MarkNextVersion` starts the
       file's settle at once, so a save slower than 1.2 seconds after `/v1/new-version` left the old file as the
       version. Save as New Version now asks `/v1/state`, saves, then calls `/v1/new-version`, so the Helper being away
       is still found before anything is saved (owner, 2026-11-01). A save that fails asks for no version, and only a
       copy whose saves are followed is followed (`Save_as_New_Version_saves_then_asks_for_a_version`,
       `Save_as_New_Version_whose_save_fails_asks_for_no_version`).
  2. *The read-only title.* M3.6's record says CoffeePDF marks the title read-only for a copy whose saves have stopped,
     where §6.1 shows the Helper's title as it is. CoffeePDF's code shows the Helper's title as it is
     (`WindowDocument.Title`). What disagreed was §6.1's table, whose read-only row gave only a read-only copy's title,
     with ` (read-only)`. §6.1 now gives both. If the owner saw ` (read-only)` in the title bar after H4, it's a bug
     still to find.
  3. *A full rewrite.* The checked-out PDF went from 204,021 bytes to 39,504 at CoffeePDF's first save. The PdfViewer
     rewrites an unsigned PDF whole (§16 row 1), which can drop unused objects and earlier revisions. Not yet shown:
     that nothing the owner can see was lost. Compare that document's version before the save with the one after.
     - **Settled (2026-11-02): nothing was lost.** A throwaway probe, not committed, compared #4 v1.0 with v1.1, both
       downloaded from Healdan. It read them in PDFium 157 (CoffeePDF's engine and the PdfViewer's), in PDFKit
       (Preview's), and object by object in the files themselves, and printed counts and matches, not the document's
       text. Healdan's v1.1 is 32,952 bytes, not the 39,504 above, which wasn't traced.
     - *What changed.* Only the step's own markup. Drawn without annotations, the page matches pixel for pixel in both
       engines, and so do its text, size and rotation. The two form fields keep their values, and the note already
       there is kept. With annotations drawn, every pixel that differs (2,710 in PDFium) is inside the two new markups,
       a text box and a rectangle, which carry the Markup author. There were no bookmarks, links, attachments,
       signatures or tags to lose, and the catalog, the form and the page have the same keys as before.
     - *Where the bytes went.* 180,464 of v1.0's bytes (88%) were objects nothing in the document uses: 89 fonts, 75
       form XObjects and colour profiles, left by whatever made the form. The cross-reference listed them, but no page
       used them, and PDFium's full save writes only what the document uses: its own full save of v1.0 is 15,989 bytes.
       v1.1 also stores the fields' appearances, which v1.0 left to `NeedAppearances`, and they draw the same. One font
       is compressed differently, with the same contents.
     - *What v1.1 holds: three saves.* The PdfViewer's full rewrite, which wrote the new markups as stamps; an update
       of its own in the same save, which makes them a text box and a rectangle; and `MarkupAuthor`'s update, which
       writes their author. PDFium writes again the page, fields and note that update loaded (2,306 bytes superseded).
     - *A finding for the owner: the PdfViewer's save keeps the drawings it replaced.* v1.1 holds 21 appearance
       drawings and 21 fonts (11,589 bytes) that nothing uses, which the PdfViewer made while the document was open:
       15 of the note's pop-up and 6 of the text box. Each shows only text the document already holds. Not shown:
       whether a markup's earlier words, after an edit, or a deleted markup stay in the file the same way, unseen but
       readable by any tool. PDFium's own full save of v1.1 drops them (17,515 bytes, with every page, annotation and
       drawing the same), so CoffeePDF's save of an unsigned PDF could too.
     - **Checked (2026-11-02): a markup that's undone stays in the file.** A throwaway headless probe, not committed,
       saved through `DocumentWindow.SaveToAsync`, gave each case's note or stamp a marker word, and searched the saved
       file for it.
       - Undone, it stays, unused, with its words in plain text: a note undone before a save, a note undone after one
         (the next save keeps it, since a save doesn't reload the PdfViewer's document), and a stamp, whose drawing
         keeps its label too.
       - A note's earlier words don't stay, whether it was edited before a save, after one or in a later session, or
         was another app's. Only the new words are in the file.
       - Not shown: a markup deleted rather than undone. The PdfViewer deletes through its own selection, which the
         headless probe couldn't drive, so a markup that was in the file when it opened and is then deleted is
         untested.
       - PDFium's own full save of each file with an undone markup drops it, and leaves the page, its annotations, their
         drawings and their author the same.
       - A signed PDF is saved incrementally, so whatever its signed revision holds stays in the file by design.
- **Phase 2's list for Phase 3, settled.** `IsAppFile` knows the lock (§4.4 item 1). `"path": null` in `/v1/save-new`
  works (M3.6 §6.2), and step 6 filed a local PDF, which sends `"replaces": null`. The Helper tracks CoffeePDF's copies
  (§4.4 item 11). Step 7 checked the menu bar on the Mac. Still open: a hand check of ⌘S then ⌘W on a large copy.
- **The release `v0.3.0` (2026-11-01).** The first release that isn't a pre-release, tagged on `main` at 703f992, after
  #11 and #12. It has finding 1's fix and the menu bar with no window open, so the checks left below can run on it. CI's
  tests passed, and it signed the app and the disk image with the owner's Developer ID, notarised and stapled both, and
  attached `CoffeePDF-0.3.0.dmg`, 47,505,143 bytes, to the release. The owner downloaded it in Safari, so macOS
  quarantined it, and installed it in Applications. Gatekeeper passed (`docs/RELEASING.md`), and `spctl -a -vv` gave
  `accepted` and `source=Notarized Developer ID`.
- **After the merges (2026-11-01).** §2.5 step 1 passed from the web page, once Healdan-Project/healdan#151 was merged
  and the owner's server upgraded, and Healdan Helper 0.5.0 was released (M3.6's plan, "Hands-on checks").
- **Left before v1.0.** Finding 1 checked on the Mac, finding 2 settled, and the owner's checks of finding 3's fix
  (below); then CoffeePDF v1.0 (§13).

### Finding 3's fix: a full save for unsigned PDFs (designed 2026-11-03)

- **Why.** The PdfViewer's save writes every object it has made while the document is open, used or not, and a save
  doesn't reload the document. So a markup that's undone stays in the file, unseen, with its words in plain text, and
  so do the drawings the PdfViewer replaced (Phase 3, finding 3). PDFium's own full save writes only what the document
  uses. It dropped every leftover the probes found, and left the pages, annotations, drawings and author the same.
- **Decision (owner, 2026-11-03).** Every save of an unsigned PDF ends with a full save in CoffeePDF's own PDFium.
  - A signed PDF keeps its incremental save, since a full save would break the signature. An undone markup may stay
    in the update a save appends to it: a known limit for v1 (owner, 2026-11-03). Observed (probe, 2026-11-02): a
    note undone in the signed fixture stayed in the PdfViewer's update, in plain text, and the signature verified.
  - A writer of CoffeePDF's own, rewriting only what follows the signed bytes and keeping only what's used, is in the
    roadmap's backlog.
- **Approaches set aside (call).**
  - Folding the full save into `MarkupAuthor`. It would save a parse, but change its contract (an incremental update,
    and the same bytes back when there's nothing to write) and give it two jobs.
  - Stopping the PdfViewer from keeping leftovers. Reloading after a save doesn't help the first one, and loses the
    undo history. The owner asks Avalonia, under the Pro licence, for a save that writes only what the document uses.
    When a PdfViewer has one, CoffeePDF drops its step (Decision 1's pattern).
- **`PdfCompaction.Compact(pdf, password)`, in `CoffeePdf.Core/Pdf/` (call).**
  - It opens the bytes in `libcoffeepdfium`, under `Pdfium.Gate`, and returns PDFium's full save of them
    (`FPDF_SaveAsCopy` with `FPDF_NO_INCREMENTAL`): one revision, holding only what the document uses.
  - A PDF holding a signature comes back as it was, told by a signature dictionary's `/ByteRange` in the file's
    bytes. Every one has it, and it stays readable: the dictionary can't sit in a compressed object stream, since
    the byte range must leave out its own `/Contents`, and names aren't encrypted.
  - PDFium's `FPDF_GetSignatureCount` reads only the form's top-level fields: it counted no signature in a
    signed field under a parent field, and one in an empty signature field (probe, 2026-11-03), so the build
    doesn't use it (call).
  - An encrypted PDF opens with the save's password (`Viewer.Password`, or what was typed into the PdfViewer's
    prompt), and keeps its encryption and permissions. Observed (probe, 2026-11-02): from the fixture with an open
    password, the result still needed it, at security revision 3 with the same permissions, and kept both notes.
    A PDF with only an owner password is checked in the build.
  - Bytes PDFium can't open or save come back as they were, and `CoffeeLog` notes it by the error, never the
    contents. A save never fails for want of it, as for the author (§7).
- **Where it runs.** `DocumentWindow.SaveToAsync` writes `PdfCompaction.Compact(MarkupAuthor.Stamp(…), password)` in
  the write that already runs off the UI thread. So every save has it: ⌘S, Save As, Save as New Version's save, and
  the copy written for Healdan (§6.6). The file is still written once, through `AtomicFile` (Decision 3). The
  PdfViewer's own copy of the document keeps its leftovers, which no longer reach the file.
- **What follows.**
  - The Helper sees nothing new.
  - `MarkupAuthor` still tells new markups from the file it replaces, since the full save keeps `/NM`, `/T` and `/SNC`.
  - Files get smaller: #4 v1.1 would have been 17,515 bytes, not 32,952.
  - Each save parses and writes the document once more. The build measures it on a large document.
  - CLAUDE.md's rule for saves gains the step.
- **Testing, test-first (§8).**
  - The core (`PdfCompactionTests`), on fixtures built in code:
    - an unused object carrying a marker word is gone, and the pages and annotations are the same;
    - saves stacked in a file come back as one revision;
    - a signed PDF comes back byte for byte;
    - a signed field under a parent field comes back as it was;
    - a PDF with an empty signature field gets its full save;
    - an encrypted one stays encrypted, with its permissions, and loses its unused object;
    - bytes PDFium can't open come back unchanged.
  - The app, with the PdfViewer: a note or a stamp undone before a save doesn't reach the file, and one undone after
    a save is gone at the next. The tests of the author and of signed and encrypted PDFs still pass.
  - The owner's checks on the Mac: undo a markup on a real document and save, then search the file; open it in
    Preview; and a save to Healdan and back.
- **Built (2026-11-03).** `PdfCompaction.Compact` runs in every save (`DocumentWindow.SaveToAsync`).
  - The tests: with the key, the core's 173 passed, and the app's 138 passed and 1 skipped (the keyless probe).
    Without it, the core's 173 passed, and the app's 32 passed and 107 skipped. The app's counts were measured at
    680b93f; the probe below is the only test added since, and it is in the core's.
  - A PDF with only an owner password keeps its encryption through the full save (`PdfCompactionTests`).
    - Observed (throwaway tool, 2026-11-03): a full save of an AES-128 PDF that macOS's Quartz had encrypted
      (`/V 4 /R 4`, `/AESV2`) kept its AES encryption, its password and its permissions. AES-256 wasn't checked.
  - The probe `How_long_a_full_save_of_a_large_pdf_takes`, on a file built in code: 500 pages of text and one
    uncompressed image of about 30 MB that is a single repeated byte. The full save of its 30,129,501 bytes took 134,
    90 and 136 ms, and each run wrote 175,524 bytes. The save presumably compressed the image (inferred: the output's
    filters weren't inspected). A real scan wasn't timed, so these times don't show what one takes.
  - Left: the owner's checks on the Mac.

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
