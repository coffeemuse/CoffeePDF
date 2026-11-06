# Developer tools in test builds

A test build of CoffeePDF can carry Avalonia's Developer Tools, so Claude can inspect and drive the app on the Mac before
handing the build to the owner for checks. Releases never carry them. The design is
[the devtools spec](superpowers/specs/2026-11-06-coffeepdf-devtools-design.md).

## What they are

- **`avdt`**, Avalonia Developer Tools, is a .NET global tool (`AvaloniaUI.DeveloperTools`). It needs an Avalonia Plus
  subscription or higher, which the owner's Pro includes, and signs in once with the Avalonia account.
- **The app's side** is the package `AvaloniaUI.DiagnosticsSupport`, which only a build with `CoffeePdfDevTools=true`
  references. `Program.BuildAvaloniaApp` then calls `.WithDeveloperTools()`.
  - From launch, the app listens on a pipe of its own (`$TMPDIR/CoreFxPipe_avdt_<pid>`), and F12 in it opens `avdt`'s
    window.
  - Any process running as the owner can read the app's tree and drive it through that pipe, which is why a release
    never carries the tools. The tree holds titles, form values and markup text, and `avdt`'s `screenshot` renders the
    PdfViewer node, which is the page itself.
- **`avdt mcp`** is its MCP server, `avalonia_devtools`, which `.mcp.json` declares.
  - `attach-to-app`: lists the running apps, or attaches to one by its process id.
  - `tree`, `ancestors` and `search`: find nodes.
  - `props`, `styles` and `pseudo-class`: read a node, or set a pseudo-class.
  - `screenshot`: a PNG of one node.
  - `input`: `Click`, `KeyDown`, `KeyUp` or `Text`, on a node.
  - `action`: `Focus`, `Enable`, `Disable` or `BringIntoView`.

## Setup, once

1. **`avdt` installed and on the `PATH`.**
   - Install it with `dotnet tool install --global AvaloniaUI.DeveloperTools`. It lives in `~/.dotnet/tools`.
   - `dotnet tool list -g` gives its version, which must be the one `Directory.Packages.props` gives
     `AvaloniaUI.DiagnosticsSupport`.
2. **Signed in.** Start `avdt` once and sign in with the Avalonia account. Only the owner signs in, and again when the
   session expires.
3. **The MCP server.** Approve `avalonia_devtools` when Claude Code asks. A session started before `.mcp.json` existed
   doesn't have it.
4. **Computer use.** In the Claude app, turn on Settings › This computer › Computer use. Claude asks for access to
   CoffeePDF in each session.

## Building a test build with them

```bash
COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 1.1.0-toolbar.5      # CoffeePDF.app, and CoffeePDF-1.1.0-toolbar.5-devtools.dmg
dotnet run --project CoffeePdf -p:CoffeePdfDevTools=true -- file.pdf # from the source
```

`package.sh` refuses `COFFEEPDF_DEVTOOLS=1` in three cases:
- with `SIGN_IDENTITY` set;
- for a version with no label (`1.2.3`);
- for a release candidate's version (`1.2.3-rc1`).

Any other value that isn't empty stops it too.

A release can't carry them, for three independent reasons:
- a signed build refuses them;
- `check-notices.py` without `--devtools` refuses a publish that carries `AvaloniaUI.DiagnosticsSupport`;
- the binary check refuses a binary holding `AvaloniaUI.DiagnosticsSupport.Avalonia.dll`.

`check-notices.py --devtools` leaves out only the developer tools' packages: `AvaloniaUI.DiagnosticsSupport`, and what
only it brings in. A build with them never leaves the owner's Mac (owner, 2026-11-06).

## The pass before a hand-off

Test packages handed to the owner carry the developer tools, and Claude runs this pass on them first. In the commands,
`$SCRATCH` is a folder of the session's own, outside the repository.

1. **The owner quits any other CoffeePDF,** which may hold unsaved markups. Claude never quits or kills a CoffeePDF it
   didn't start. The test build has the same bundle identifier, which computer use and the Dock go by.
2. **Make the PDFs:**
   `COFFEEPDF_PASS_DIR="$SCRATCH/pdfs" dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PassFilesProbe.Writes_the_pass_pdfs"`.
   That writes `plain.pdf` and `with-note.pdf`, the second with a note by another app.
