# Developer tools in CoffeePDF's test builds

- **Date:** 2026-11-06
- **Status:** agreed with the owner section by section in a brainstorming session on 2026-11-06, and the owner
  approved this written spec the same day.
  Built on 2026-11-06 (`docs/superpowers/plans/2026-11-06-coffeepdf-devtools.md`). The acceptance run (§7) is next.
- **Related:** the v1 spec (`2026-10-29-coffeepdf-v1-design.md`), its §8 (testing), §10 (CI and releases) and §12
  (the third-party notices); the toolbar spec (`2026-11-05-coffeepdf-toolbar-design.md`, on the toolbar's branch,
  coffeemuse/CoffeePDF#17); `packaging/mac/package.sh` and `packaging/mac/check-notices.py`.

Throughout, **(owner)** marks something the owner decided. **(call)** marks a proposal the owner heard and approved as
part of a section.

## 1. Why

On 2026-11-06, during the owner's checks of the toolbar on the test build `1.1.0-toolbar.4`, the owner found that
clicking a format button (Colour, Line Width) with a shape selected ends the selection and hides the menus. The
headless tests missed it, because they opened the menus from code rather than by clicking. Claude had no way to look
at the real app on the Mac, or drive it, before handing the build over, so the owner was the first to see it.

The owner asked for Avalonia's Developer Tools (`avdt`) in test builds, so Claude can catch such problems before the
owner's checks. (owner)

## 2. Decisions (owner, 2026-11-06)

| Question | Decision |
|---|---|
| What reaches what `avdt` can't | `avdt` for inspecting the app and for clicks and keys on its controls. The Claude app's computer use handles clicks at a point and drags on the page, the inside of an open menu, the menu bar, the app's windows and dialogs, and the look. The other two choices were `avdt` alone, and an input hook of CoffeePDF's own in test builds. |
| Which builds carry it | Only builds that ask: `COFFEEPDF_DEVTOOLS=1` for `package.sh`, `-p:CoffeePdfDevTools=true` for a build. It's off everywhere else, Debug included. The other two choices were every ad-hoc package, and every ad-hoc package and Debug build. |
| Notices | None for the developer tools. A build that carries them never leaves the owner's Mac and is never published. `check-notices.py` leaves out, in such a build only, `AvaloniaUI.DiagnosticsSupport` and the packages only it brings in, and checks everything else as before (§5). The other two choices were a notices file of their own, and sections in `THIRD-PARTY-NOTICES.md`. |
| How sessions get the MCP server | A project `.mcp.json`, committed. The other two choices were the owner's own Claude Code configuration, and a script for each check. |

## 3. Avalonia's Developer Tools (checked 2026-11-06)

Checked against Avalonia's documentation, the 2.2.3 packages, and a throwaway Avalonia 12.1.4 app on the owner's Mac.
The app was built in Claude's scratchpad and kept out of the repository.

- **The tool.** Avalonia Developer Tools 2.2.3 is a .NET global tool, the package `AvaloniaUI.DeveloperTools`, run as
  `avdt`. It's installed on the owner's Mac (in `~/.dotnet/tools`, and linked into Homebrew's `bin`) and signed in.
- **Its licence.** It needs an Avalonia Plus subscription or higher, which the owner's Avalonia Pro includes. Its MCP
  server has accepted Plus since 2.2.1. The tool signs in once with the Avalonia account, and then works offline until
  that session expires.
  - The `AVALONIA_TOOLS_LICENSE_KEY` variable signs in without the dialog, but only with Enterprise licences, so it
    isn't used.
  - The app's side needs no key. A build with the package needs only what CoffeePDF's builds already need.
- **The app's side.** The package `AvaloniaUI.DiagnosticsSupport` 2.2.3, and `.WithDeveloperTools()` on the
  `AppBuilder`. `AttachDeveloperTools()` in the `Application` does the same, and an app calls one or the other, never
  both.
  - The package's MSBuild files add the runtime option `Avalonia.Diagnostics.Diagnostic.IsEnabled`, which turns on
    Avalonia's diagnostic metrics, and set `MetricsSupport` in Debug builds.
  - They stop a build that also references the old `Avalonia.Diagnostics` package, and warn when Avalonia's major
    version isn't 11 or 12.
  - Its version goes with the tool's. Avalonia's notes ask for both to be updated together, and the tool warns when
    they differ.
- **How they meet.** From launch, the app listens on a named pipe of its own, `$TMPDIR/CoreFxPipe_avdt_<pid>`
  (observed). `avdt` and its MCP server find running apps through it, with no key pressed in the app. F12 in the app
  opens `avdt`'s own window, by starting the global tool.
- **The MCP server.** `avdt mcp` runs over stdio, and Avalonia's docs name the server `avalonia_devtools`. Its tools,
  from its own `tools/list`:
  - `attach-to-app` lists the running apps, or attaches to one by its `id`, the process id. A new attach makes every
    node id stale.
  - `attach-to-file` opens a XAML file in Avalonia's previewer instead.
  - `tree` gives a node's children (logical, visual or merged, to a depth), and `ancestors` its parents. `search`
    finds nodes by type or `x:Name`.
  - `props` reads a node's properties, and `set-prop` sets one. `styles` gives the styles that apply, and
    `pseudo-class` lists a node's pseudo-classes or sets one.
  - `resources`, `assets` and `open-asset` read the app's resources and embedded files.
  - `screenshot` returns a PNG of one node.
  - `input` sends `Click`, `KeyDown`, `KeyUp` or `Text` to a node, and `action` does `Focus`, `Enable`, `Disable` or
    `BringIntoView`.
- **What the throwaway app showed.** It had a text box, three buttons (one with a `MenuFlyout`) and a log of the
  window's events:
  - `input Click` on a button went through the window as a real click does in Avalonia: a tunnelling `PointerPressed`
    at the button's centre with the button as its source, then the release and `Click`. Focus moved from the text box
    to the button. So it goes through Avalonia's hit-testing and focus, rather than only raising `Click`.
  - `action Focus`, then `input Text`, typed into the text box, and `KeyDown Return` reached it.
  - `Click` on the menu's button opened the menu (the button showed `:flyout-open`). The open menu was in no tree
    (logical, visual or merged, at full depth), and `search` found no `MenuItem`. `KeyDown Escape` closed it.
  - `screenshot` renders the node itself, not the screen: no popups and nothing native.
- **What a publish carries.** The throwaway app, published as `package.sh` publishes CoffeePDF, carried four more
  packages in its `deps.json`. All of them are managed code inside the single-file binary, and none is a native
  library:
  - `AvaloniaUI.DiagnosticsSupport` 2.2.3, which is commercial and Avalonia's. Its package names no licence.
  - `Microsoft.Extensions.Logging.Abstractions` 8.0.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0 and
    `Microsoft.IO.RecyclableMemoryStream` 3.0.1, all MIT.
  - The binary holds the file name `AvaloniaUI.DiagnosticsSupport.Avalonia.dll`. `Avalonia.Base` and
    `Avalonia.Controls` name that assembly in an `InternalsVisibleTo`, so every Avalonia binary holds the package's
    name. Only the file name marks a binary that carries the developer tools.

## 4. The build (call)

- **`CoffeePdf.csproj`** gets the property `CoffeePdfDevTools`, which is `false` unless a build sets it. When it's
  `true`, the project:
  - references `AvaloniaUI.DiagnosticsSupport`. Its version is pinned in `Directory.Packages.props` (2.2.3), with a
    note that it moves with `avdt`'s;
  - defines `COFFEEPDF_DEVTOOLS`.
- **`Program.BuildAvaloniaApp`** calls `.WithDeveloperTools()` under `#if COFFEEPDF_DEVTOOLS`, with Avalonia's defaults.
  So F12 opens `avdt`'s window, and `avdt mcp` finds the app from launch.
- **Nothing else changes.**
  - `CoffeePdf.Core` is untouched, so `ArchitectureTests` holds.
  - The headless tests build their own `AppBuilder` (`Headless.BuildAvaloniaApp`), so they never attach the developer
    tools, and they build without the property.
- **Running from the source:** `dotnet run --project CoffeePdf -p:CoffeePdfDevTools=true -- <file.pdf>`.
- **One `obj` folder serves both builds.** Restore and compilation follow the property. If a build ever carried the
  wrong one, `package.sh`'s checks (§5) would stop it.
- **CI.** The macOS leg builds the app once more, after its tests, with the property set
  (`dotnet build CoffeePdf/CoffeePdf.csproj --configuration Release -p:CoffeePdfDevTools=true`). That way the
  `#if COFFEEPDF_DEVTOOLS` code compiles under `TreatWarningsAsErrors` and doesn't rot. None of CI's packages sets it,
  neither a tag's nor a run by hand.

## 5. The package (call)

- **`package.sh`** takes `COFFEEPDF_DEVTOOLS`, an environment option like its others. `1` asks for the developer tools.
  Any other value that isn't empty stops it, so a typo can't make a build without them. With it, `package.sh`:
  - stops when `SIGN_IDENTITY` is set, since a signed build is a release candidate;
  - stops when the version has no label (`1.2.3`), or a label beginning with `rc` in any case, as a release
    candidate's does (`1.2.3-rc1`, `1.2.3-RC1`). So `1.1.0-toolbar.5` is allowed;
  - publishes with `-p:CoffeePdfDevTools=true`;
  - names the app `CoffeePDF-devtools.app`, in `packaging/mac/out` and in the disk image, so it never takes a plain
    build's place, and the disk image `CoffeePDF-<version>-devtools.dmg`;
  - warns as loudly as it does for a build without a signature: "WITH the developer tools: any process running as you
    can read and drive this app. Never hand it beyond this Mac."
- **`check-notices.py`** gets a `--devtools` flag, which `package.sh` passes when `COFFEEPDF_DEVTOOLS=1`:
  - **Without the flag,** a `deps.json` that lists `AvaloniaUI.DiagnosticsSupport` is a problem: only a test build made
    with `COFFEEPDF_DEVTOOLS=1` may carry the developer tools.
  - **With the flag,** a `deps.json` that doesn't list it is a problem: `COFFEEPDF_DEVTOOLS=1` asked for the developer
    tools, but the publish doesn't carry them.
  - **With the flag, the notices check leaves out** `AvaloniaUI.DiagnosticsSupport`, every package that only it
    brings in, and the native libraries only those bring. Those are found from the dependencies `deps.json` lists for
    each library, starting from the app's own project, matching package names in any case, as NuGet does. A package
    that the app also reaches without passing through `AvaloniaUI.DiagnosticsSupport` still needs its section, but not
    its version, since NuGet takes the highest version either asks for. Everything else stays checked.
  - **`test_check_notices.py`** covers each case on made-up `deps.json` files:
    - a build without the flag that carries the developer tools;
    - a build with the flag that doesn't carry them;
    - the packages left out, and nothing else;
    - a package that both the app and `AvaloniaUI.DiagnosticsSupport` bring in, which still needs its section, though
      not the version the tools raised it to;
    - a native library that only the developer tools bring;
    - dependencies spelled in another case;
    - a build with the flag that is missing another component's notice, which still stops.
- **`package.sh` then checks the binary itself,** on the bytes that ship: `AvaloniaUI.DiagnosticsSupport.Avalonia.dll`
  must be in a binary made with `COFFEEPDF_DEVTOOLS=1`, and in no other (§3 says why the file name is the test).
- **What doesn't change.**
  - The check that only native code sits in `Contents/MacOS` passes as it is, since nothing native is added.
  - `THIRD-PARTY-NOTICES.md` doesn't change. A test build's `Contents/Resources` carries it, as a release's does.
- **Exposure.** A build with the developer tools listens on its pipe from launch. Any process running as the owner can
  then read the app's tree and drive it, and the tree holds the open files' names in the titles, the forms' values and
  any markup text being typed. avdt's `screenshot` also renders the PdfViewer node, which is the page itself. That's acceptable for a test build on the owner's Mac, and it's why a release must never
  carry them.
- **The guards, together.** A release can't carry the developer tools for three independent reasons:
  - a signed build refuses them;
  - `check-notices.py` without `--devtools` refuses a publish that carries them;
  - the binary check refuses the file name.

## 6. Claude's pass before a hand-off (call)

### 6.1 Setup, once

- **The MCP server.** `.mcp.json` at the repository's root declares `avalonia_devtools`, which runs `avdt mcp` over
  stdio. Every CoffeePDF session, in any worktree, offers it, and Claude Code asks the owner to approve a project's
  server before its first use.
  - It names the command `avdt`, not a path, so it holds no home folder. `avdt` must be on the `PATH` that Claude
    Code's processes see.
- **Signing in.** `avdt` is signed in to the owner's Avalonia account, which is done on this Mac. When the session
  expires, the tool asks again, and only the owner signs in.
- **Computer use.** The owner turns on Computer use in the Claude app (Settings › This computer › Computer use). In each
  session, Claude asks for access to CoffeePDF, and the owner approves it.

### 6.2 Which builds carry them

Test packages handed to the owner carry the developer tools. When one of the owner's checks finds something, Claude can
attach to the owner's running copy and read its state at that moment. Release candidates and releases never carry
them (§5).

### 6.3 Each hand-off

1. **Build** the test package with `COFFEEPDF_DEVTOOLS=1` and the licence key, such as
   `COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 1.1.0-toolbar.5`.
2. **Launch it walled off,** as `Headless` walls off the tests:
   - First, the owner quits any other CoffeePDF, which may hold unsaved markups. Claude never quits or kills a
     CoffeePDF it didn't start. The test build has the same bundle identifier, which computer use and the Dock go by
     (found while planning).
   - Claude starts the bundle's binary directly (`CoffeePDF-devtools.app/Contents/MacOS/CoffeePDF <file.pdf>`), not
     through `open`, which would let LaunchServices hand the file to a running CoffeePDF with the same bundle
     identifier.
   - `COFFEEPDF_SETTINGS_DIR`, `COFFEEPDF_LOG_DIR` and `HEALDAN_HELPER_DEV_ROOT` point at folders in Claude's
     scratchpad.
   - The settings there give the markup author `Test Reviewer` and have already answered the first launch's Healdan
     question.
   - The PDFs are ones the tests' fixtures make, written to the scratchpad. The launch refuses a PDF outside its folder.
   - Nothing touches the owner's settings, Helper or documents, and no PDF goes into the repository.
3. **Attach** with `attach-to-app`, giving the process id.
4. **The pass.** Claude goes through the pull request's list of the owner's checks, item by item:
   - **By `avdt`:**
     - a click on each toolbar button, in each state that matters: nothing selected, a tool armed, a shape selected,
       text being edited;
     - after each click, the window's states (`:shape-selected`, `:editing-text`), the button's `:flyout-open`, and
       which element has focus;
     - shortcuts, by `KeyDown`, and typed values in the page and zoom boxes;
     - each button's accessible name and tooltip.
   - **By computer use:**
     - drawing markups on the page and selecting them;
     - choosing inside an open menu, such as a colour or a line width;
     - the menu bar, and the app's windows and dialogs;
     - resizing, for the toolbar's overflow;
     - screenshots in light and dark.
   - **Saving:** after a save, the file is read back through the core, for its markups and their author.
5. **The hand-off note.** The pull request's list marks each check Claude ran, with the build it ran on and the result.
   What's left is what only the owner can judge or reach: the look beside Preview's, sharpness on Retina, VoiceOver,
   anything with the real Helper and server, and anything computer use couldn't reach.
6. **Quit** the app. The scratch folders belong to the session.

### 6.4 Where the headless tests still do the job

- **Anything a test can assert stays a headless test,** written first (the v1 spec's §8).
- **The tests click by position,** as the toolbar's `ToolbarTesting.Click` does (a move, a press, a release). They never
  call a handler or open a menu from code instead of clicking, which is the gap that let the toolbar's bug through.
- **A bug the pass finds gets a headless test that fails before its fix,** when headless input can reproduce it. When it
  can't (a native popup, a window becoming active, the menu bar), the pull request says so, and the check stays in the
  pass for later hand-offs.
- **The developer tools come before the owner's checks.** They replace neither a test nor one of the owner's checks.

### 6.5 Limits

- **`avdt`'s click is Avalonia's input, not the Mac's events.** It goes through Avalonia's hit-testing and focus, but
  not through macOS. The first click on a window that isn't active, the menu bar and the native layer aren't in its
  reach.
- **An open menu shows only through its button's `:flyout-open`.** `avdt` reaches neither its items nor its look.
- **Computer use needs the owner's approval in each session.** Whether its background clicks reach CoffeePDF without
  bringing it to the front is unproven (§7).

## 7. Proving it (call)

Once the build and the package work, the toolbar's bug is the test of the whole design:

1. Claude builds a package with the developer tools from a local merge of this work into the toolbar's branch, at the
   commit that `1.1.0-toolbar.4` was built from (the toolbar's session names it). The merge is never committed or
   pushed.
2. On that build, with a shape selected, Claude clicks Colour twice: once by `avdt`'s `Click`, and once by computer use.
   It records which of the two reproduces the owner's finding: the selection ends and the menus hide.
3. Claude does the same on the toolbar's fixed commit, where both should pass.
4. The same run records:
   - whether computer use's background clicks reach CoffeePDF without bringing it to the front;
   - what the developer tools cost at launch, in the CPU time to the first window and in memory, beside a build
     without them, so that the owner's checks on a test build aren't skewed.
5. The findings go in §10.

The outcome sets the pass:

- **If `avdt`'s `Click` reproduces the bug,** clicks on controls are made by `avdt`, which is quicker and exact.
- **If only computer use does,** the pass makes its clicks on controls that can take focus from the viewer (the format
  buttons, and anything like them) by computer use, and §6.5 says why.

## 8. Docs (call)

- **`docs/DEVTOOLS.md`:** the setup, building, launching walled off, attaching, the pass and its limits, and keeping
  `avdt` and the package at one version.
- **`CLAUDE.md`:**
  - the build line, under Build and test;
  - a rule: test builds handed to the owner carry the developer tools, and get Claude's pass first
    (`docs/DEVTOOLS.md`). Release candidates and releases never carry them, which `package.sh` checks;
  - in the third-party notices rule: `check-notices.py` leaves out only the developer tools' packages, and only for a
    build made with `COFFEEPDF_DEVTOOLS=1`.
- **The v1 spec:** §8's "Checked by hand" gains Claude's pass first, and §10 gains `package.sh`'s option.
- **The roadmap's status, and the README's build section.**
- **No path in any of them names the owner's home folder.** They write `~/.dotnet/tools` and
  `$TMPDIR/CoreFxPipe_avdt_<pid>`, so the personal-details hook lets them through.

## 9. Out of scope

- **Linux and Windows.** Their test builds can take the same switch when their phases come.
- **Packages CI makes with the developer tools.**
- **Code of CoffeePDF's own for automation.** `avdt` and computer use do the driving.

## 10. Findings

None yet. The run in §7 records its findings here.