3. **Launch it walled off, in the background:**
   `APP=<the build's CoffeePDF.app> packaging/mac/launch-test-build.sh "$SCRATCH" "$SCRATCH/pdfs/plain.pdf"`.
   - The settings, log and Helper folders go in `$SCRATCH`, with the markup author `Test Reviewer`.
   - It runs until the app quits, and its process id, which is the app's, is in `$SCRATCH/coffeepdf.pid`.
   - Walled off covers CoffeePDF's own files only. What macOS keeps per bundle identifier is shared with the owner's
     install, since the identifier is the same: the app's defaults domain (for example the open panel's last folder),
     and LaunchServices may register the scratch bundle.
4. **Attach:** `attach-to-app` with that id. A new attach makes node ids stale, so search again after one.
5. **Go through the pull request's list of the owner's checks,** item by item:

   | What | How |
   |---|---|
   | A click on a toolbar button, in each state that matters: nothing selected, a tool armed, a shape selected, text being edited | `avdt`'s `input Click`, or computer use where §Limits says so |
   | What a click did | `search` and `props`: the window's pseudo-classes (`:shape-selected`, `:editing-text`, the toolbar's, coffeemuse/CoffeePDF#17), the button's `:flyout-open`, which element has `:focus` |
   | Shortcuts, and typed values in the page and zoom boxes | `action Focus`, then `input Text` or `KeyDown` |
   | Each button's accessible name and tooltip | `props` (`AutomationProperties.Name`, `ToolTip.Tip`) |
   | Drawing markups on the page, and selecting one | computer use: a drag, and a click on the markup's edge |
   | Choosing inside an open menu (a colour, a line width) | computer use |
   | The menu bar, the app's windows and dialogs, resizing for overflow | computer use |
   | The look, in light and dark | computer use's screenshots |

6. **After a save,** read the file back:
   `COFFEEPDF_PASS_DIR="$SCRATCH/pdfs" dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PassFilesProbe.Reads_back_the_pass_pdfs" --logger "console;verbosity=detailed"`.
   It prints each markup and its author, and fails on any author but `Test Reviewer` or the fixture's.
7. **Note it in the pull request.** Mark each check Claude ran on the list of the owner's checks, with the build and the
   result, such as "Claude's pass, 1.1.0-toolbar.5-devtools: passed". What's left is the owner's: the look beside
   Preview's, sharpness on Retina, VoiceOver, the real Helper and server, and anything computer use couldn't reach.
8. **Quit:** `kill "$(cat "$SCRATCH/coffeepdf.pid")"`.

## When the owner's check finds something

The owner's test build carries the developer tools, so Claude can attach to the owner's running copy (`attach-to-app`
lists it) and read its state at that moment: the tree, the properties, the pseudo-classes and the focus. Claude only
reads there. It never sets a property or sends input to the owner's copy unless the owner asks.

## Where the headless tests still do the job

- **Anything a test can assert stays a headless test,** written first.
- **Tests click by position,** as `ToolbarTesting.Click` does (the toolbar's, coffeemuse/CoffeePDF#17: a move, a press, a release). They never call a handler or
  open a menu from code instead of clicking.
- **A bug the pass finds gets a headless test that fails before its fix,** when headless input can reproduce it. When
  it can't (a native popup, a window becoming active, the menu bar), the pull request says so, and the check stays in
  the pass.
- **The pass comes before the owner's checks.** It replaces neither a test nor one of the owner's checks.

## Limits

- **`avdt`'s click is Avalonia's input, not the Mac's events.** It goes through Avalonia's hit-testing and focus, but
  not through macOS: the first click on a window that isn't active, the menu bar and the native layer aren't in its
  reach.
- **An open menu shows only through its button's `:flyout-open`.** `avdt` reaches neither its items nor its look.
- **`screenshot` renders one node,** without popups or anything native.
- **Computer use needs the owner's approval in each session.**

## One version for `avdt` and the package

When `dotnet tool update --global AvaloniaUI.DeveloperTools` brings a new version, the same change moves
`AvaloniaUI.DiagnosticsSupport` in `Directory.Packages.props` to it. Avalonia asks for both to be updated together, and
`avdt` and its MCP server warn when they differ.
