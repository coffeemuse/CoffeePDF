# CoffeePDF Phase 2: Healdan in CoffeePDF Implementation Plan

Written 2026-10-30, and checked before it was committed: every task, applied as written and in order to a scratch copy
of the repository, builds with no warnings.
- Without the key, the core's 160 tests pass, and the app's 19 pass with 80 skipped.
- With the key, the core's 160 and the app's 98 pass, with 1 skipped (the keyless probe), and the PdfViewer's tests
  passed five runs more.
- Only the fake Helper was used, never the real Helper or Healdan. The edits to doc comments alone weren't checked.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** With Healdan support on, CoffeePDF works with Healdan documents through the Healdan Helper's local channel.
It opens a copy in its mode (editable, read-only, or with the Helper away), keeps its lock file, follows each save to
Healdan, saves as a new version or a new document, files a local PDF with Save to Healdan, shows Properties, and lets
the copy go when its window closes. ⌘O and ⇧⌘S go to the Helper's windows while it answers. Everything is built and
tested against a fake Helper, without touching Healdan.

**Architecture:** The logic goes in `CoffeePdf.Core`, and its tests run on macOS and Linux:
- `Helper/`: the Helper's folders, `channel.json`, `ChannelRoutes`, and the channel client, `HelperChannel`;
- `Documents/`: a window's mode, title, bar and commands (`WindowDocument`), and the save-follow loop (`SaveFollower`);
- `Locks/`: the lock file beside an editable copy (`CopyLock`);
- `Logging/`: the log (`CoffeeLog`).

The app's `DocumentWindow` holds one `WindowDocument` and replaces it as the Helper answers. A test's window gets a
`HealdanLink` to a `FakeHelper`, which serves exactly `ChannelRoutes` over HTTP on 127.0.0.1.

Every save still goes through `DocumentWindow.SaveToAsync`: the PdfViewer saves to memory, `MarkupAuthor.Stamp` writes
the author, and `AtomicFile.Write` renames the result over the file. Save to Healdan writes its copy for the Helper
through the same path, with `asCopy`.

**Tech Stack:**
- .NET 10 and C#.
- Avalonia 12.1.4, with `Avalonia.Controls.PdfViewer` 12.0.1.
- `System.Net.Http` for the channel client, and `System.Net.HttpListener` for the fake Helper. No new packages.
- macOS Launch Services, to tell whether the Helper is installed.
- xUnit 2.9.3 and Avalonia.Headless.

**Spec:** `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`. Read §4, §5, §6, §8, §9, §13 and all of §16 first.
The channel is Healdan's, so read its Developer Guide's Desktop Clients page too (`site/content/developer/desktop-clients.md`
in `Healdan-Project/healdan`), especially "The Channel" and "The Helper's Files".

## Questions for the owner

The spec leaves these open, or today's Helper does something other than the spec says. The plan doesn't decide them.
Each says what the tasks are written to assume, and which task changes if the answer is different.

1. **The title's form (§6.1).** The spec shows `Settlement Agreement — #1234 v2.1`. The Helper's `title` is the copy's
   file name without `.pdf`, `Settlement Agreement #1234 v2.1`, or `… v2.1 (read-only)` for a read-only copy, and the
   LibreOffice extension shows it as it is.
   - *Assumed:* §6.1's form, built from `title` and `docRef`. The name is `title` without ` #1234 v2.1` and
     ` (read-only)`. A read-only copy has the same title, and its bar says it's read-only. When `title` doesn't end in
     its `docRef`, the name comes from the copy's file name.
   - *Changes:* Task 5 (`WindowDocument.Title`).
2. **Who holds it (§6.1).** The read-only bar names the holder and the computer ("checked out by Jane on MacBook-Pro").
   The state's `holdBy` is the holder's name only, since the Helper takes `hold.by.name`.
   - *Assumed:* "Read-only — checked out by Jane". Naming the computer needs a field in the state, from Healdan's
     milestone (Phase 3).
   - *Changes:* Task 5 (`WindowBar.HeldBy`).
3. **A refused save's state (§6.9).** The spec says CoffeePDF's next `/v1/state` shows the copy as read-only after a
   `409`. Today's Helper ends the check-out on a refusal (the record's `CheckedOutHere` becomes false) but leaves the
   state's `readOnly` false, so nothing in the state changes. A copy reopened from the Helper's Not Yet in Healdan is
   likely the same, since its check-out has ended too and no field of the state says so.
   - *Assumed:* the fake Helper answers `readOnly: true` as §6.9 says. Healdan's milestone then makes the real Helper
     do so: an item for §4.4.
   - *Changes:* nothing in CoffeePDF if Healdan agrees. If the owner wants CoffeePDF to cope with today's Helper
     instead, the state needs another field, so it's still a Healdan change.
4. **The Helper's errors (§6.9).** The spec says CoffeePDF shows the error's message. The channel's errors carry only a
   code, such as `{"error": "internal-error"}` with status 500.
   - *Assumed:* CoffeePDF's own words, with the status and code: "Healdan Helper couldn’t show its Open window." and
     "Healdan Helper answered 500 (internal-error)". Then the Mac's own box, as the LibreOffice extension falls back to
     its own.
   - *Changes:* Tasks 12 and 13.
5. **A read-only copy while the Helper is away (§6.1).** The Helper-away row has markup on. A copy named
   `… (read-only).pdf` is read-only all the same.
   - *Assumed:* such a copy stays read-only, and gets no lock file. Only an editable copy's name gets markup, a lock
     file and its saves followed.
   - *Changes:* Task 5 (`WindowDocument.Commands`, `HoldsLock`).
6. **When the Helper-away bar goes (§6.9).** The spec says once the Helper answers again. Nothing asks it while the user
   doesn't save.
   - *Assumed:* the window asks `/v1/state` again each time it comes forward, and each save's following asks too.
     There's no polling of its own.
   - *Changes:* Task 9.
7. **Turning Healdan support on (§5).** The spec says only that turning it off takes effect for each copy as its
   window closes.
   - *Assumed:* turning it on takes effect at once. Local windows get the Healdan menu, and ⌘O and ⇧⌘S go to the
     Helper. A copy opened with support off asks the Helper and takes its mode. Turning it off takes effect at once for
     local windows too.
   - *Changes:* Tasks 9 and 13.
8. **Leaving a copy through Save As.** §6.6 lets the old copy go when Save as New Document moves the window. A Save As
   to a local file through the Mac's panel (after the Helper's `local`, or with the Helper away) also moves the window
   off its copy.
   - *Assumed:* it lets the copy go the same way, with its lock file removed and `/v1/closed`, and the window becomes
     local.
   - *Changes:* Tasks 10 and 11 (`AfterSave`).
9. **Save as New Version with the Helper away (§6.5).** The version can't be asked for.
   - *Assumed:* CoffeePDF says so and doesn't save, rather than saving a snapshot the user didn't ask for. The
     LibreOffice extension saves locally without saying.
   - *Changes:* Task 11.
10. **Smaller ones.**
    - *The Saving to Healdan… indicator (§6.4):* assumed from the save's write until an answer that isn't `pending`,
      since the first `/v1/saved` can take 120 seconds to answer.
    - *Time limits:* §4.3 says only that the window routes never time out. Assumed are the LibreOffice extension's
      limits for the rest: 10 seconds, 135 for `/v1/saved` (which answers within 120), and 660 for `/v1/file`.
    - *A copy the Helper doesn't know* (`{"known": false}` for a file in `Copies/`): assumed local, and still kept out
      of Open Recent.
    - *The first launch's question (§5):* assumed "Use CoffeePDF with Healdan?", with Not Now and Use with Healdan. An
      answer counts as asked, and "installed" means Launch Services knows `com.healdan.helper`.
    - *The name the Save window starts with:* assumed a copy's name without its doc ref, and a local file's name
      without `.pdf`.
    - *The log on Linux:* §6.9 names only the Mac's place. Assumed is the settings folder, until the Linux phase.

These are for Healdan's milestone (Phase 3) rather than questions for this plan:
- §4.4 should add the state's read-only after a refusal (question 3), and the holder's computer if wanted (question 2).
- Save as New Version on a document with no new changes marks the next save as a version (`MarkNextVersion`), and
  `/v1/saved` answers `pending` while that version is still to go. The acceptance's step 3 (§2.5) should mark up before
  Save as New Version.

## What the Helper's channel does today

Read from Healdan's repository on 2026-10-30 (`clients/helper/Healdan.Helper.Core/Channel/`, `Cycle/CopyCycle.cs`,
`Copies/CopyNames.cs`, `clients/libreoffice/healdan_logic.py`, and the Desktop Clients page). The fake Helper follows
it, and so do the tasks.

- **`channel.json`** is in the Helper's support folder, mode 600: `{"port", "secret", "pid"}`. The secret is 48
  lowercase hex digits, and the file goes when the Helper stops.
- **Every route is a `POST` with a JSON body** of up to 64 KB, except `GET /v1/ping`. Each sends the secret as
  `X-Healdan-Secret`. A missing or wrong one is answered `403`.
- **Errors are `{"error": "<code>"}`:** `400 bad-request`, `403 forbidden`, `404 not-found`, `405 method-not-allowed`,
  `413 too-large`, and `500 internal-error` for any failure in a route. A failed filing is one, and the Helper also tells
  the user why in a notification and deletes the temporary file.
- **A copy's state** is `{"known": true, "docNumber", "title", "docRef", "readOnly", "path", "holdBy", "pending"}`, or
  `{"known": false}`.
  - `title` is the copy's file name without its extension.
  - `holdBy` is the holder's name, for a read-only copy.
- **The routes that take a path** act only on a file directly in `Copies/`, with links followed and names compared in
  Unicode's composed form. Any other path is `{"known": false}`, ignored, or, for `/v1/properties`, `file`.
- **`/v1/saved`** answers once Healdan has every save written so far, or after 120 seconds with the state as it is.
- **The LibreOffice extension's limits:** 3,600 seconds for the window routes, 660 for `/v1/file`, 135 for
  `/v1/saved`, and 10 for the rest. It follows a save with pauses of 2, 5, 15, 30 and then 60 seconds.
- **Copies' names** are `<name> #<doc #> v<fileVersion>.pdf`, or `… v2.1 (read-only).pdf` for a read-only copy, in at
  most 241 bytes.
- **A refusal** (`held-elsewhere`, `not-held`, `checked-out`, `locked`) ends the check-out and keeps the copy, but the
  state's `readOnly` stays false (question 3).

## Global Constraints

- **SDK and versions:** the SDK is `10.0.100` with `rollForward: latestFeature`, and projects target `net10.0`.
  Package versions stay as they are in `Directory.Packages.props`. Avalonia packages are `12.1.4`, and
  Avalonia.Controls.PdfViewer is `12.0.1`. This phase adds no package.
- **CoffeePDF holds no Healdan token and calls no server** (§4.1). It talks only to the Helper's channel on
  `127.0.0.1`.
- **`ChannelRoutes` lists every route CoffeePDF calls** (§9). The client calls only those, and `FakeHelper` serves only
  those. A test fails if the client calls any other.
- **The window routes never time out** (§4.3): `/v1/open-dialog`, `/v1/save-new` and `/v1/properties`.
- **`CoffeePdf.Core` references no Avalonia assembly** (`ArchitectureTests`).
- **The Avalonia licence key** comes only from `AVALONIA_LICENSE_KEY`, and is never committed, logged or printed.
- **Compiler settings:** `TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings` are on everywhere. An `async` method
  with no `await` is a warning, and so an error.
- **Every save goes through `DocumentWindow.SaveToAsync`.** The PdfViewer's `AllowDocumentSaving` and `AutoSave` stay
  `False`.
- **CoffeePDF's own PDFium** (`libcoffeepdfium`) is untouched by this phase.
- **Tests never touch the real Helper, settings or log.**
  - `Headless` sets `HEALDAN_HELPER_DEV_ROOT` and `COFFEEPDF_LOG_DIR` to folders of the run's own, so the real Helper
    on the Mac running the tests is never reached, and an app window starts with the Helper away.
  - The flows' tests give each window a `FakeHelper`'s `HealdanLink`, with its own folders, settings and log.
  - A core test that changes an environment variable is in the `Environment variables` collection, which xUnit runs
    one test at a time. Phase 1's `SettingsLocationTests` joins it.
- **Nothing logs the channel's secret or a file's contents** (§6.9). `ChannelInfo.ToString` leaves the secret out. An
  exception is logged by its type, since its message can quote a request.
- **Healdan's copies stay out of Open Recent** (§6.10).
- **Personal details stay out of the repository** (CLAUDE.md):
  - Tests save with the made-up author `Test Reviewer`, and other made-up names such as `Zoë Q. Reviewer` are fine.
  - No PDF is committed.
  - The pre-commit hook is never bypassed. If it rejects a commit, take the detail out.
- **Tests wait for conditions, never for fixed times.** A bound on a wait, such as `WaitAsync(TimeSpan.FromSeconds(10))`
  around a task that should finish, is fine. So is a time limit given to the client under test.
- **Tests that need the PdfViewer** use `[PdfViewerFact]` and `[Trait("Category", "PdfViewer")]`. Every test that
  creates a `DocumentWindow` needs it.
- **macOS words:**
  - Text uses `…` (U+2026), and curly quotes and apostrophes (“ ” ’), as Phase 1's does.
  - The app's name for the Helper is "Healdan Helper".
- **Pushes go through the owner.** Never push, tag or open a pull request without the owner's go-ahead.

---

## File Structure

| File | Responsibility |
|---|---|
| `CoffeePdf.Core/Helper/HelperFolders.cs` | The Helper's support folder, `Copies/`, `Temp/` and `channel.json`, and whether a file is a copy |
| `CoffeePdf.Core/Helper/ChannelInfo.cs` | Reads `channel.json` |
| `CoffeePdf.Core/Helper/CopyName.cs` | A copy's file name: its document name, doc ref and read-only mark |
| `CoffeePdf.Core/Logging/CoffeeLog.cs` | The log, and where it lives |
| `CoffeePdf.Core/Helper/ChannelRoutes.cs` | Every route CoffeePDF calls, with its body, answer and time limit |
| `CoffeePdf.Core/Helper/ChannelAnswers.cs` | A copy's state, and the window routes' answers |
| `CoffeePdf.Core/Helper/HelperExceptions.cs` | The Helper unreachable, or answering with an error |
| `CoffeePdf.Core/Helper/HelperChannel.cs` | The channel client |
| `CoffeePdf.Core/Helper/HealdanLink.cs` | What a window needs of Healdan: support on or off, the folders, the channel and the log |
| `CoffeePdf.Core/Documents/WindowDocument.cs` | A window's file, its mode (§6.1), and what the mode decides |
| `CoffeePdf.Core/Documents/WindowCommands.cs` | What a mode allows, and its bar |
| `CoffeePdf.Core/Documents/DocumentTitle.cs` | Adds a copy's title |
| `CoffeePdf.Core/Documents/SaveFollower.cs` | The save-follow loop (§6.4) |
| `CoffeePdf.Core/Locks/CopyLock.cs` | The lock file (§6.8) |
| `CoffeePdf.Core/Settings/SettingsStore.cs`, `FirstLaunch.cs` | Healdan support's change event and the first launch's question (§5) |
| `CoffeePdf/Platform/CoreFoundation.cs`, `HelperInstall.cs` | Core Foundation's strings, shared, and whether the Helper is installed |
| `CoffeePdf/Windows/DocumentWindow.axaml(.cs)` | The modes, the bar, the lock file, following saves, the flows of §6, and the Healdan menu |
| `CoffeePdf/Windows/SettingsWindow.axaml(.cs)` | The Healdan support box |
| `CoffeePdf/App.axaml.cs` | The log, the Healdan link, the first launch's question, Open Recent without copies, and quitting with copies open |
| `CoffeePdf.Core.Tests/Fakes/FakeHelper.cs` | The fake Helper, which the app's tests compile too |
| `CoffeePdf.Core.Tests/Helper/…`, `Logging/…`, `Documents/…`, `Locks/…`, `Settings/…` | The core's tests |
| `CoffeePdf.Tests/Windows/Healdan…Tests.cs`, `SaveToHealdanTests.cs` | The app's tests of the flows |

---

### Task 1: The Helper's folders, `channel.json`, and copies' names

**Files:**
- Create: `CoffeePdf.Core/Helper/HelperFolders.cs`, `CoffeePdf.Core/Helper/ChannelInfo.cs`, `CoffeePdf.Core/Helper/CopyName.cs`
- Test: `CoffeePdf.Core.Tests/Helper/HelperFoldersTests.cs`, `CoffeePdf.Core.Tests/Helper/HelperFoldersLocationTests.cs`,
  `CoffeePdf.Core.Tests/Helper/ChannelInfoTests.cs`, `CoffeePdf.Core.Tests/Helper/CopyNameTests.cs`
- Modify: `CoffeePdf.Core.Tests/Settings/SettingsLocationTests.cs` (its collection)

**Interfaces:**
- Produces:
  - `sealed class HelperFolders(string support)`: `const string DevRootVariable = "HEALDAN_HELPER_DEV_ROOT"`,
    `string Support`, `string Copies`, `string Temp`, `string ChannelFile`, `static HelperFolders ForThisUser()`, and
    `bool HoldsCopy(string path)`.
  - `sealed record ChannelInfo(int Port, string Secret, int Pid)`: `static ChannelInfo? Read(string file)`. Its
    `ToString()` leaves the secret out.
  - `static class CopyName`: `CopyNameParts? Parse(string path)` and `string? NameIn(string title, string docRef)`.
  - `sealed record CopyNameParts(string Name, string DocRef, bool ReadOnly)`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Helper/HelperFoldersTests.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §4.3 and §6.1: the Helper's files, and which files are its local copies.</summary>
public sealed class HelperFoldersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_Helpers_files_are_in_its_support_folder()
    {
        var folders = new HelperFolders(_dir);

        Assert.Equal(Path.Combine(_dir, "Copies"), folders.Copies);
        Assert.Equal(Path.Combine(_dir, "Temp"), folders.Temp);
        Assert.Equal(Path.Combine(_dir, "channel.json"), folders.ChannelFile);
    }

    [Fact]
    public void Only_a_file_directly_in_Copies_is_a_copy()
    {
        var folders = new HelperFolders(_dir);

        Assert.True(folders.HoldsCopy(Path.Combine(folders.Copies, "Settlement Agreement #1234 v2.1.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(folders.Copies, "inner", "Report.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(_dir, "Copies from example.com", "Report #1 v1.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(_dir, "Report.pdf")));
    }

    [Fact]
    public void A_copy_reached_through_a_link_is_a_copy()
    {
        var folders = new HelperFolders(_dir);
        Directory.CreateDirectory(folders.Copies);
        var link = Path.Combine(_dir, "linked");
        Directory.CreateSymbolicLink(link, folders.Copies);

        Assert.True(folders.HoldsCopy(Path.Combine(link, "Report #1 v1.pdf")));
    }

    [Fact]
    public void Names_are_compared_in_composed_form_as_the_Helper_compares_them()
    {
        var folders = new HelperFolders(Path.Combine(_dir, "Café"));

        Assert.True(folders.HoldsCopy(Path.Combine(_dir, "Café", "Copies", "Report #1 v1.pdf")));
    }
}
```

`CoffeePdf.Core.Tests/Helper/HelperFoldersLocationTests.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
public sealed class HelperFoldersLocationTests : IDisposable
{
    private readonly string? _root = Environment.GetEnvironmentVariable(HelperFolders.DevRootVariable);
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, _root);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _xdg);
    }

    [Fact]
    public void The_dev_root_holds_everything_when_its_set()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, "/tmp/helper-dev");

        Assert.Equal("/tmp/helper-dev", HelperFolders.ForThisUser().Support);
    }

    [Fact]
    public void Otherwise_its_the_platforms_own_place()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, null);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/tmp/xdg-data");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "Healdan Helper")
            : Path.Combine("/tmp/xdg-data", "healdan-helper");
        Assert.Equal(expected, HelperFolders.ForThisUser().Support);
    }
}
```

`CoffeePdf.Core.Tests/Helper/ChannelInfoTests.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

public sealed class ChannelInfoTests : IDisposable
{
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string contents)
    {
        var path = Path.Combine(_dir, "channel.json");
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void It_reads_the_port_the_secret_and_the_process() =>
        Assert.Equal(new ChannelInfo(52100, Secret, 4321), ChannelInfo.Read(Write($$"""{"port": 52100, "secret": "{{Secret}}", "pid": 4321}""")));

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"port": 0, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 1}""")]
    [InlineData("""{"port": "52100", "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "short", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdeg", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 0}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    public void Anything_but_the_Helpers_shape_is_no_channel(string contents) => Assert.Null(ChannelInfo.Read(Write(contents)));

    [Fact]
    public void A_missing_file_is_no_channel() => Assert.Null(ChannelInfo.Read(Path.Combine(_dir, "channel.json")));

    [Fact]
    public void Printing_it_leaves_the_secret_out() => Assert.DoesNotContain(Secret, new ChannelInfo(52100, Secret, 4321).ToString());
}
```

`CoffeePdf.Core.Tests/Helper/CopyNameTests.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>The Desktop Clients page's "Local Copies": <c>&lt;name&gt; #&lt;doc #&gt; v&lt;version&gt;.pdf</c>.</summary>
public sealed class CopyNameTests
{
    [Fact]
    public void A_copys_name_gives_its_document_name_and_doc_ref() =>
        Assert.Equal(new CopyNameParts("Settlement Agreement", "#1234 v2.1", ReadOnly: false),
            CopyName.Parse("/support/Copies/Settlement Agreement #1234 v2.1.pdf"));

    [Fact]
    public void A_read_only_copy_says_so() =>
        Assert.Equal(new CopyNameParts("Settlement Agreement", "#1234 v2.1", ReadOnly: true),
            CopyName.Parse("/support/Copies/Settlement Agreement #1234 v2.1 (read-only).pdf"));

    [Fact]
    public void The_doc_ref_is_the_last_one_in_the_name() =>
        Assert.Equal(new CopyNameParts("Bill #7 draft", "#1234 v3", ReadOnly: false), CopyName.Parse("Bill #7 draft #1234 v3.pdf"));

    [Theory]
    [InlineData("Report.pdf")]
    [InlineData("Report #1234.pdf")]
    [InlineData("Lease #1234 v2.1.docx")]
    public void Other_names_arent_a_copys(string name) => Assert.Null(CopyName.Parse(name));

    [Fact]
    public void The_Helpers_title_gives_the_document_name()
    {
        Assert.Equal("Settlement Agreement", CopyName.NameIn("Settlement Agreement #1234 v2.2", "#1234 v2.2"));
        Assert.Equal("Settlement Agreement", CopyName.NameIn("Settlement Agreement #1234 v2.1 (read-only)", "#1234 v2.1"));
        Assert.Null(CopyName.NameIn("Settlement Agreement #1234 v2.1", "#1234 v2.2"));
        Assert.Null(CopyName.NameIn("Settlement Agreement", ""));
    }
}
```

In `CoffeePdf.Core.Tests/Settings/SettingsLocationTests.cs`, put Phase 1's class in the same collection, replacing its
summary with the first two lines here. Its tests change `COFFEEPDF_SETTINGS_DIR`, which `CoffeeLog.Folder()` reads too on
Linux (Task 2), so they mustn't run beside the log's:
```csharp
/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
public sealed class SettingsLocationTests : IDisposable
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~CoffeePdf.Core.Tests.Helper"`
Expected: the build fails. `CoffeePdf.Core.Helper` doesn't exist yet.

- [ ] **Step 3: Write the folders, the channel file and the names**

`CoffeePdf.Core/Helper/HelperFolders.cs`:
```csharp
using System.Text;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// Where the Healdan Helper keeps its files (spec §4.3; the Desktop Clients page's "The Helper's Files"): its support
/// folder, the local copies directly in <c>Copies/</c>, the files for filing in <c>Temp/</c>, and <c>channel.json</c>.
/// </summary>
public sealed class HelperFolders(string support)
{
    /// <summary>Puts the Helper's files under one folder, as the Helper itself does when it's set (§4.3).</summary>
    public const string DevRootVariable = "HEALDAN_HELPER_DEV_ROOT";

    public string Support { get; } = Path.GetFullPath(support);

    public string Copies => Path.Combine(Support, "Copies");

    public string Temp => Path.Combine(Support, "Temp");

    public string ChannelFile => Path.Combine(Support, "channel.json");

    /// <summary>
    /// This user's: under <see cref="DevRootVariable"/> when it's set, else <c>~/Library/Application Support/Healdan
    /// Helper/</c> on macOS, and <c>$XDG_DATA_HOME/healdan-helper/</c> (or <c>~/.local/share/healdan-helper/</c>)
    /// elsewhere.
    /// </summary>
    public static HelperFolders ForThisUser()
    {
        if (Environment.GetEnvironmentVariable(DevRootVariable) is { Length: > 0 } root)
            return new HelperFolders(root);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return new HelperFolders(Path.Combine(home, "Library", "Application Support", "Healdan Helper"));
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".local", "share");
        return new HelperFolders(Path.Combine(data, "healdan-helper"));
    }

    /// <summary>
    /// Whether <paramref name="path"/> is directly in <see cref="Copies"/>, where every local copy is (§6.1). Links are
    /// followed (<c>/var</c> is <c>/private/var</c> on macOS) and names compared in Unicode's composed form, as the
    /// Helper's own check does, so a file the Helper calls a copy is one here too.
    /// </summary>
    public bool HoldsCopy(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        return folder is not null && Same(Real(folder), Real(Copies));
    }

    private static bool Same(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a).Normalize(NormalizationForm.FormC),
            Path.TrimEndingDirectorySeparator(b).Normalize(NormalizationForm.FormC), StringComparison.Ordinal);

    /// <summary><paramref name="path"/> with each link in it followed. A part that doesn't exist is kept as it is.</summary>
    private static string Real(string path, int depth = 0)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            // 32 links deep is a loop.
            if (depth < 32 && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                current = Real(target.FullName, depth + 1);
        }
        return current;
    }
}
```

`CoffeePdf.Core/Helper/ChannelInfo.cs`:
```csharp
using System.Text.Json;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// <c>channel.json</c>, which the Helper writes in its support folder while its channel is up (the Desktop Clients
/// page): the port on 127.0.0.1, the secret every request carries, and the Helper's process id.
/// </summary>
public sealed record ChannelInfo(int Port, string Secret, int Pid)
{
    /// <summary>
    /// The file's contents, or null when it's missing, damaged, or not the Helper's shape: a port, a secret of 48 hex
    /// digits, and a process id.
    /// </summary>
    public static ChannelInfo? Read(string file)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("port", out var port) || port.ValueKind != JsonValueKind.Number || !port.TryGetInt32(out var p) || p is < 1 or > 65535
                || !root.TryGetProperty("secret", out var secret) || secret.ValueKind != JsonValueKind.String
                || secret.GetString() is not { Length: 48 } s || !s.All(Uri.IsHexDigit)
                || !root.TryGetProperty("pid", out var pid) || pid.ValueKind != JsonValueKind.Number || !pid.TryGetInt32(out var id) || id < 1)
                return null;
            return new ChannelInfo(p, s, id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Leaves the secret out, so nothing that prints this prints it (§6.9).</summary>
    public override string ToString() => $"ChannelInfo {{ Port = {Port}, Pid = {Pid} }}";
}
```

`CoffeePdf.Core/Helper/CopyName.cs`:
```csharp
using System.Text.RegularExpressions;

namespace CoffeePdf.Core.Helper;

/// <summary>A copy's file name, split: its document's name, its doc ref, and whether it's a read-only copy.</summary>
public sealed record CopyNameParts(string Name, string DocRef, bool ReadOnly);

/// <summary>
/// The Helper's names for its local copies (the Desktop Clients page's "Local Copies"):
/// <c>Settlement Agreement #1234 v2.1.pdf</c>, and <c>… v2.1 (read-only).pdf</c> for a read-only copy. The Helper's
/// <c>title</c> for a copy is the same name without its extension.
/// </summary>
public static partial class CopyName
{
    private const string ReadOnlyTail = " (read-only)";

    /// <summary>The parts of a copy's file name, or null for a name that isn't a PDF copy's.</summary>
    public static CopyNameParts? Parse(string path)
    {
        var match = FileName().Match(Path.GetFileName(path));
        return match.Success ? new CopyNameParts(match.Groups["name"].Value, match.Groups["ref"].Value, match.Groups["ro"].Success) : null;
    }

    /// <summary>
    /// The document's name in the Helper's <paramref name="title"/>, without <paramref name="docRef"/> and the
    /// read-only mark, or null when the title doesn't end in that doc ref.
    /// </summary>
    public static string? NameIn(string title, string docRef)
    {
        var rest = title.EndsWith(ReadOnlyTail, StringComparison.Ordinal) ? title[..^ReadOnlyTail.Length] : title;
        var tail = " " + docRef;
        return docRef.Length > 0 && rest.Length > tail.Length && rest.EndsWith(tail, StringComparison.Ordinal) ? rest[..^tail.Length] : null;
    }

    // The name runs to the last " #<doc #> v<version>", so a name with its own "#7" in it keeps it.
    [GeneratedRegex(@"^(?<name>.+) (?<ref>#\d+ v\d+(?:\.\d+)*)(?<ro> \(read-only\))?\.pdf$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~CoffeePdf.Core.Tests.Helper"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Helper CoffeePdf.Core.Tests/Helper CoffeePdf.Core.Tests/Settings/SettingsLocationTests.cs
git commit -m "Find the Helper's folders, its channel.json and its copies' names"
```

---

### Task 2: The log

**Files:**
- Create: `CoffeePdf.Core/Logging/CoffeeLog.cs`
- Test: `CoffeePdf.Core.Tests/Logging/CoffeeLogTests.cs`, `CoffeePdf.Core.Tests/Logging/CoffeeLogLocationTests.cs`

**Interfaces:**
- Consumes: `SettingsLocation.Folder()` (Phase 1).
- Produces: `sealed class CoffeeLog(string folder)`: `const string OverrideVariable = "COFFEEPDF_LOG_DIR"`,
  `string FilePath`, `static string Folder()`, `void Info(string message)` and `void Warn(string message)`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Logging/CoffeeLogTests.cs`:
```csharp
using CoffeePdf.Core.Logging;

namespace CoffeePdf.Core.Tests.Logging;

/// <summary>Spec §6.9: the log, which never stops the app.</summary>
public sealed class CoffeeLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Each_event_is_a_line_with_its_level()
    {
        var log = new CoffeeLog(Path.Combine(_dir, "not-yet"));

        log.Info("/v1/state answered");
        log.Warn("/v1/file answered 500 (internal-error)");

        var lines = File.ReadAllLines(log.FilePath);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith(" INFO /v1/state answered", lines[0]);
        Assert.EndsWith(" WARN /v1/file answered 500 (internal-error)", lines[1]);
    }

    [Fact]
    public void A_log_that_cant_be_written_is_dropped()
    {
        var blocked = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocked, "a file where the folder would be");

        new CoffeeLog(blocked).Info("nothing to see");

        Assert.Equal("a file where the folder would be", File.ReadAllText(blocked));
    }
}
```

`CoffeePdf.Core.Tests/Logging/CoffeeLogLocationTests.cs`:
```csharp
using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Logging;

/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
public sealed class CoffeeLogLocationTests : IDisposable
{
    private readonly string? _override = Environment.GetEnvironmentVariable(CoffeeLog.OverrideVariable);

    public void Dispose() => Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, _override);

    [Fact]
    public void The_override_wins()
    {
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, "/tmp/coffeepdf-log");

        Assert.Equal("/tmp/coffeepdf-log", CoffeeLog.Folder());
    }

    [Fact]
    public void Otherwise_its_the_Macs_Logs_folder_or_beside_the_settings()
    {
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, null);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS() ? Path.Combine(home, "Library", "Logs", "CoffeePDF") : SettingsLocation.Folder();
        Assert.Equal(expected, CoffeeLog.Folder());
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~CoffeePdf.Core.Tests.Logging"`
Expected: the build fails. `CoffeePdf.Core.Logging` doesn't exist yet.

- [ ] **Step 3: Write the log**

`CoffeePdf.Core/Logging/CoffeeLog.cs`:
```csharp
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Logging;

/// <summary>
/// CoffeePDF's log (spec §6.9), a line per event, in <c>coffeepdf.log</c>. Nothing logs the channel's secret or a
/// file's contents, and an exception is logged by its type, since its message can quote either. A line that can't be
/// written is dropped: the log never stops a save or a close.
/// </summary>
public sealed class CoffeeLog(string folder)
{
    /// <summary>Overrides the folder, for tests and development, as <see cref="SettingsLocation.OverrideVariable"/> does the settings'.</summary>
    public const string OverrideVariable = "COFFEEPDF_LOG_DIR";

    private readonly Lock _gate = new();

    public string FilePath { get; } = Path.Combine(folder, "coffeepdf.log");

    /// <summary>
    /// <c>~/Library/Logs/CoffeePDF/</c> on macOS, unless <see cref="OverrideVariable"/> is set. Elsewhere it's the
    /// settings folder, as the Helper keeps its log in its support folder on Linux, until the Linux phase (§13).
    /// </summary>
    public static string Folder()
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } overridden)
            return overridden;
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "CoffeePDF");
        return SettingsLocation.Folder();
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {level} {message}{Environment.NewLine}";
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(FilePath, line);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The log is for diagnosis: a line that can't be written mustn't stop what it describes.
            }
        }
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~CoffeePdf.Core.Tests.Logging"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Logging CoffeePdf.Core.Tests/Logging
git commit -m "Keep a log in ~/Library/Logs/CoffeePDF"
```

---

### Task 3: `ChannelRoutes`, a copy's state, and the fake Helper

**Files:**
- Create: `CoffeePdf.Core/Helper/ChannelRoutes.cs`, `CoffeePdf.Core/Helper/ChannelAnswers.cs`
- Create: `CoffeePdf.Core.Tests/Fakes/FakeHelper.cs`
- Test: `CoffeePdf.Core.Tests/Helper/ChannelRoutesTests.cs`, `CoffeePdf.Core.Tests/Fakes/FakeHelperTests.cs`

**Interfaces:**
- Consumes: `HelperFolders` and `CopyName` (Task 1); `MinimalPdf.Build()` (Phase 1's fixture,
  `CoffeePdf.Tests.Fixtures`).
- Produces:
  - `enum ChannelWait { Quick, Saved, Filing, Window }`.
  - `sealed record ChannelRoute(string Name, ChannelWait Wait, string Body, string Answer)`, with `string Path` (`/v1/<name>`).
  - `static class ChannelRoutes`: `const string SecretHeader = "X-Healdan-Secret"`, `const string StateShape`, the
    routes `OpenDialog`, `SaveNew`, `File`, `Adopted`, `Saved`, `NewVersion`, `Properties`, `Closed` and `State`, and
    `IReadOnlyList<ChannelRoute> All`.
  - `sealed record CopyState(bool Known, long DocNumber, string Title, string DocRef, bool ReadOnly, string Path, string? HoldBy, bool Pending)`,
    with `static CopyState Unknown(string path)` and `static CopyState From(JsonObject answer, string path)`.
  - `enum OpenDialogAction { Opened, Local, Cancel }`, `enum SaveNewAction { Save, Local, Cancel }`,
    `enum PropertiesAction { Saved, Cancel, File }`.
  - `sealed record SaveNewAnswer(SaveNewAction Action, string? TempPath, string? Format, JsonObject Raw)`, with
    `static SaveNewAnswer From(JsonObject answer)`.
  - `internal static class JsonFields`: `Text`, `Flag` and `Number`.
  - Test support, `internal sealed class FakeHelper : IDisposable` in `CoffeePdf.Core.Tests.Fakes`:
    - `const string Secret`;
    - `HelperFolders Folders`, `int Port`;
    - `IReadOnlyList<(string Route, JsonObject Body)> Calls`, `IReadOnlyList<string> Routes`, and
      `IReadOnlyList<JsonObject> CallsTo(ChannelRoute route)`;
    - `void Answer(ChannelRoute, Func<JsonObject, Task<JsonNode>>)`, `void Answer(ChannelRoute, JsonNode)` and
      `void Fail(ChannelRoute, int status, string code)`;
    - `string AddCopy(string name, long docNumber, string version, bool readOnly = false, string? holdBy = null, byte[]? pdf = null)`,
      `void SetState(CopyState)` and `CopyState StateOf(string path)`;
    - `void GoAway()`, `void ComeBack()`, and `static JsonObject Json(CopyState)`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Helper/ChannelRoutesTests.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §9: the routes CoffeePDF calls, as the Desktop Clients page's table has them.</summary>
public sealed class ChannelRoutesTests
{
    [Fact]
    public void The_routes_are_the_Desktop_Clients_pages_but_link_and_ping() =>
        // /v1/link is for a second Helper, and CoffeePDF needs no /v1/ping: /v1/state tells it the Helper answers.
        Assert.Equal(
            new[] { "open-dialog", "save-new", "file", "adopted", "saved", "new-version", "properties", "closed", "state" },
            ChannelRoutes.All.Select(r => r.Name));

    [Fact]
    public void Each_route_is_under_v1() => Assert.All(ChannelRoutes.All, r => Assert.Equal("/v1/" + r.Name, r.Path));

    [Fact]
    public void Only_the_window_routes_wait_for_the_user() =>
        Assert.Equal(
            new[] { ChannelRoutes.OpenDialog, ChannelRoutes.SaveNew, ChannelRoutes.Properties },
            ChannelRoutes.All.Where(r => r.Wait == ChannelWait.Window));

    [Fact]
    public void A_state_says_known_or_not()
    {
        var path = "/support/Copies/Settlement Agreement #1234 v2.1.pdf";
        var known = System.Text.Json.Nodes.JsonNode.Parse($$"""
            {"known": true, "docNumber": 1234, "title": "Settlement Agreement #1234 v2.1", "docRef": "#1234 v2.1",
             "readOnly": false, "path": "{{path}}", "holdBy": null, "pending": true}
            """)!.AsObject();

        Assert.Equal(new CopyState(true, 1234, "Settlement Agreement #1234 v2.1", "#1234 v2.1", false, path, null, true), CopyState.From(known, path));
        Assert.Equal(CopyState.Unknown(path), CopyState.From(new() { ["known"] = false }, path));
        Assert.Equal(CopyState.Unknown(path), CopyState.From(new(), path));
    }

    [Fact]
    public void The_Save_windows_answer_keeps_its_values()
    {
        var answer = System.Text.Json.Nodes.JsonNode.Parse("""
            {"action": "save", "folderId": 42, "name": "Report", "format": "pdf", "properties": {"Client": "Acme"},
             "tempPath": "/support/Temp/save.pdf"}
            """)!.AsObject();

        var read = SaveNewAnswer.From(answer);

        Assert.Equal(SaveNewAction.Save, read.Action);
        Assert.Equal("/support/Temp/save.pdf", read.TempPath);
        Assert.Equal("pdf", read.Format);
        Assert.Same(answer, read.Raw);
        Assert.Equal(SaveNewAction.Cancel, SaveNewAnswer.From(new() { ["action"] = "something new" }).Action);
    }
}
```

`CoffeePdf.Core.Tests/Fakes/FakeHelperTests.cs`:
```csharp
using System.Net;
using System.Text;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Fakes;

/// <summary>Spec §9: the fake Helper serves exactly ChannelRoutes, and fails a test that calls anything else.</summary>
public sealed class FakeHelperTests
{
    private static async Task<HttpStatusCode> PostAsync(FakeHelper helper, string path, string? secret = FakeHelper.Secret)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{helper.Port}{path}")
        {
            Content = new StringContent("""{"path": "/nowhere.pdf"}""", Encoding.UTF8, "application/json"),
        };
        if (secret is not null)
            request.Headers.Add(ChannelRoutes.SecretHeader, secret);
        using var response = await http.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task It_serves_each_route_ChannelRoutes_lists()
    {
        using var helper = new FakeHelper();
        helper.Answer(ChannelRoutes.File, new System.Text.Json.Nodes.JsonObject { ["known"] = false }); // there's no file to file

        foreach (var route in ChannelRoutes.All)
            Assert.Equal(HttpStatusCode.OK, await PostAsync(helper, route.Path));
        Assert.Equal(ChannelRoutes.All.Select(r => r.Name), helper.Routes);
    }

    [Fact]
    public async Task Without_the_secret_it_refuses_as_the_Helper_does()
    {
        using var helper = new FakeHelper();

        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(helper, ChannelRoutes.State.Path, secret: null));
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(helper, ChannelRoutes.State.Path, secret: new string('0', 48)));
        Assert.Empty(helper.Calls);
    }

    [Fact]
    public async Task A_route_ChannelRoutes_doesnt_list_fails_the_test()
    {
        var helper = new FakeHelper();

        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(helper, "/v1/link"));

        Assert.Contains("/v1/link", Assert.Throws<InvalidOperationException>(helper.Dispose).Message);
    }

    [Fact]
    public void A_copy_is_named_as_the_Helper_names_it_and_answers_its_state()
    {
        using var helper = new FakeHelper();

        var path = helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");

        Assert.Equal(Path.Combine(helper.Folders.Copies, "Settlement Agreement #1234 v2.1 (read-only).pdf"), path);
        Assert.True(File.Exists(path));
        Assert.Equal(new CopyState(true, 1234, "Settlement Agreement #1234 v2.1 (read-only)", "#1234 v2.1", true, path, "Zoë Q. Reviewer", false),
            helper.StateOf(path));
    }

    [Fact]
    public void Going_away_takes_channel_json_and_coming_back_writes_it()
    {
        using var helper = new FakeHelper();

        helper.GoAway();
        Assert.Null(ChannelInfo.Read(helper.Folders.ChannelFile));
        helper.ComeBack();

        Assert.Equal(new ChannelInfo(helper.Port, FakeHelper.Secret, Environment.ProcessId), ChannelInfo.Read(helper.Folders.ChannelFile));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~ChannelRoutesTests|FullyQualifiedName~FakeHelperTests"`
Expected: the build fails. `ChannelRoutes` and `FakeHelper` don't exist yet.

- [ ] **Step 3: Write the routes and the answers**

`CoffeePdf.Core/Helper/ChannelRoutes.cs`:
```csharp
namespace CoffeePdf.Core.Helper;

/// <summary>How long CoffeePDF waits for a route's answer (spec §4.3; the limits are <see cref="HelperChannel.DefaultWaits"/>).</summary>
public enum ChannelWait
{
    /// <summary>Answered at once.</summary>
    Quick,

    /// <summary><c>/v1/saved</c>, which the Helper answers within 120 seconds.</summary>
    Saved,

    /// <summary><c>/v1/file</c>, which waits for Healdan's filing.</summary>
    Filing,

    /// <summary>The window routes, which answer when the user closes the Helper's window: never timed out (§6.3).</summary>
    Window,
}

/// <summary>One of the Helper's channel routes that CoffeePDF calls: a <c>POST</c>, with its body and answer as the Desktop Clients page has them.</summary>
public sealed record ChannelRoute(string Name, ChannelWait Wait, string Body, string Answer)
{
    public string Path => "/v1/" + Name;
}

/// <summary>
/// Every route CoffeePDF calls on the Helper's channel (spec §9), with its body and answer, following the Desktop
/// Clients page's table (<c>site/content/developer/desktop-clients.md</c> in Healdan-Project/healdan). The channel has
/// no machine-readable contract, so this is CoffeePDF's. The client calls only these, the fake Helper serves only
/// these, and a change to the channel in Healdan changes this list in the same release. <c>/v1/link</c> is for a second
/// Helper, and CoffeePDF never calls it.
/// </summary>
public static class ChannelRoutes
{
    /// <summary>The header every route's request carries the secret in.</summary>
    public const string SecretHeader = "X-Healdan-Secret";

    /// <summary>A copy's state, as the routes that answer one give it.</summary>
    public const string StateShape = """{"known": true, "docNumber", "title", "docRef", "readOnly", "path", "holdBy", "pending"}, or {"known": false}""";

    public static readonly ChannelRoute OpenDialog = new("open-dialog", ChannelWait.Window,
        """{"app"}""", """{"action": "opened" | "local" | "cancel"}, once the window closes""");

    public static readonly ChannelRoute SaveNew = new("save-new", ChannelWait.Window,
        """{"app", "name", "formats", "path"}; path is the copy, for Save as New Document""",
        """{"action": "save" | "local" | "cancel", "folderId", "name", "format", "properties", "tempPath"}""");

    public static readonly ChannelRoute File = new("file", ChannelWait.Filing,
        """{"tempPath", "folderId", "name", "format", "properties", "app", "replaces"}""", "the new copy's state");

    public static readonly ChannelRoute Adopted = new("adopted", ChannelWait.Quick,
        """{"path"}""", "the copy's state; the file as it is now is the new copy's starting point");

    public static readonly ChannelRoute Saved = new("saved", ChannelWait.Saved,
        """{"path"}""", "the copy's state once Healdan has every save written so far, at once when nothing is on its way, else after 120 s as it is");

    public static readonly ChannelRoute NewVersion = new("new-version", ChannelWait.Quick,
        """{"path"}""", "{}; the copy's next save goes up as a version");

    public static readonly ChannelRoute Properties = new("properties", ChannelWait.Window,
        """{"path"}""", """{"action": "saved" | "cancel" | "file"}; file means it isn't in Healdan, so the caller runs Save to Healdan""");

    public static readonly ChannelRoute Closed = new("closed", ChannelWait.Quick, """{"path"}""", "{}");

    public static readonly ChannelRoute State = new("state", ChannelWait.Quick, """{"path"}""", "the copy's state");

    /// <summary>Every route, in the Desktop Clients page's order.</summary>
    public static IReadOnlyList<ChannelRoute> All { get; } = [OpenDialog, SaveNew, File, Adopted, Saved, NewVersion, Properties, Closed, State];
}
```

`CoffeePdf.Core/Helper/ChannelAnswers.cs`:
```csharp
using System.Text.Json.Nodes;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// A copy's state, as the channel's routes answer it (<see cref="ChannelRoutes.StateShape"/>).
/// </summary>
/// <param name="Known">False for a path the Helper has no copy at; the rest is then empty.</param>
/// <param name="Title">The copy's file name without its extension: <c>Settlement Agreement #1234 v2.1</c>, with <c> (read-only)</c> for a read-only copy.</param>
/// <param name="DocRef">The version Healdan has: <c>#1234 v2.1</c>.</param>
/// <param name="ReadOnly">Opened without a check-out, or (spec §6.9) no longer checked out here.</param>
/// <param name="HoldBy">For a read-only copy, the name of whoever has the document checked out, if anyone.</param>
/// <param name="Pending">Changes are still on their way to Healdan.</param>
public sealed record CopyState(bool Known, long DocNumber, string Title, string DocRef, bool ReadOnly, string Path, string? HoldBy, bool Pending)
{
    public static CopyState Unknown(string path) => new(false, 0, "", "", false, path, null, false);

    /// <summary>Reads an answer. One that doesn't say <c>"known": true</c> is <see cref="Unknown"/> for <paramref name="path"/>.</summary>
    public static CopyState From(JsonObject answer, string path) =>
        !JsonFields.Flag(answer, "known")
            ? Unknown(path)
            : new CopyState(true, JsonFields.Number(answer, "docNumber"), JsonFields.Text(answer, "title") ?? "",
                JsonFields.Text(answer, "docRef") ?? "", JsonFields.Flag(answer, "readOnly"), JsonFields.Text(answer, "path") ?? path,
                JsonFields.Text(answer, "holdBy"), JsonFields.Flag(answer, "pending"));
}

/// <summary><c>/v1/open-dialog</c>'s answer (§6.3). Anything else the Helper might answer counts as <see cref="Cancel"/>.</summary>
public enum OpenDialogAction
{
    /// <summary>The Helper opened what was chosen, in the app for its kind.</summary>
    Opened,

    /// <summary>The user chose the Mac's own Open box.</summary>
    Local,

    Cancel,
}

/// <summary><c>/v1/save-new</c>'s action (§6.6). Anything else counts as <see cref="Cancel"/>.</summary>
public enum SaveNewAction
{
    /// <summary>Write the PDF to the answer's <c>tempPath</c>, then file it.</summary>
    Save,

    /// <summary>The user chose the Mac's own Save box.</summary>
    Local,

    Cancel,
}

/// <summary><c>/v1/properties</c>'s answer (§6.7). Anything else counts as <see cref="Cancel"/>.</summary>
public enum PropertiesAction
{
    Saved,
    Cancel,

    /// <summary>The PDF isn't in Healdan, so Save to Healdan runs instead.</summary>
    File,
}

/// <summary>
/// <c>/v1/save-new</c>'s answer (§6.6): its action, and on <see cref="SaveNewAction.Save"/> the values
/// <c>/v1/file</c> takes back unchanged, which <see cref="Raw"/> keeps as they came.
/// </summary>
public sealed record SaveNewAnswer(SaveNewAction Action, string? TempPath, string? Format, JsonObject Raw)
{
    public static SaveNewAnswer From(JsonObject answer) =>
        new(JsonFields.Text(answer, "action") switch
        {
            "save" => SaveNewAction.Save,
            "local" => SaveNewAction.Local,
            _ => SaveNewAction.Cancel,
        }, JsonFields.Text(answer, "tempPath"), JsonFields.Text(answer, "format"), answer);
}

/// <summary>Reads an answer's fields, taking a field of the wrong type as missing.</summary>
internal static class JsonFields
{
    public static string? Text(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    public static bool Flag(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;

    public static long Number(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out long number) ? number : 0;
}
```

- [ ] **Step 4: Write the fake Helper**

`CoffeePdf.Core.Tests/Fakes/FakeHelper.cs`:
```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Fakes;

/// <summary>
/// A Healdan Helper for the tests (spec §9). Its channel is on 127.0.0.1, with <c>channel.json</c>, <c>Copies/</c> and
/// <c>Temp/</c> in a support folder of its own, and it serves exactly <see cref="ChannelRoutes"/>, as the Helper's
/// server does: a <c>POST</c>, the secret, a JSON body, and <c>{"error": code}</c> for a refusal.
/// </summary>
/// <remarks>
/// Each route answers from a handler the test can replace. By default the state routes answer the copies added,
/// <c>file</c> files the temporary file as a new copy, the window routes answer <c>cancel</c>, and the rest answer
/// <c>{}</c>. A call to any other route is answered 404 and fails the test at <see cref="Dispose"/>. The app's tests
/// compile this file too.
/// </remarks>
internal sealed class FakeHelper : IDisposable
{
    /// <summary>The channel's secret: 48 hex digits, as the Helper's.</summary>
    public const string Secret = "5ec7e75ec7e75ec7e75ec7e75ec7e75ec7e75ec7e75ec7e7";

    private readonly HttpListener _listener;
    private readonly ConcurrentQueue<(string Route, JsonObject Body)> _calls = new();
    private readonly ConcurrentQueue<string> _unlisted = new();
    private readonly ConcurrentDictionary<string, Func<JsonObject, Task<JsonNode>>> _answers = new();
    private readonly ConcurrentDictionary<string, CopyState> _copies = new();
    private long _lastDocNumber;

    public FakeHelper()
    {
        Folders = new HelperFolders(Directory.CreateTempSubdirectory("coffeepdf-helper-").FullName);
        Directory.CreateDirectory(Folders.Copies);
        Directory.CreateDirectory(Folders.Temp);
        (_listener, Port) = Listen();
        ComeBack();
        _ = Task.Run(ServeAsync);
        Answer(ChannelRoutes.State, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.Saved, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.Adopted, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.File, FileTemporary);
        Answer(ChannelRoutes.OpenDialog, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.NewVersion, new JsonObject());
        Answer(ChannelRoutes.Closed, new JsonObject());
    }

    public HelperFolders Folders { get; }

    public int Port { get; }

    /// <summary>Every call answered so far, in order: the route's name and its body.</summary>
    public IReadOnlyList<(string Route, JsonObject Body)> Calls => [.. _calls];

    /// <summary>The routes called so far, in order.</summary>
    public IReadOnlyList<string> Routes => [.. _calls.Select(c => c.Route)];

    /// <summary>The bodies of the calls to <paramref name="route"/>, in order.</summary>
    public IReadOnlyList<JsonObject> CallsTo(ChannelRoute route) => [.. _calls.Where(c => c.Route == route.Name).Select(c => c.Body)];

    /// <summary>Answers <paramref name="route"/> with what <paramref name="answer"/> gives for each call's body.</summary>
    public void Answer(ChannelRoute route, Func<JsonObject, Task<JsonNode>> answer) => _answers[route.Name] = answer;

    /// <summary>Answers <paramref name="route"/> with a copy of <paramref name="answer"/> each time.</summary>
    public void Answer(ChannelRoute route, JsonNode answer) => Answer(route, _ => Task.FromResult(answer.DeepClone()));

    /// <summary>Answers <paramref name="route"/> with an error, as the Helper's server does: <c>{"error": code}</c>.</summary>
    public void Fail(ChannelRoute route, int status, string code) => Answer(route, _ => throw new Refusal(status, code));

    /// <summary>Puts a copy in <c>Copies/</c>, named as the Helper names them, and answers its state.</summary>
    public string AddCopy(string name, long docNumber, string version, bool readOnly = false, string? holdBy = null, byte[]? pdf = null)
    {
        var file = $"{name} #{docNumber} v{version}{(readOnly ? " (read-only)" : "")}.pdf";
        var path = Path.Combine(Folders.Copies, file);
        File.WriteAllBytes(path, pdf ?? MinimalPdf.Build());
        SetState(new CopyState(true, docNumber, Path.GetFileNameWithoutExtension(file), $"#{docNumber} v{version}", readOnly, path, holdBy, Pending: false));
        return path;
    }

    /// <summary>What the state routes answer for <paramref name="state"/>'s path from now on.</summary>
    public void SetState(CopyState state) => _copies[Key(state.Path)] = state;

    public CopyState StateOf(string path) => _copies.TryGetValue(Key(path), out var state) ? state : CopyState.Unknown(path);

    /// <summary>Stops answering, as a Helper that quit: <c>channel.json</c> goes.</summary>
    public void GoAway() => File.Delete(Folders.ChannelFile);

    /// <summary>Answers again: <c>channel.json</c> names this channel, and this process as the Helper's.</summary>
    public void ComeBack() =>
        File.WriteAllText(Folders.ChannelFile, JsonSerializer.Serialize(new { port = Port, secret = Secret, pid = Environment.ProcessId }));

    /// <summary>A copy's state as the Helper writes it (its ChannelHandler.Json).</summary>
    public static JsonObject Json(CopyState state) => !state.Known
        ? new JsonObject { ["known"] = false }
        : new JsonObject
        {
            ["known"] = true,
            ["docNumber"] = state.DocNumber,
            ["title"] = state.Title,
            ["docRef"] = state.DocRef,
            ["readOnly"] = state.ReadOnly,
            ["path"] = state.Path,
            ["holdBy"] = state.HoldBy,
            ["pending"] = state.Pending,
        };

    public void Dispose()
    {
        _listener.Close();
        try
        {
            Directory.Delete(Folders.Support, recursive: true);
        }
        catch (IOException)
        {
            // A window still closing may hold a file; the folder is a temporary one all the same.
        }
        if (!_unlisted.IsEmpty)
            throw new InvalidOperationException($"CoffeePDF called routes ChannelRoutes doesn’t list: {string.Join(", ", _unlisted)}");
    }

    /// <summary><c>/v1/file</c> as the Helper does it: the temporary file becomes a new document's copy, checked out here, and goes.</summary>
    private Task<JsonNode> FileTemporary(JsonObject body)
    {
        var temp = (string)body["tempPath"]!;
        var path = AddCopy((string)body["name"]!, Interlocked.Increment(ref _lastDocNumber), "1", pdf: File.ReadAllBytes(temp));
        File.Delete(temp);
        return Task.FromResult<JsonNode>(Json(StateOf(path)));
    }

    private static string PathIn(JsonObject body) => (string?)body["path"] ?? "";

    private static string Key(string path) => Path.GetFullPath(path).Normalize(NormalizationForm.FormC);

    private static (HttpListener Listener, int Port) Listen()
    {
        // HttpListener can't bind port 0, so learn a free port and try again if something takes it first, as the Helper does.
        for (var attempt = 1; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            _ = Task.Run(() => RespondAsync(context));
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var (status, answer) = await RouteAsync(context.Request);
        var bytes = Encoding.UTF8.GetBytes(answer.ToJsonString());
        try
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or IOException)
        {
            // The caller gave up waiting, or the fake is stopping.
        }
    }

    private async Task<(int Status, JsonNode Answer)> RouteAsync(HttpListenerRequest request)
    {
        var path = request.Url?.AbsolutePath ?? "";
        if (ChannelRoutes.All.FirstOrDefault(r => r.Path == path) is not { } route)
        {
            _unlisted.Enqueue(path);
            return (404, Error("not-found"));
        }
        if (request.Headers[ChannelRoutes.SecretHeader] != Secret)
            return (403, Error("forbidden"));
        if (request.HttpMethod != "POST")
            return (405, Error("method-not-allowed"));
        JsonObject body;
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            if (JsonNode.Parse(await reader.ReadToEndAsync()) is not JsonObject parsed)
                return (400, Error("bad-request"));
            body = parsed;
        }
        catch (JsonException)
        {
            return (400, Error("bad-request"));
        }
        _calls.Enqueue((route.Name, body));
        try
        {
            return (200, await _answers[route.Name](body));
        }
        catch (Refusal refusal)
        {
            return (refusal.Status, Error(refusal.Code));
        }
        catch (Exception)
        {
            return (500, Error("internal-error")); // as the Helper answers a route that throws
        }
    }

    private static JsonObject Error(string code) => new() { ["error"] = code };

    private sealed class Refusal(int status, string code) : Exception(code)
    {
        public int Status { get; } = status;

        public string Code { get; } = code;
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~ChannelRoutesTests|FullyQualifiedName~FakeHelperTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf.Core/Helper CoffeePdf.Core.Tests/Helper CoffeePdf.Core.Tests/Fakes
git commit -m "List the channel's routes in ChannelRoutes, and add a fake Helper that serves exactly those"
```

---

### Task 4: The channel client, and the link a window holds

**Files:**
- Create: `CoffeePdf.Core/Helper/HelperExceptions.cs`, `CoffeePdf.Core/Helper/HelperChannel.cs`, `CoffeePdf.Core/Helper/HealdanLink.cs`
- Modify: `CoffeePdf.Core.Tests/Fakes/FakeHelper.cs` (add `Link`)
- Test: `CoffeePdf.Core.Tests/Helper/HelperChannelTests.cs`

**Interfaces:**
- Consumes: `HelperFolders`, `ChannelInfo` (Task 1); `CoffeeLog` (Task 2); `ChannelRoutes`, `CopyState`,
  `SaveNewAnswer` and the action enums (Task 3); `SettingsStore` (Phase 1).
- Produces:
  - `sealed class HelperUnreachableException(string message) : Exception`.
  - `sealed class HelperErrorException(int status, string code) : Exception`, with `int Status`, `string Code`, and
    the message `Healdan Helper answered <status> (<code>)`.
  - `sealed class HelperChannel(HelperFolders folders, CoffeeLog log, IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null)`:
    - `const string App = "CoffeePDF"`;
    - `static IReadOnlyDictionary<ChannelWait, TimeSpan> DefaultWaits`;
    - `Task<CopyState> StateAsync(string path, CancellationToken cancel = default)`;
    - `Task<CopyState> SavedAsync(string path, CancellationToken cancel = default)`;
    - `Task<CopyState> AdoptedAsync(string path)`, `Task NewVersionAsync(string path)`, `Task ClosedAsync(string path)`;
    - `Task<OpenDialogAction> OpenDialogAsync()`;
    - `Task<SaveNewAnswer> SaveNewAsync(string name, string? copy)`;
    - `Task<CopyState> FileAsync(SaveNewAnswer answer, string? replaces)`;
    - `Task<PropertiesAction> PropertiesAsync(string path)`.

    Each throws `HelperUnreachableException` or `HelperErrorException`, and an `OperationCanceledException` when its
    `cancel` is cancelled.
  - `sealed class HealdanLink(SettingsStore settings, HelperFolders folders, CoffeeLog log, HelperChannel? channel = null)`:
    `Settings`, `Folders`, `Log`, `Channel`, and `bool Enabled`.
  - `FakeHelper.Link(bool enabled = true, IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null) : HealdanLink`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Helper/HelperChannelTests.cs`:
```csharp
using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Tests.Fakes;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §4.3 and §9: the channel client, against the fake Helper.</summary>
public sealed class HelperChannelTests : IDisposable
{
    private readonly FakeHelper _helper = new();

    public void Dispose() => _helper.Dispose();

    private HelperChannel Channel(IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null) => _helper.Link(waits: waits).Channel;

    [Fact]
    public async Task State_sends_the_path_and_reads_the_copys_state()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var state = await Channel().StateAsync(copy);

        Assert.Equal(_helper.StateOf(copy), state);
        Assert.Equal(copy, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.State))["path"]);
    }

    [Fact]
    public async Task A_path_the_Helper_doesnt_know_is_unknown() =>
        Assert.False((await Channel().StateAsync("/docs/Report.pdf")).Known);

    [Fact]
    public async Task The_window_routes_name_CoffeePDF_and_ask_for_a_pdf()
    {
        var channel = Channel();

        await channel.OpenDialogAsync();
        await channel.SaveNewAsync("Report", copy: null);
        await channel.SaveNewAsync("Settlement Agreement", copy: "/support/Copies/Settlement Agreement #1234 v2.1.pdf");

        Assert.Equal("""{"app":"CoffeePDF"}""", Assert.Single(_helper.CallsTo(ChannelRoutes.OpenDialog)).ToJsonString());
        Assert.Equal(
            new[]
            {
                """{"app":"CoffeePDF","name":"Report","formats":["pdf"],"path":null}""",
                """{"app":"CoffeePDF","name":"Settlement Agreement","formats":["pdf"],"path":"/support/Copies/Settlement Agreement #1234 v2.1.pdf"}""",
            },
            _helper.CallsTo(ChannelRoutes.SaveNew).Select(b => b.ToJsonString()));
    }

    [Theory]
    [InlineData("opened", OpenDialogAction.Opened)]
    [InlineData("local", OpenDialogAction.Local)]
    [InlineData("cancel", OpenDialogAction.Cancel)]
    [InlineData("something new", OpenDialogAction.Cancel)]
    public async Task The_Open_windows_answer_is_read(string answer, OpenDialogAction expected)
    {
        _helper.Answer(ChannelRoutes.OpenDialog, new JsonObject { ["action"] = answer });

        Assert.Equal(expected, await Channel().OpenDialogAsync());
    }

    [Theory]
    [InlineData("saved", PropertiesAction.Saved)]
    [InlineData("file", PropertiesAction.File)]
    [InlineData("cancel", PropertiesAction.Cancel)]
    public async Task The_Properties_windows_answer_is_read(string answer, PropertiesAction expected)
    {
        _helper.Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = answer });

        Assert.Equal(expected, await Channel().PropertiesAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task Filing_sends_the_Save_windows_values_back_unchanged()
    {
        var temp = Path.Combine(_helper.Folders.Temp, "save.pdf");
        File.WriteAllBytes(temp, CoffeePdf.Tests.Fixtures.MinimalPdf.Build());
        var answer = SaveNewAnswer.From(JsonNode.Parse($$"""
            {"action": "save", "folderId": 42, "name": "Report", "format": "pdf", "properties": {"Client": "Acme", "Year": 2026},
             "tempPath": "{{temp}}"}
            """)!.AsObject());

        var filed = await Channel().FileAsync(answer, replaces: "/support/Copies/Old #7 v1.pdf");

        var body = Assert.Single(_helper.CallsTo(ChannelRoutes.File));
        Assert.Equal(temp, (string?)body["tempPath"]);
        Assert.Equal(42, (int)body["folderId"]!);
        Assert.Equal("Report", (string?)body["name"]);
        Assert.Equal("pdf", (string?)body["format"]);
        Assert.Equal("""{"Client":"Acme","Year":2026}""", body["properties"]!.ToJsonString());
        Assert.Equal("CoffeePDF", (string?)body["app"]);
        Assert.Equal("/support/Copies/Old #7 v1.pdf", (string?)body["replaces"]);
        Assert.Equal(Path.Combine(_helper.Folders.Copies, "Report #1 v1.pdf"), filed.Path);
    }

    [Fact]
    public async Task Without_channel_json_the_Helper_cant_be_reached()
    {
        _helper.GoAway();

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task A_channel_json_whose_process_is_gone_cant_be_reached()
    {
        // The largest pid there is: no process has it.
        File.WriteAllText(_helper.Folders.ChannelFile, $$"""{"port": {{_helper.Port}}, "secret": "{{FakeHelper.Secret}}", "pid": 2147483647}""");

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
        Assert.Empty(_helper.Calls);
    }

    [Fact]
    public async Task A_channel_nothing_listens_on_cant_be_reached()
    {
        var other = new FakeHelper();
        var port = other.Port;
        other.Dispose(); // its port is free again, and nothing listens on it
        File.WriteAllText(_helper.Folders.ChannelFile, $$"""{"port": {{port}}, "secret": "{{FakeHelper.Secret}}", "pid": {{Environment.ProcessId}}}""");

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task An_error_answer_carries_its_status_and_code()
    {
        _helper.Fail(ChannelRoutes.State, 500, "internal-error");

        var error = await Assert.ThrowsAsync<HelperErrorException>(() => Channel().StateAsync("/docs/Report.pdf"));

        Assert.Equal(500, error.Status);
        Assert.Equal("internal-error", error.Code);
        Assert.Equal("Healdan Helper answered 500 (internal-error)", error.Message);
    }

    [Fact]
    public async Task A_quick_route_gives_up_at_its_limit_but_a_window_route_waits_for_the_user()
    {
        var user = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.State, async _ => { await user.Task; return new JsonObject { ["known"] = false }; });
        _helper.Answer(ChannelRoutes.OpenDialog, async _ => { await user.Task; return new JsonObject { ["action"] = "local" }; });
        // A short limit for the quick routes, so the test sees one run out; the window routes keep none.
        var channel = Channel(new Dictionary<ChannelWait, TimeSpan>(HelperChannel.DefaultWaits) { [ChannelWait.Quick] = TimeSpan.FromMilliseconds(200) });

        var open = channel.OpenDialogAsync();
        await Assert.ThrowsAsync<HelperUnreachableException>(() => channel.StateAsync("/docs/Report.pdf"));
        var waitedPastIt = !open.IsCompleted;
        user.SetResult();

        Assert.True(waitedPastIt);
        Assert.Equal(OpenDialogAction.Local, await open.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(Timeout.InfiniteTimeSpan, HelperChannel.DefaultWaits[ChannelWait.Window]);
    }

    [Fact]
    public async Task Cancelling_a_call_cancels_it_rather_than_calling_the_Helper_away()
    {
        _helper.Answer(ChannelRoutes.Saved, async _ => { await new TaskCompletionSource().Task; return new JsonObject(); }); // never answers
        using var stop = new CancellationTokenSource();

        var saved = Channel().SavedAsync("/docs/Report.pdf", stop.Token);
        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saved);
    }

    [Fact]
    public async Task Nothing_logged_holds_the_secret()
    {
        var link = _helper.Link();
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        await link.Channel.StateAsync(copy);
        _helper.Fail(ChannelRoutes.Closed, 500, "internal-error");
        await Assert.ThrowsAsync<HelperErrorException>(() => link.Channel.ClosedAsync(copy));
        _helper.GoAway();
        var unreachable = await Assert.ThrowsAsync<HelperUnreachableException>(() => link.Channel.StateAsync(copy));

        var log = File.ReadAllText(link.Log.FilePath);
        Assert.Contains("/v1/state", log);
        Assert.DoesNotContain(FakeHelper.Secret, log);
        Assert.DoesNotContain(FakeHelper.Secret, unreachable.Message);
    }

    [Fact]
    public void The_link_reads_Healdan_support_from_its_settings()
    {
        var link = _helper.Link(enabled: false);
        Assert.False(link.Enabled);

        link.Settings.HealdanEnabled = true;

        Assert.True(link.Enabled);
        Assert.Same(_helper.Folders, link.Folders);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~HelperChannelTests`
Expected: the build fails. `HelperChannel` and `FakeHelper.Link` don't exist yet.

- [ ] **Step 3: Write the client and the link**

`CoffeePdf.Core/Helper/HelperExceptions.cs`:
```csharp
namespace CoffeePdf.Core.Helper;

/// <summary>
/// The Helper can't be reached: no <c>channel.json</c>, its process gone, no connection, or no answer in time. Open and
/// Save then use the Mac's own boxes, and a copy's saves stay on the Mac until it's back (spec §6.9).
/// </summary>
public sealed class HelperUnreachableException(string message) : Exception(message);

/// <summary>
/// The Helper answered with an error, <c>{"error": code}</c> and a status other than 200, or with something that isn't
/// a JSON object. Its message is CoffeePDF's own, with the status and code: the channel's errors carry no message.
/// </summary>
public sealed class HelperErrorException(int status, string code) : Exception($"Healdan Helper answered {status} ({code})")
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}
```

`CoffeePdf.Core/Helper/HelperChannel.cs`:
```csharp
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Logging;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// The Healdan Helper's local channel (spec §4.3, §9): HTTP on 127.0.0.1, at the port in <c>channel.json</c>, with its
/// secret in every request. The file is read at each call, so a Helper that restarted is found at its new port. It
/// calls only <see cref="ChannelRoutes"/>, and never holds a Healdan token or calls Healdan itself (§4.1).
/// </summary>
/// <remarks>
/// Nothing it logs holds the secret or a file's contents: a call is logged by its route and outcome, and a failure to
/// reach the Helper by the exception's type, since its message could quote the request. Its awaits keep the caller's
/// context, so a window's code carries on on the UI thread.
/// </remarks>
public sealed class HelperChannel(HelperFolders folders, CoffeeLog log, IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null)
{
    /// <summary>The app CoffeePDF names itself as to the Helper's windows and in its check-outs (§6.2, §6.3).</summary>
    public const string App = "CoffeePDF";

    /// <summary>
    /// How long each kind of route is waited for: the LibreOffice extension's limits (its <c>healdan_logic.py</c>),
    /// except the window routes, which are waited for however long the user takes (§4.3).
    /// </summary>
    public static readonly IReadOnlyDictionary<ChannelWait, TimeSpan> DefaultWaits = new Dictionary<ChannelWait, TimeSpan>
    {
        [ChannelWait.Quick] = TimeSpan.FromSeconds(10),
        [ChannelWait.Saved] = TimeSpan.FromSeconds(135),
        [ChannelWait.Filing] = TimeSpan.FromSeconds(660),
        [ChannelWait.Window] = Timeout.InfiniteTimeSpan,
    };

    // One client for the app's life, with no proxy, since the channel is on this computer. Its own time limit is off:
    // each call sets its route's.
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };

    private readonly IReadOnlyDictionary<ChannelWait, TimeSpan> _waits = waits ?? DefaultWaits;

    /// <summary><c>/v1/state</c>: the copy's state, which sets a window's mode (§6.2).</summary>
    public async Task<CopyState> StateAsync(string path, CancellationToken cancel = default) =>
        CopyState.From(await CallAsync(ChannelRoutes.State, WithPath(path), cancel), path);

    /// <summary><c>/v1/saved</c>: the copy's state once Healdan has every save so far, or after 120 seconds as it is (§6.4).</summary>
    public async Task<CopyState> SavedAsync(string path, CancellationToken cancel = default) =>
        CopyState.From(await CallAsync(ChannelRoutes.Saved, WithPath(path), cancel), path);

    /// <summary><c>/v1/adopted</c>: the window is now the new copy's, and the file as it is now is its starting point (§6.6).</summary>
    public async Task<CopyState> AdoptedAsync(string path) =>
        CopyState.From(await CallAsync(ChannelRoutes.Adopted, WithPath(path), default), path);

    /// <summary><c>/v1/new-version</c>: the copy's next save goes up as a version (§6.5).</summary>
    public Task NewVersionAsync(string path) => CallAsync(ChannelRoutes.NewVersion, WithPath(path), default);

    /// <summary><c>/v1/closed</c>: the copy's window has closed, so the Helper checks it in (§6.8).</summary>
    public Task ClosedAsync(string path) => CallAsync(ChannelRoutes.Closed, WithPath(path), default);

    /// <summary><c>/v1/open-dialog</c>: the Helper's Open window (§6.3).</summary>
    public async Task<OpenDialogAction> OpenDialogAsync() =>
        JsonFields.Text(await CallAsync(ChannelRoutes.OpenDialog, new JsonObject { ["app"] = App }, default), "action") switch
        {
            "opened" => OpenDialogAction.Opened,
            "local" => OpenDialogAction.Local,
            _ => OpenDialogAction.Cancel,
        };

    /// <summary>
    /// <c>/v1/save-new</c>: the Helper's Save window (§6.6), for Save as New Document when <paramref name="copy"/> is
    /// the window's copy, or Save to Healdan when it's null.
    /// </summary>
    public async Task<SaveNewAnswer> SaveNewAsync(string name, string? copy) =>
        SaveNewAnswer.From(await CallAsync(ChannelRoutes.SaveNew, new JsonObject
        {
            ["app"] = App,
            ["name"] = name,
            ["formats"] = new JsonArray("pdf"),
            ["path"] = copy,
        }, default));

    /// <summary>
    /// <c>/v1/file</c>: files the PDF written to the Save window's <c>tempPath</c>, with the answer's values unchanged,
    /// and answers the new copy's state (§6.6). <paramref name="replaces"/> is the copy Save as New Document started from.
    /// </summary>
    public async Task<CopyState> FileAsync(SaveNewAnswer answer, string? replaces)
    {
        var body = new JsonObject
        {
            ["tempPath"] = answer.Raw["tempPath"]?.DeepClone(),
            ["folderId"] = answer.Raw["folderId"]?.DeepClone(),
            ["name"] = answer.Raw["name"]?.DeepClone(),
            ["format"] = answer.Raw["format"]?.DeepClone(),
            ["properties"] = answer.Raw["properties"]?.DeepClone() ?? new JsonObject(),
            ["app"] = App,
            ["replaces"] = replaces,
        };
        return CopyState.From(await CallAsync(ChannelRoutes.File, body, default), answer.TempPath ?? "");
    }

    /// <summary><c>/v1/properties</c>: the Helper's Properties window (§6.7).</summary>
    public async Task<PropertiesAction> PropertiesAsync(string path) =>
        JsonFields.Text(await CallAsync(ChannelRoutes.Properties, WithPath(path), default), "action") switch
        {
            "saved" => PropertiesAction.Saved,
            "file" => PropertiesAction.File,
            _ => PropertiesAction.Cancel,
        };

    private static JsonObject WithPath(string path) => new() { ["path"] = path };

    private async Task<JsonObject> CallAsync(ChannelRoute route, JsonObject body, CancellationToken cancel)
    {
        var info = ChannelInfo.Read(folders.ChannelFile) ?? throw Unreachable(route, "no channel.json");
        if (!IsRunning(info.Pid))
            throw Unreachable(route, "its process is gone");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        if (_waits[route.Wait] != Timeout.InfiniteTimeSpan)
            limit.CancelAfter(_waits[route.Wait]);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{info.Port}{route.Path}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add(ChannelRoutes.SecretHeader, info.Secret);
        int status;
        byte[] raw;
        try
        {
            using var response = await Http.SendAsync(request, limit.Token);
            status = (int)response.StatusCode;
            raw = await response.Content.ReadAsByteArrayAsync(limit.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            throw; // the caller stopped waiting, as when a window closes while it follows a save
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            throw Unreachable(route, e.GetType().Name);
        }
        var answer = Parse(raw);
        if (status != 200)
        {
            var code = answer is not null ? JsonFields.Text(answer, "error") ?? "no code" : "no code";
            log.Warn($"{route.Path} answered {status} ({code})");
            throw new HelperErrorException(status, code);
        }
        if (answer is null)
        {
            log.Warn($"{route.Path} answered something that isn’t a JSON object");
            throw new HelperErrorException(status, "not-json");
        }
        log.Info($"{route.Path} answered");
        return answer;
    }

    private static JsonObject? Parse(byte[] raw)
    {
        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private HelperUnreachableException Unreachable(ChannelRoute route, string why)
    {
        log.Info($"{route.Path}: Healdan Helper can’t be reached ({why})");
        return new HelperUnreachableException($"Healdan Helper can’t be reached ({why})");
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
```

`CoffeePdf.Core/Helper/HealdanLink.cs`:
```csharp
using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// What a window needs of Healdan (spec §5, §9): whether Healdan support is on, the Helper's folders, its channel, and
/// the log. The app has one. A test makes its own, with a fake Helper's folders and settings of its own
/// (<c>FakeHelper.Link</c>).
/// </summary>
public sealed class HealdanLink(SettingsStore settings, HelperFolders folders, CoffeeLog log, HelperChannel? channel = null)
{
    public SettingsStore Settings => settings;

    public HelperFolders Folders => folders;

    public CoffeeLog Log => log;

    public HelperChannel Channel { get; } = channel ?? new HelperChannel(folders, log);

    /// <summary>Healdan support (§5), read from <c>settings.json</c> each time, as the Helper reads it too.</summary>
    public bool Enabled => settings.HealdanEnabled;
}
```

- [ ] **Step 4: Give the fake Helper its link**

In `CoffeePdf.Core.Tests/Fakes/FakeHelper.cs`, add `using CoffeePdf.Core.Logging;` and
`using CoffeePdf.Core.Settings;`, and add after `ComeBack`:
```csharp
    /// <summary>
    /// CoffeePDF's link to this Helper: settings of its own, in the support folder, with Healdan support
    /// <paramref name="enabled"/>, and a log beside them. <paramref name="waits"/> replaces the channel's time limits.
    /// </summary>
    public HealdanLink Link(bool enabled = true, IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null)
    {
        var settings = new SettingsStore(Path.Combine(Folders.Support, "coffeepdf-settings")) { HealdanEnabled = enabled };
        var log = new CoffeeLog(Path.Combine(Folders.Support, "coffeepdf-log"));
        return new HealdanLink(settings, Folders, log, new HelperChannel(Folders, log, waits));
    }
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~HelperChannelTests|FullyQualifiedName~FakeHelperTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf.Core/Helper CoffeePdf.Core.Tests/Helper CoffeePdf.Core.Tests/Fakes
git commit -m "Call the Helper's channel, with the secret, and wait for its windows however long they take"
```

---

### Task 5: A window's mode, title, bar and commands

**Files:**
- Create: `CoffeePdf.Core/Documents/WindowDocument.cs`, `CoffeePdf.Core/Documents/WindowCommands.cs`
- Modify: `CoffeePdf.Core/Documents/DocumentTitle.cs`
- Test: `CoffeePdf.Core.Tests/Documents/WindowDocumentTests.cs`, `CoffeePdf.Core.Tests/Documents/DocumentTitleTests.cs`

**Interfaces:**
- Consumes: `CopyName` (Task 1); `CopyState` (Task 3); `DocumentTitle.For(string?, bool)` (Phase 1).
- Produces:
  - `enum DocumentMode { Local, Asking, Editable, ReadOnly, HelperAway, SupportOff }`.
  - `sealed record WindowDocument(string Path, DocumentMode Mode, CopyState? Copy = null, bool Refused = false)`:
    - `static WindowDocument Opened(string path, bool healdan, bool inCopies)`;
    - `WindowDocument Answered(CopyState? state)`, where null means the Helper didn't answer;
    - `bool HoldsLock`, `bool TellsHelper` and `bool FollowsSaves`;
    - `string Title(bool edited, bool saving)`, `WindowBar? Bar`, `WindowCommands Commands(bool healdan)` and
      `string NameForHealdan`.
  - `sealed record WindowCommands(bool Markup, bool Save, bool HealdanMenu, bool SaveToHealdan, bool SaveAsNewVersion, bool SaveAsNewDocument, bool Properties, bool HelperBoxes)`,
    with `static WindowCommands Empty(bool healdan)`.
  - `enum BarAction { None, SaveAsNewDocument, TurnOnHealdan }`.
  - `sealed record WindowBar(string Text, BarAction Action)`, with the texts `ReadOnlyOpen`, `Refused`, `HelperAway`,
    `HelperAwayReadOnly` and `SupportOff`, and `static string HeldBy(string holder)`.
  - `DocumentTitle.ForCopy(string name, string docRef, bool edited, bool saving) : string`.

The modes follow §6.1's table, with the assumptions of questions 1, 2, 5 and 7. `Asking` is a copy whose `/v1/state`
hasn't answered yet: read-only meanwhile, so nothing can be marked up before the mode is known.

- [ ] **Step 1: Write the failing tests**

Add to `CoffeePdf.Core.Tests/Documents/DocumentTitleTests.cs`:
```csharp
    [Fact]
    public void A_copy_shows_its_name_and_doc_ref() =>
        Assert.Equal("Settlement Agreement — #1234 v2.1", DocumentTitle.ForCopy("Settlement Agreement", "#1234 v2.1", edited: false, saving: false));

    [Fact]
    public void A_copy_says_Edited_and_Saving_to_Healdan() =>
        Assert.Equal("Settlement Agreement — #1234 v2.1 — Edited — Saving to Healdan…",
            DocumentTitle.ForCopy("Settlement Agreement", "#1234 v2.1", edited: true, saving: true));
```

`CoffeePdf.Core.Tests/Documents/WindowDocumentTests.cs`:
```csharp
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Documents;

/// <summary>Spec §6.1: a window's mode, and what it decides.</summary>
public sealed class WindowDocumentTests
{
    private const string Copy = "/support/Copies/Settlement Agreement #1234 v2.1.pdf";
    private const string ReadOnlyCopy = "/support/Copies/Settlement Agreement #1234 v2.1 (read-only).pdf";

    private static CopyState State(bool readOnly = false, string? holdBy = null, string version = "2.1", bool pending = false) =>
        new(true, 1234, $"Settlement Agreement #1234 v{version}{(readOnly ? " (read-only)" : "")}", $"#1234 v{version}",
            readOnly, readOnly ? ReadOnlyCopy : Copy, holdBy, pending);

    private static WindowDocument Asking(string path = Copy) => WindowDocument.Opened(path, healdan: true, inCopies: true);

    [Fact]
    public void A_file_outside_Copies_is_local_whatever_the_setting()
    {
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", healdan: true, inCopies: false).Mode);
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", healdan: false, inCopies: false).Mode);
    }

    [Fact]
    public void A_copy_with_support_off_is_read_only_until_support_is_on() =>
        Assert.Equal(DocumentMode.SupportOff, WindowDocument.Opened(Copy, healdan: false, inCopies: true).Mode);

    [Fact]
    public void A_copy_with_support_on_asks_the_Helper() => Assert.Equal(DocumentMode.Asking, Asking().Mode);

    [Fact]
    public void The_Helpers_answer_sets_the_mode()
    {
        Assert.Equal(DocumentMode.Editable, Asking().Answered(State()).Mode);
        Assert.Equal(DocumentMode.ReadOnly, Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Mode);
        Assert.False(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Refused);
        Assert.Equal(DocumentMode.HelperAway, Asking().Answered(null).Mode);
        Assert.Equal(DocumentMode.Local, Asking().Answered(CopyState.Unknown(Copy)).Mode);
    }

    [Fact]
    public void A_read_only_answer_for_an_editable_copy_is_a_refused_save()
    {
        var refused = Asking().Answered(State()).Answered(State(readOnly: true));

        Assert.Equal(DocumentMode.ReadOnly, refused.Mode);
        Assert.True(refused.Refused);
        Assert.True(Asking().Answered(State()).Answered(null).Answered(State(readOnly: true)).Refused);
    }

    [Fact]
    public void An_editable_copy_whose_Helper_goes_is_the_Helper_away_one_until_it_answers()
    {
        var away = Asking().Answered(State()).Answered(null);

        Assert.Equal(DocumentMode.HelperAway, away.Mode);
        Assert.Equal(DocumentMode.Editable, away.Answered(State(version: "2.2")).Mode);
    }

    [Fact]
    public void A_read_only_copy_stays_read_only()
    {
        var readOnly = Asking(ReadOnlyCopy).Answered(State(readOnly: true));

        Assert.Equal(DocumentMode.ReadOnly, readOnly.Answered(State()).Mode);
        Assert.Equal(DocumentMode.ReadOnly, readOnly.Answered(null).Mode);
    }

    [Fact]
    public void Local_and_support_off_windows_dont_take_the_Helpers_answers()
    {
        Assert.Equal(DocumentMode.Local, WindowDocument.Opened("/docs/Report.pdf", true, false).Answered(null).Mode);
        Assert.Equal(DocumentMode.SupportOff, WindowDocument.Opened(Copy, false, true).Answered(State()).Mode);
    }

    [Fact]
    public void A_copys_title_is_its_name_and_the_version_Healdan_has()
    {
        Assert.Equal("Settlement Agreement — #1234 v2.1", Asking().Answered(State()).Title(edited: false, saving: false));
        Assert.Equal("Settlement Agreement — #1234 v2.1 — Edited", Asking().Answered(State()).Title(edited: true, saving: false));
        // A save moves the title on, though the copy keeps the name it was opened with.
        Assert.Equal("Settlement Agreement — #1234 v2.2 — Saving to Healdan…",
            Asking().Answered(State()).Answered(State(version: "2.2", pending: true)).Title(edited: false, saving: true));
    }

    [Fact]
    public void A_read_only_copys_title_leaves_the_read_only_mark_to_its_bar() =>
        Assert.Equal("Settlement Agreement — #1234 v2.1", Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Title(false, false));

    [Fact]
    public void Without_the_Helpers_answer_the_title_comes_from_the_copys_name()
    {
        Assert.Equal("Settlement Agreement — #1234 v2.1", Asking().Title(false, false));
        Assert.Equal("Settlement Agreement — #1234 v2.1", Asking().Answered(null).Title(false, false));
        Assert.Equal("Settlement Agreement — #1234 v2.1", WindowDocument.Opened(Copy, false, true).Title(false, false));
    }

    [Fact]
    public void A_title_that_doesnt_end_in_its_doc_ref_gives_way_to_the_copys_name() =>
        Assert.Equal("Settlement Agreement — #1234 v2.1",
            Asking().Answered(State() with { Title = "Something else entirely" }).Title(false, false));

    [Fact]
    public void Local_files_and_names_that_arent_a_copys_show_the_file_name()
    {
        Assert.Equal("Report.pdf — Edited", WindowDocument.Opened("/docs/Report.pdf", true, false).Title(edited: true, saving: false));
        Assert.Equal("notes.pdf", Asking("/support/Copies/notes.pdf").Answered(null).Title(false, false));
    }

    [Fact]
    public void Saving_to_Healdan_shows_only_where_saves_go_there() =>
        Assert.Equal("Report.pdf", WindowDocument.Opened("/docs/Report.pdf", true, false).Title(edited: false, saving: true));

    [Fact]
    public void Each_copy_mode_has_its_bar()
    {
        Assert.Null(WindowDocument.Opened("/docs/Report.pdf", true, false).Bar);
        Assert.Null(Asking().Bar);
        Assert.Null(Asking().Answered(State()).Bar);
        Assert.Equal(new WindowBar(WindowBar.HeldBy("Zoë Q. Reviewer"), BarAction.SaveAsNewDocument),
            Asking(ReadOnlyCopy).Answered(State(readOnly: true, holdBy: "Zoë Q. Reviewer")).Bar);
        Assert.Equal(new WindowBar(WindowBar.ReadOnlyOpen, BarAction.SaveAsNewDocument), Asking(ReadOnlyCopy).Answered(State(readOnly: true)).Bar);
        Assert.Equal(new WindowBar(WindowBar.Refused, BarAction.SaveAsNewDocument), Asking().Answered(State()).Answered(State(readOnly: true)).Bar);
        Assert.Equal(new WindowBar(WindowBar.HelperAway, BarAction.None), Asking().Answered(null).Bar);
        Assert.Equal(new WindowBar(WindowBar.HelperAwayReadOnly, BarAction.None), Asking(ReadOnlyCopy).Answered(null).Bar);
        Assert.Equal(new WindowBar(WindowBar.SupportOff, BarAction.TurnOnHealdan), WindowDocument.Opened(Copy, false, true).Bar);
        Assert.Equal("Read-only — checked out by Zoë Q. Reviewer", WindowBar.HeldBy("Zoë Q. Reviewer"));
    }

    [Fact]
    public void Each_mode_allows_what_spec_6_1_says()
    {
        static WindowCommands Of(DocumentMode mode, bool healdan = true, string path = Copy) => new WindowDocument(path, mode).Commands(healdan);

        Assert.Equal(new WindowCommands(Markup: true, Save: true, HealdanMenu: true, SaveToHealdan: true, SaveAsNewVersion: false,
            SaveAsNewDocument: false, Properties: true, HelperBoxes: true), Of(DocumentMode.Local));
        Assert.Equal(new WindowCommands(true, true, false, false, false, false, false, false), Of(DocumentMode.Local, healdan: false));
        Assert.Equal(new WindowCommands(true, true, true, true, true, true, true, true), Of(DocumentMode.Editable));
        Assert.Equal(new WindowCommands(false, false, true, false, false, true, false, true), Of(DocumentMode.ReadOnly));
        Assert.Equal(new WindowCommands(true, true, true, false, false, false, false, true), Of(DocumentMode.HelperAway));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), Of(DocumentMode.HelperAway, path: ReadOnlyCopy));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), Of(DocumentMode.Asking));
        Assert.Equal(new WindowCommands(false, false, false, false, false, false, false, false), Of(DocumentMode.SupportOff));
        // Turning support off waits for a copy's window to close (§5): a copy's commands don't follow the setting.
        Assert.Equal(Of(DocumentMode.Editable), Of(DocumentMode.Editable, healdan: false));
        Assert.Equal(new WindowCommands(false, false, true, false, false, false, false, true), WindowCommands.Empty(healdan: true));
        Assert.Equal(new WindowCommands(false, false, false, false, false, false, false, false), WindowCommands.Empty(healdan: false));
    }

    [Fact]
    public void Only_a_copy_the_window_may_edit_keeps_a_lock_and_follows_its_saves()
    {
        static bool Locks(WindowDocument document) => document.HoldsLock && document.FollowsSaves;

        Assert.True(Locks(Asking().Answered(State())));
        Assert.True(Locks(Asking().Answered(null)));
        Assert.False(Asking(ReadOnlyCopy).Answered(null).HoldsLock);
        Assert.False(Asking().HoldsLock);
        Assert.False(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).HoldsLock);
        Assert.False(WindowDocument.Opened(Copy, false, true).HoldsLock);
        Assert.False(WindowDocument.Opened("/docs/Report.pdf", true, false).HoldsLock);
    }

    [Fact]
    public void The_Helper_hears_about_every_copy_with_support_on()
    {
        Assert.True(Asking().TellsHelper);
        Assert.True(Asking().Answered(State()).TellsHelper);
        Assert.True(Asking(ReadOnlyCopy).Answered(State(readOnly: true)).TellsHelper);
        Assert.True(Asking().Answered(null).TellsHelper);
        Assert.False(WindowDocument.Opened(Copy, false, true).TellsHelper);
        Assert.False(WindowDocument.Opened("/docs/Report.pdf", true, false).TellsHelper);
    }

    [Fact]
    public void The_Save_window_starts_with_the_documents_name()
    {
        Assert.Equal("Settlement Agreement", Asking().Answered(State()).NameForHealdan);
        Assert.Equal("Settlement Agreement", Asking().Answered(null).NameForHealdan);
        Assert.Equal("Report", WindowDocument.Opened("/docs/Report.pdf", true, false).NameForHealdan);
        Assert.Equal("Report #1 v2", WindowDocument.Opened("/docs/Report #1 v2.pdf", true, false).NameForHealdan);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~WindowDocumentTests|FullyQualifiedName~DocumentTitleTests"`
Expected: the build fails. `WindowDocument` and `DocumentTitle.ForCopy` don't exist yet.

- [ ] **Step 3: Write the title, the commands and the bar**

In `CoffeePdf.Core/Documents/DocumentTitle.cs`, change the class's summary to
`/// <summary>A document window's title (spec §6.1): a local file's, and a Healdan copy's.</summary>`, and add:
```csharp
    /// <summary>
    /// A Healdan copy's title (spec §6.1): <c>Settlement Agreement — #1234 v2.1</c>, with " — Edited" while it has
    /// unsaved changes, and " — Saving to Healdan…" while a save is on its way there (§6.4).
    /// </summary>
    public static string ForCopy(string name, string docRef, bool edited, bool saving) =>
        $"{name} — {docRef}{(edited ? " — Edited" : "")}{(saving ? " — Saving to Healdan…" : "")}";
```

`CoffeePdf.Core/Documents/WindowCommands.cs`:
```csharp
namespace CoffeePdf.Core.Documents;

/// <summary>
/// What a window allows in its mode (spec §6.1, §6.3): marking up and saving, the Healdan menu and its items, and
/// whether ⌘O and ⇧⌘S go to the Helper's windows first.
/// </summary>
public sealed record WindowCommands(bool Markup, bool Save, bool HealdanMenu, bool SaveToHealdan, bool SaveAsNewVersion,
    bool SaveAsNewDocument, bool Properties, bool HelperBoxes)
{
    /// <summary>An empty window's: nothing to mark up or save. While support is on, the Healdan menu shows and ⌘O goes to the Helper.</summary>
    public static WindowCommands Empty(bool healdan) => new(false, false, healdan, false, false, false, false, healdan);
}

/// <summary>What a bar's button does.</summary>
public enum BarAction
{
    None,
    SaveAsNewDocument,
    TurnOnHealdan,
}

/// <summary>The bar across the top of a Healdan copy's window (spec §6.1, §6.9), and its one button.</summary>
public sealed record WindowBar(string Text, BarAction Action)
{
    public const string ReadOnlyOpen = "Read-only — opened without a check-out";

    public const string Refused =
        "Healdan didn’t take this copy’s last save: it’s no longer checked out to you. Use Save as New Document to keep your changes.";

    public const string HelperAway = "Healdan Helper isn’t running. Saves stay on this Mac, and go to Healdan once it’s back.";

    public const string HelperAwayReadOnly = "Read-only — Healdan Helper isn’t running.";

    public const string SupportOff = "This is a copy from Healdan, and Healdan support is off, so it’s read-only.";

    /// <summary>A read-only copy of a document someone has checked out. The Helper's <c>holdBy</c> names the person, not the computer.</summary>
    public static string HeldBy(string holder) => $"Read-only — checked out by {holder}";
}
```

- [ ] **Step 4: Write the window's document**

`CoffeePdf.Core/Documents/WindowDocument.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Documents;

/// <summary>A document window's mode (spec §6.1).</summary>
public enum DocumentMode
{
    /// <summary>A file outside the Helper's <c>Copies/</c> folder, or one the Helper doesn't know.</summary>
    Local,

    /// <summary>A copy whose <c>/v1/state</c> hasn't answered yet (§6.2): read-only meanwhile.</summary>
    Asking,

    /// <summary>A copy checked out to this computer.</summary>
    Editable,

    /// <summary>A read-only open, a document held elsewhere, or a copy whose save Healdan refused (§6.9).</summary>
    ReadOnly,

    /// <summary>A copy while the Helper's channel doesn't answer (§6.9).</summary>
    HelperAway,

    /// <summary>A copy, with Healdan support off: read-only, with a bar offering to turn support on.</summary>
    SupportOff,
}

/// <summary>
/// A window's file as CoffeePDF sees it (spec §6.1): where it is, its mode, and what the Helper last said of it. The
/// window keeps one and replaces it as the Helper answers. Everything the mode decides is here, so it's tested
/// without a window.
/// </summary>
/// <param name="Path">The file's full path.</param>
/// <param name="Mode">Its mode.</param>
/// <param name="Copy">The Helper's last answer about it, for a copy.</param>
/// <param name="Refused">A copy whose save Healdan refused, read-only since (§6.9).</param>
public sealed record WindowDocument(string Path, DocumentMode Mode, CopyState? Copy = null, bool Refused = false)
{
    /// <summary>
    /// A file just opened (§6.2): local outside the Helper's <c>Copies/</c> folder, <see cref="DocumentMode.SupportOff"/>
    /// for a copy with Healdan support off, and otherwise <see cref="DocumentMode.Asking"/> until the Helper answers.
    /// </summary>
    public static WindowDocument Opened(string path, bool healdan, bool inCopies) =>
        new(System.IO.Path.GetFullPath(path), !inCopies ? DocumentMode.Local : !healdan ? DocumentMode.SupportOff : DocumentMode.Asking);

    /// <summary>
    /// The document after the Helper's answer about it, or after it didn't answer (<paramref name="state"/> null). A
    /// read-only answer for a copy the Helper had called editable is a refused save (§6.9). A read-only copy stays so,
    /// since the Helper doesn't hand a check-out back to an open window.
    /// </summary>
    public WindowDocument Answered(CopyState? state)
    {
        if (Mode is DocumentMode.Local or DocumentMode.SupportOff)
            return this; // the Helper isn't asked about these
        if (state is null)
            return Mode == DocumentMode.ReadOnly ? this : this with { Mode = DocumentMode.HelperAway };
        if (!state.Known)
            return Mode == DocumentMode.Asking ? this with { Mode = DocumentMode.Local } : this;
        if (Mode == DocumentMode.ReadOnly)
            return this with { Copy = state };
        if (state.ReadOnly)
            return this with { Mode = DocumentMode.ReadOnly, Copy = state, Refused = Copy is { Known: true, ReadOnly: false } };
        return this with { Mode = DocumentMode.Editable, Copy = state };
    }

    /// <summary>
    /// Whether the window keeps the copy's lock file while it's open (§6.8): an editable copy, as the Helper says, or as
    /// its name says while the Helper is away.
    /// </summary>
    public bool HoldsLock => Mode == DocumentMode.Editable || (Mode == DocumentMode.HelperAway && !ReadOnlyByName);

    /// <summary>Whether the window's saves are followed to Healdan (§6.4), which goes on while the Helper is away (§6.9).</summary>
    public bool FollowsSaves => HoldsLock;

    /// <summary>Whether the Helper is told when the window lets the copy go (§6.8): every copy, while support is on.</summary>
    public bool TellsHelper => Mode is DocumentMode.Asking or DocumentMode.Editable or DocumentMode.ReadOnly or DocumentMode.HelperAway;

    /// <summary>The window's title (§6.1). <paramref name="saving"/> shows only where saves go to Healdan.</summary>
    public string Title(bool edited, bool saving) =>
        Mode != DocumentMode.Local && CopyTitle() is { } copy
            ? DocumentTitle.ForCopy(copy.Name, copy.DocRef, edited, saving && FollowsSaves)
            : DocumentTitle.For(Path, edited);

    /// <summary>The bar across the window's top, if its mode has one (§6.1, §6.9).</summary>
    public WindowBar? Bar => Mode switch
    {
        DocumentMode.ReadOnly when Refused => new(WindowBar.Refused, BarAction.SaveAsNewDocument),
        DocumentMode.ReadOnly when Copy?.HoldBy is { Length: > 0 } holder => new(WindowBar.HeldBy(holder), BarAction.SaveAsNewDocument),
        DocumentMode.ReadOnly => new(WindowBar.ReadOnlyOpen, BarAction.SaveAsNewDocument),
        DocumentMode.HelperAway when ReadOnlyByName => new(WindowBar.HelperAwayReadOnly, BarAction.None),
        DocumentMode.HelperAway => new(WindowBar.HelperAway, BarAction.None),
        DocumentMode.SupportOff => new(WindowBar.SupportOff, BarAction.TurnOnHealdan),
        _ => null,
    };

    /// <summary>
    /// What the mode allows (§6.1, §6.3). A local window follows Healdan support as it is now; a copy's mode was set
    /// when it opened, and turning support off waits for its window to close (§5).
    /// </summary>
    public WindowCommands Commands(bool healdan)
    {
        var editable = !ReadOnlyByName;
        return Mode switch
        {
            DocumentMode.Local => new(Markup: true, Save: true, HealdanMenu: healdan, SaveToHealdan: healdan, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: healdan, HelperBoxes: healdan),
            // Save to Healdan on a copy is its ordinary save, as in the LibreOffice extension.
            DocumentMode.Editable => new(Markup: true, Save: true, HealdanMenu: true, SaveToHealdan: true, SaveAsNewVersion: true,
                SaveAsNewDocument: true, Properties: true, HelperBoxes: true),
            DocumentMode.ReadOnly => new(Markup: false, Save: false, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: true, Properties: false, HelperBoxes: true),
            // Healdan's commands are off with the Helper away. ⌘O and ⇧⌘S still try it first, and fall to the Mac's boxes at once.
            DocumentMode.HelperAway => new(Markup: editable, Save: editable, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: true),
            DocumentMode.Asking => new(Markup: false, Save: false, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: true),
            _ => new(Markup: false, Save: false, HealdanMenu: false, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: false),
        };
    }

    /// <summary>
    /// The name the Helper's Save window starts with (§6.6): a copy's name without its doc ref, else the file's name
    /// without its extension, as the LibreOffice extension's.
    /// </summary>
    public string NameForHealdan =>
        (Mode == DocumentMode.Local ? null : CopyTitle()?.Name)
        ?? (System.IO.Path.GetFileNameWithoutExtension(Path) is { Length: > 0 } stem ? stem : "Untitled");

    /// <summary>The copy's name says it's read-only: <c>… (read-only).pdf</c>.</summary>
    private bool ReadOnlyByName => CopyName.Parse(Path)?.ReadOnly == true;

    /// <summary>The copy's name and doc ref: from the Helper's answer when it gave them, else from the file's name.</summary>
    private (string Name, string DocRef)? CopyTitle()
    {
        if (Copy is { Known: true } copy && CopyName.NameIn(copy.Title, copy.DocRef) is { } name)
            return (name, copy.DocRef);
        return CopyName.Parse(Path) is { } parts ? (parts.Name, parts.DocRef) : null;
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~WindowDocumentTests|FullyQualifiedName~DocumentTitleTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf.Core/Documents CoffeePdf.Core.Tests/Documents
git commit -m "Decide a window's mode, title, bar and commands from the Helper's answers"
```

---

### Task 6: Following a save

**Files:**
- Create: `CoffeePdf.Core/Documents/SaveFollower.cs`
- Test: `CoffeePdf.Core.Tests/Documents/SaveFollowerTests.cs`

**Interfaces:**
- Consumes: `CopyState` (Task 3); `HelperUnreachableException` and `HelperErrorException` (Task 4).
- Produces: `static class SaveFollower`:
  - `IReadOnlyList<TimeSpan> Pauses` (2, 5, 15, 30 and 60 seconds);
  - `TimeSpan PauseBefore(int round)`;
  - `Task FollowAsync(Func<CancellationToken, Task<CopyState>> saved, Action<CopyState> answered, Action unreachable, Func<TimeSpan, CancellationToken, Task> pause, CancellationToken stop)`.

  It never throws: it ends at an answer that isn't `pending`, at an error answer, or when `stop` is cancelled.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Documents/SaveFollowerTests.cs`:
```csharp
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Documents;

/// <summary>Spec §6.4: following a save, as the LibreOffice extension does.</summary>
public sealed class SaveFollowerTests
{
    private static readonly CopyState Pending = new(true, 1234, "Settlement Agreement #1234 v2.1", "#1234 v2.1", false, "/c.pdf", null, Pending: true);
    private static readonly CopyState Done = Pending with { Title = "Settlement Agreement #1234 v2.2", DocRef = "#1234 v2.2", Pending = false };

    private static Func<Task<CopyState>> Answer(CopyState state) => () => Task.FromResult(state);

    private static Func<Task<CopyState>> Away() => () => Task.FromException<CopyState>(new HelperUnreachableException("away"));

    /// <summary>Follows with <paramref name="turns"/> answering in order, and pauses that pass at once but are counted.</summary>
    private static async Task<(List<TimeSpan> Pauses, List<CopyState> Answers, int Unreachable, int Asked)> FollowAsync(
        IEnumerable<Func<Task<CopyState>>> turns, CancellationTokenSource? stop = null, int stopAtPause = 0)
    {
        var queue = new Queue<Func<Task<CopyState>>>(turns);
        var pauses = new List<TimeSpan>();
        var answers = new List<CopyState>();
        var unreachable = 0;
        var asked = 0;
        await SaveFollower.FollowAsync(
            _ =>
            {
                asked++;
                return queue.Dequeue()();
            },
            answers.Add,
            () => unreachable++,
            (pause, token) =>
            {
                pauses.Add(pause);
                if (pauses.Count == stopAtPause)
                    stop!.Cancel(); // the window closed during this pause
                return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
            },
            stop?.Token ?? CancellationToken.None);
        return (pauses, answers, unreachable, asked);
    }

    [Fact]
    public void The_pauses_are_2_5_15_30_then_60_seconds_for_ever()
    {
        Assert.Equal(
            new double[] { 2, 5, 15, 30, 60, 60, 60 },
            Enumerable.Range(1, 7).Select(round => SaveFollower.PauseBefore(round).TotalSeconds));
    }

    [Fact]
    public async Task An_answer_thats_not_pending_ends_it_at_once()
    {
        var (pauses, answers, _, asked) = await FollowAsync([Answer(Done)]);

        Assert.Empty(pauses);
        Assert.Equal(new[] { Done }, answers);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task While_the_answer_is_pending_it_asks_again_after_each_pause()
    {
        var (pauses, answers, _, _) = await FollowAsync(Enumerable.Repeat(Answer(Pending), 7).Append(Answer(Done)));

        Assert.Equal(new double[] { 2, 5, 15, 30, 60, 60, 60 }, pauses.Select(p => p.TotalSeconds));
        Assert.Equal(8, answers.Count);
        Assert.Equal(Done, answers[^1]);
    }

    [Fact]
    public async Task While_the_Helper_cant_be_reached_it_keeps_asking_the_same_way()
    {
        var (pauses, answers, unreachable, _) = await FollowAsync([Away(), Away(), Away(), Answer(Done)]);

        Assert.Equal(3, unreachable);
        Assert.Equal(new double[] { 2, 5, 15 }, pauses.Select(p => p.TotalSeconds));
        Assert.Equal(new[] { Done }, answers);
    }

    [Fact]
    public async Task Stopping_it_ends_it_without_another_question()
    {
        using var stop = new CancellationTokenSource();

        var (pauses, answers, _, asked) = await FollowAsync([Answer(Pending), Answer(Done)], stop, stopAtPause: 1);

        Assert.Single(pauses);
        Assert.Equal(new[] { Pending }, answers);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task An_answer_that_comes_after_its_stopped_isnt_taken()
    {
        using var stop = new CancellationTokenSource();
        var answers = new List<CopyState>();

        await SaveFollower.FollowAsync(_ =>
        {
            stop.Cancel(); // a newer save took over while this one was asked
            return Task.FromResult(Done);
        }, answers.Add, () => { }, (_, _) => Task.CompletedTask, stop.Token);

        Assert.Empty(answers);
    }

    [Fact]
    public async Task An_error_answer_ends_it()
    {
        var (pauses, answers, _, asked) = await FollowAsync([() => Task.FromException<CopyState>(new HelperErrorException(500, "internal-error")), Answer(Done)]);

        Assert.Empty(pauses);
        Assert.Empty(answers);
        Assert.Equal(1, asked);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~SaveFollowerTests`
Expected: the build fails. `SaveFollower` doesn't exist yet.

- [ ] **Step 3: Write the follower**

`CoffeePdf.Core/Documents/SaveFollower.cs`:
```csharp
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Documents;

/// <summary>
/// Follows a copy's save to Healdan (spec §6.4), as the LibreOffice extension does. It asks <c>/v1/saved</c>, which
/// answers once Healdan has the save, or after 120 seconds with the save still <c>pending</c>. While it's pending, or
/// the Helper can't be reached, it asks again after 2, 5 and 15 seconds, then 30, then every 60.
/// </summary>
public static class SaveFollower
{
    public static readonly IReadOnlyList<TimeSpan> Pauses =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>The pause before round <paramref name="round"/>, counting the first question as round 0.</summary>
    public static TimeSpan PauseBefore(int round) => Pauses[Math.Min(round, Pauses.Count) - 1];

    /// <summary>
    /// Asks <paramref name="saved"/> until an answer isn't pending. Each answer goes to <paramref name="answered"/>, and
    /// each time the Helper can't be reached, <paramref name="unreachable"/> is called. It ends quietly when
    /// <paramref name="stop"/> is cancelled (the window closed, a newer save took over, or the file was saved
    /// elsewhere), or at an error answer, which leaves the title as it is, as in the extension. It keeps the caller's
    /// context, so the callbacks run on the window's thread.
    /// </summary>
    public static async Task FollowAsync(Func<CancellationToken, Task<CopyState>> saved, Action<CopyState> answered, Action unreachable,
        Func<TimeSpan, CancellationToken, Task> pause, CancellationToken stop)
    {
        try
        {
            for (var round = 0; !stop.IsCancellationRequested; round++)
            {
                if (round > 0)
                    await pause(PauseBefore(round), stop);
                CopyState state;
                try
                {
                    state = await saved(stop);
                }
                catch (HelperUnreachableException)
                {
                    if (!stop.IsCancellationRequested)
                        unreachable();
                    continue;
                }
                if (stop.IsCancellationRequested)
                    return;
                answered(state);
                if (!state.Pending)
                    return;
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Stopped during a pause or a question.
        }
        catch (HelperErrorException)
        {
            // The Helper answered the question itself with an error: there's nothing more to follow.
        }
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~SaveFollowerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Documents/SaveFollower.cs CoffeePdf.Core.Tests/Documents/SaveFollowerTests.cs
git commit -m "Follow a copy's save until Healdan has it, asking again after 2, 5, 15, 30, then every 60 seconds"
```

---

### Task 7: The lock file

**Files:**
- Create: `CoffeePdf.Core/Locks/CopyLock.cs`
- Test: `CoffeePdf.Core.Tests/Locks/CopyLockTests.cs`

**Interfaces:**
- Produces: `static class CopyLock`: `const string Prefix = ".~coffeepdf."`, `const string Suffix = "#"`,
  `string PathFor(string copy)`, `void Write(string copy, int processId, DateTimeOffset opened)`, and
  `bool TryRemove(string copy)`, which never throws.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Locks/CopyLockTests.cs`:
```csharp
using System.Text;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Locks;

namespace CoffeePdf.Core.Tests.Locks;

/// <summary>Spec §6.8: the lock file beside an editable copy, which the Helper looks for.</summary>
public sealed class CopyLockTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_lock_sits_beside_the_copy_named_for_it() =>
        Assert.Equal(Path.Combine(_dir, ".~coffeepdf.Settlement Agreement #1234 v2.1.pdf#"),
            CopyLock.PathFor(Path.Combine(_dir, "Settlement Agreement #1234 v2.1.pdf")));

    [Fact]
    public void Its_13_added_bytes_fit_beside_the_longest_copy_name()
    {
        // The Helper keeps copies' names to 241 bytes (the Desktop Clients page), so their records fit in 255.
        var copy = Path.Combine(_dir, new string('a', 237) + ".pdf");

        CopyLock.Write(copy, 4321, DateTimeOffset.Now);

        Assert.Equal(254, Encoding.UTF8.GetByteCount(Path.GetFileName(CopyLock.PathFor(copy))));
        Assert.True(File.Exists(CopyLock.PathFor(copy)));
    }

    [Fact]
    public void It_holds_the_process_and_when_the_window_opened_and_replaces_a_crashs_lock()
    {
        var copy = Path.Combine(_dir, "Report #1 v1.pdf");
        File.WriteAllText(CopyLock.PathFor(copy), "left by a crash");
        var opened = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);

        CopyLock.Write(copy, 4321, opened);

        var json = JsonNode.Parse(File.ReadAllText(CopyLock.PathFor(copy)))!;
        Assert.Equal(4321, json["pid"]!.GetValue<int>());
        Assert.Equal(opened, DateTimeOffset.Parse(json["opened"]!.GetValue<string>()));
    }

    [Fact]
    public void Removing_takes_the_lock_and_a_lock_thats_gone_is_fine()
    {
        var copy = Path.Combine(_dir, "Report #1 v1.pdf");
        CopyLock.Write(copy, 4321, DateTimeOffset.Now);

        Assert.True(CopyLock.TryRemove(copy));
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.False(CopyLock.TryRemove(copy));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~CopyLockTests`
Expected: the build fails. `CoffeePdf.Core.Locks` doesn't exist yet.

- [ ] **Step 3: Write the lock**

`CoffeePdf.Core/Locks/CopyLock.cs`:
```csharp
using System.Text.Json;

namespace CoffeePdf.Core.Locks;

/// <summary>
/// The lock file CoffeePDF keeps beside an editable copy while its window is open (spec §6.8):
/// <c>.~coffeepdf.&lt;copy's file name&gt;#</c>, which the Helper's <c>CoffeePdfApp.LockExists</c> looks for (§4.4
/// item 1). Its 13 added bytes fit within the 14 that the Helper's 241-byte limit on copies' names leaves. It holds
/// CoffeePDF's process id and when the window opened, for diagnosis. The Helper only checks that it's there, and
/// treats one left with no CoffeePDF running as a crash's.
/// </summary>
public static class CopyLock
{
    public const string Prefix = ".~coffeepdf.";

    public const string Suffix = "#";

    public static string PathFor(string copy)
    {
        var full = Path.GetFullPath(copy);
        return Path.Combine(Path.GetDirectoryName(full)!, Prefix + Path.GetFileName(full) + Suffix);
    }

    /// <summary>Writes the lock for <paramref name="copy"/>, replacing one a crash left.</summary>
    public static void Write(string copy, int processId, DateTimeOffset opened) =>
        File.WriteAllText(PathFor(copy), JsonSerializer.Serialize(new { pid = processId, opened = opened.ToString("O") }));

    /// <summary>
    /// Removes the lock for <paramref name="copy"/>, and returns whether there was one. It never throws: a lock that
    /// can't be removed is left to the Helper's crash handling (§6.8).
    /// </summary>
    public static bool TryRemove(string copy)
    {
        try
        {
            var path = PathFor(copy);
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~CopyLockTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Locks CoffeePdf.Core.Tests/Locks
git commit -m "Keep a lock file beside an editable copy"
```

---

### Task 8: Healdan support in Settings, and the first launch's question

**Files:**
- Modify: `CoffeePdf.Core/Settings/SettingsStore.cs`
- Create: `CoffeePdf.Core/Settings/FirstLaunch.cs`
- Create: `CoffeePdf/Platform/CoreFoundation.cs`, `CoffeePdf/Platform/HelperInstall.cs`
- Modify: `CoffeePdf/Platform/DefaultPdfApp.cs`, `CoffeePdf/CoffeePdf.csproj` (a comment)
- Modify: `CoffeePdf/Windows/SettingsWindow.axaml`, `CoffeePdf/Windows/SettingsWindow.axaml.cs`, `CoffeePdf/App.axaml.cs`, `CoffeePdf.Tests/Headless.cs`
- Test: `CoffeePdf.Core.Tests/Settings/SettingsStoreTests.cs`, `CoffeePdf.Core.Tests/Settings/FirstLaunchTests.cs`,
  `CoffeePdf.Tests/Windows/SettingsWindowTests.cs`, `CoffeePdf.Tests/Platform/HelperInstallTests.cs`

**Interfaces:**
- Consumes: `SettingsStore` and `MessageDialog.ShowAsync` (Phase 1).
- Produces:
  - `SettingsStore.HealdanEnabledChanged : event EventHandler?`, raised after each change to `HealdanEnabled`.
  - `SettingsStore.HealdanAsked : bool`, kept as `healdan.asked`.
  - `static class FirstLaunch`: `Task AskAboutHealdanAsync(SettingsStore settings, Func<bool> helperInstalled, Func<Task<bool>> ask)`.
  - `internal interface IHelperInstall { bool IsInstalled(); }`, and `MacHelperInstall(string bundleId = "com.healdan.helper")`.
  - `internal static class CoreFoundation`: `CoreServices`, `CreateString`, `ManagedString`, `ArrayCount` and `Release`.
  - `SettingsWindow.HealdanBox`, a `CheckBox`.
  - `App.HelperInstall : IHelperInstall`, which the tests could replace.

- [ ] **Step 1: Write the failing tests**

Add to `CoffeePdf.Core.Tests/Settings/SettingsStoreTests.cs`:
```csharp
    [Fact]
    public void Whether_the_first_launch_asked_is_kept_beside_Healdan_support()
    {
        var store = new SettingsStore(_dir);
        Assert.False(store.HealdanAsked);

        store.HealdanAsked = true;

        Assert.True(JsonNode.Parse(File.ReadAllText(store.FilePath))!["healdan"]!["asked"]!.GetValue<bool>());
        Assert.True(new SettingsStore(_dir).HealdanAsked);
        Assert.False(new SettingsStore(_dir).HealdanEnabled);
    }

    [Fact]
    public void Changing_Healdan_support_tells_whoever_follows_it_after_its_kept()
    {
        var store = new SettingsStore(_dir);
        var seen = new List<bool>();
        store.HealdanEnabledChanged += (_, _) => seen.Add(new SettingsStore(_dir).HealdanEnabled);

        store.HealdanEnabled = true;
        store.HealdanEnabled = false;

        Assert.Equal(new[] { true, false }, seen);
    }
```

`CoffeePdf.Core.Tests/Settings/FirstLaunchTests.cs`:
```csharp
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

/// <summary>Spec §5: at first launch, with the Helper installed, CoffeePDF asks once whether to use it with Healdan.</summary>
public sealed class FirstLaunchTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private async Task<int> AskAsync(SettingsStore store, bool installed, bool answer)
    {
        var asked = 0;
        await FirstLaunch.AskAboutHealdanAsync(store, () => installed, () =>
        {
            asked++;
            return Task.FromResult(answer);
        });
        return asked;
    }

    [Fact]
    public async Task Yes_turns_Healdan_support_on()
    {
        var store = new SettingsStore(_dir);

        Assert.Equal(1, await AskAsync(store, installed: true, answer: true));
        Assert.True(store.HealdanEnabled);
        Assert.True(store.HealdanAsked);
    }

    [Fact]
    public async Task Not_now_leaves_it_off_and_it_isnt_asked_again()
    {
        var store = new SettingsStore(_dir);

        await AskAsync(store, installed: true, answer: false);

        Assert.False(store.HealdanEnabled);
        Assert.Equal(0, await AskAsync(store, installed: true, answer: true));
        Assert.False(store.HealdanEnabled);
    }

    [Fact]
    public async Task Without_the_Helper_it_isnt_asked() =>
        Assert.Equal(0, await AskAsync(new SettingsStore(_dir), installed: false, answer: true));

    [Fact]
    public async Task With_support_on_already_it_isnt_asked() =>
        Assert.Equal(0, await AskAsync(new SettingsStore(_dir) { HealdanEnabled = true }, installed: true, answer: false));
}
```

Add to `CoffeePdf.Tests/Windows/SettingsWindowTests.cs`:
```csharp
    [Fact]
    public async Task The_Healdan_box_shows_support_and_keeps_a_change()
    {
        var store = new SettingsStore(_dir);
        var changes = 0;
        store.HealdanEnabledChanged += (_, _) => changes++;

        var (before, after) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            var shown = window.HealdanBox.IsChecked;
            window.HealdanBox.IsChecked = true;
            return Task.FromResult((shown, store.HealdanEnabled));
        });

        Assert.False(before);
        Assert.True(after);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Healdan_support_that_cant_be_kept_leaves_the_box_and_nothing_crashes()
    {
        var store = new SettingsStore(_dir);
        var mode = File.GetUnixFileMode(_dir);

        var shown = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                window.HealdanBox.IsChecked = true;
                return Task.FromResult(window.HealdanBox.IsChecked);
            }
            finally
            {
                File.SetUnixFileMode(_dir, mode);
            }
        });

        Assert.True(shown);
        Assert.False(store.HealdanEnabled);
    }
```

`CoffeePdf.Tests/Platform/HelperInstallTests.cs`:
```csharp
using CoffeePdf.Platform;

namespace CoffeePdf.Tests.Platform;

/// <summary>Spec §5: the first launch asks only when the Helper is installed, as Launch Services tells.</summary>
public sealed class HelperInstallTests
{
    [Fact]
    public void An_app_Launch_Services_knows_is_installed() => Assert.True(new MacHelperInstall("com.apple.Preview").IsInstalled());

    [Fact]
    public void A_bundle_id_no_app_has_isnt() => Assert.False(new MacHelperInstall("com.healdan.no-such-app").IsInstalled());
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.sln --filter "FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~FirstLaunchTests|FullyQualifiedName~SettingsWindowTests|FullyQualifiedName~HelperInstallTests"`
Expected: the build fails. `HealdanAsked`, `FirstLaunch`, `HealdanBox` and `MacHelperInstall` don't exist yet.

- [ ] **Step 3: Keep the question's answer, and tell the windows when support changes**

In `CoffeePdf.Core/Settings/SettingsStore.cs`, change the class's summary's shape to
`{ "healdan": { "enabled": false, "asked": true }, "markup": { "author": "…" } }`, and replace the `HealdanEnabled`
property with:
```csharp
    /// <summary>Raised after Healdan support changes here, once the file has it, so the windows can follow (spec §5).</summary>
    public event EventHandler? HealdanEnabledChanged;

    /// <summary>Healdan support (§5), off by default.</summary>
    public bool HealdanEnabled
    {
        get => HealdanFlag("enabled");
        set
        {
            Change(root => Section(root, "healdan")["enabled"] = value);
            HealdanEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the first launch has asked to use CoffeePDF with Healdan (§5), so it asks once.</summary>
    public bool HealdanAsked
    {
        get => HealdanFlag("asked");
        set => Change(root => Section(root, "healdan")["asked"] = value);
    }
```

and add beside `Load`:
```csharp
    private bool HealdanFlag(string key) =>
        Load()["healdan"] is JsonObject healdan && healdan[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;
```

`CoffeePdf.Core/Settings/FirstLaunch.cs`:
```csharp
namespace CoffeePdf.Core.Settings;

/// <summary>The first launch's one question (spec §5): with the Helper installed, whether to use CoffeePDF with Healdan.</summary>
public static class FirstLaunch
{
    /// <summary>
    /// Asks through <paramref name="ask"/>, unless it was asked before, Healdan support is on already, or the Helper
    /// isn't installed. An answer counts as asked, and yes turns support on. A launch that ends before the answer asks
    /// again next time.
    /// </summary>
    public static async Task AskAboutHealdanAsync(SettingsStore settings, Func<bool> helperInstalled, Func<Task<bool>> ask)
    {
        if (settings.HealdanAsked || settings.HealdanEnabled || !helperInstalled())
            return;
        var yes = await ask();
        settings.HealdanAsked = true;
        if (yes)
            settings.HealdanEnabled = true;
    }
}
```

- [ ] **Step 4: Tell whether the Helper is installed**

Launch Services' calls share Core Foundation's strings, so they move out of `DefaultPdfApp.cs`.

`CoffeePdf/Platform/CoreFoundation.cs`:
```csharp
using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Core Foundation's strings and arrays, for Launch Services' calls (DefaultPdfApp, HelperInstall).</summary>
internal static unsafe partial class CoreFoundation
{
    /// <summary>Launch Services' framework.</summary>
    public const string CoreServices = "/System/Library/Frameworks/CoreServices.framework/CoreServices";

    private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>A CFString holding <paramref name="text"/>, which the caller releases.</summary>
    public static nint CreateString(string text)
    {
        fixed (char* characters = text)
            return CFStringCreateWithCharacters(0, characters, text.Length);
    }

    /// <summary>A CFString's text.</summary>
    public static string ManagedString(nint cfString)
    {
        var length = CFStringGetLength(cfString);
        var buffer = new char[length];
        fixed (char* b = buffer)
            CFStringGetCharacters(cfString, new CFRange { Location = 0, Length = length }, b);
        return new string(buffer);
    }

    /// <summary>How many items a CFArray holds.</summary>
    public static long ArrayCount(nint array) => CFArrayGetCount(array);

    public static void Release(nint cf) => CFRelease(cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CFRange
    {
        public nint Location;
        public nint Length;
    }

    [LibraryImport(Library)] private static partial nint CFStringCreateWithCharacters(nint allocator, char* characters, nint length);
    [LibraryImport(Library)] private static partial nint CFStringGetLength(nint text);
    [LibraryImport(Library)] private static partial void CFStringGetCharacters(nint text, CFRange range, char* buffer);
    [LibraryImport(Library)] private static partial nint CFArrayGetCount(nint array);
    [LibraryImport(Library)] private static partial void CFRelease(nint cf);
}
```

Replace `CoffeePdf/Platform/DefaultPdfApp.cs` with:
```csharp
using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Settings' Default PDF app (spec §5): whether CoffeePDF opens PDFs, and a way to make it.</summary>
internal interface IDefaultPdfApp
{
    /// <summary>Whether this platform has a default to read and set.</summary>
    bool IsSupported { get; }

    bool IsCoffeePdf();

    /// <summary>Makes CoffeePDF the default for PDFs. Returns whether Launch Services took it.</summary>
    bool MakeCoffeePdf();
}

/// <summary>The default PDF app through macOS's Launch Services, for every role, by CoffeePDF's bundle id.</summary>
internal sealed partial class MacDefaultPdfApp : IDefaultPdfApp
{
    public const string BundleId = "com.healdan.coffeepdf";

    private const string PdfType = "com.adobe.pdf";
    private const uint AllRoles = 0xFFFFFFFF;

    public bool IsSupported => OperatingSystem.IsMacOS();

    public bool IsCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CoreFoundation.CreateString(PdfType);
        try
        {
            var handler = LSCopyDefaultRoleHandlerForContentType(type, AllRoles);
            if (handler == 0)
                return false;
            try
            {
                return string.Equals(CoreFoundation.ManagedString(handler), BundleId, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                CoreFoundation.Release(handler);
            }
        }
        finally
        {
            CoreFoundation.Release(type);
        }
    }

    public bool MakeCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CoreFoundation.CreateString(PdfType);
        var bundle = CoreFoundation.CreateString(BundleId);
        try
        {
            return LSSetDefaultRoleHandlerForContentType(type, AllRoles, bundle) == 0;
        }
        finally
        {
            CoreFoundation.Release(bundle);
            CoreFoundation.Release(type);
        }
    }

    // Deprecated since macOS 12, and still the plain C call for it.
    [LibraryImport(CoreFoundation.CoreServices)] private static partial nint LSCopyDefaultRoleHandlerForContentType(nint contentType, uint role);
    [LibraryImport(CoreFoundation.CoreServices)] private static partial int LSSetDefaultRoleHandlerForContentType(nint contentType, uint role, nint handlerBundleId);
}
```

`CoffeePdf/Platform/HelperInstall.cs`:
```csharp
using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Whether the Healdan Helper is installed, for the first launch's question (spec §5).</summary>
internal interface IHelperInstall
{
    bool IsInstalled();
}

/// <summary>The Helper is installed when Launch Services knows an app with its bundle id, wherever it is.</summary>
internal sealed unsafe partial class MacHelperInstall(string bundleId = MacHelperInstall.BundleId) : IHelperInstall
{
    public const string BundleId = "com.healdan.helper";

    public bool IsInstalled()
    {
        if (!OperatingSystem.IsMacOS())
            return false;
        var id = CoreFoundation.CreateString(bundleId);
        try
        {
            var apps = LSCopyApplicationURLsForBundleIdentifier(id, null);
            if (apps == 0)
                return false;
            try
            {
                return CoreFoundation.ArrayCount(apps) > 0;
            }
            finally
            {
                CoreFoundation.Release(apps);
            }
        }
        finally
        {
            CoreFoundation.Release(id);
        }
    }

    [LibraryImport(CoreFoundation.CoreServices)] private static partial nint LSCopyApplicationURLsForBundleIdentifier(nint bundleId, nint* error);
}
```

In `CoffeePdf/CoffeePdf.csproj`, change the comment above `AllowUnsafeBlocks` to
`<!-- Platform/CoreFoundation.cs and HelperInstall.cs pass pointers to Core Foundation and Launch Services. -->`.

- [ ] **Step 5: Add the box to Settings**

In `CoffeePdf/Windows/SettingsWindow.axaml`, add between the Markup author's `StackPanel` and `DefaultAppSection`:
```xml
    <StackPanel Spacing="6">
      <TextBlock Text="Healdan" FontWeight="SemiBold" />
      <CheckBox x:Name="HealdanBox" Content="Use CoffeePDF with Healdan" />
      <TextBlock TextWrapping="Wrap" Opacity="0.7"
                 Text="Opens and saves documents in Healdan through Healdan Helper. Turning it off takes effect for each Healdan document as its window closes." />
    </StackPanel>
```

In `CoffeePdf/Windows/SettingsWindow.axaml.cs`:
- add `using Avalonia.Controls.Primitives;`;
- change the class's summary to `/// <summary>Settings (spec §5): the Markup author, Healdan support, and the Default PDF app. Each change is kept at once.</summary>`;
- in the second constructor, after the `AuthorBox.PropertyChanged` handler, add:
```csharp
        HealdanBox.IsChecked = store.HealdanEnabled;
        HealdanBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleButton.IsCheckedProperty)
                return;
            try
            {
                store.HealdanEnabled = HealdanBox.IsChecked == true; // the windows follow through HealdanEnabledChanged
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // As for the author: the box shows what was chosen, and the store keeps its last good value.
            }
        };
```

- [ ] **Step 6: Ask at the first launch**

In `CoffeePdf/App.axaml.cs`, add `using CoffeePdf.Dialogs;`, and these members after `MarkupAuthor`:
```csharp
    /// <summary>Whether the Healdan Helper is installed, for the first launch's question (spec §5).</summary>
    internal IHelperInstall HelperInstall { get; set; } = new MacHelperInstall();

    private bool _healdanQuestionDue = true; // the first window this run shows asks, if the question is still to ask

    /// <summary>
    /// At the first launch with the Helper installed, asks once over <paramref name="owner"/> whether to use CoffeePDF
    /// with Healdan (spec §5). Turning support on raises <see cref="SettingsStore.HealdanEnabledChanged"/>, which the
    /// windows follow.
    /// </summary>
    private async Task AskAboutHealdanAsync(Window owner)
    {
        try
        {
            await FirstLaunch.AskAboutHealdanAsync(Settings, HelperInstall.IsInstalled, async () =>
                await MessageDialog.ShowAsync(owner, "Use CoffeePDF with Healdan?",
                    "CoffeePDF can open and save documents in Healdan, through Healdan Helper. You can change this in Settings.",
                    ["Not Now", "Use with Healdan"]) == "Use with Healdan");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // settings.json can't be written: the question comes again at the next launch, and nothing else is lost.
        }
    }
```

and in `NewWindow`, before `return window;`:
```csharp
        if (_healdanQuestionDue)
        {
            _healdanQuestionDue = false;
            window.Opened += async (_, _) => await AskAboutHealdanAsync(window);
        }
```

In `CoffeePdf.Tests/Headless.cs`, replace `new SettingsStore(settings).MarkupAuthor = Windows.TestWindows.Author;` with:
```csharp
        var store = new SettingsStore(settings) { MarkupAuthor = Windows.TestWindows.Author };
        store.HealdanAsked = true; // so the first launch's question never shows over a test's window
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.sln --filter "FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~FirstLaunchTests|FullyQualifiedName~SettingsWindowTests|FullyQualifiedName~HelperInstallTests|FullyQualifiedName~DefaultPdfAppTests"`
Expected: PASS. `DefaultPdfAppTests` checks that the move to `CoreFoundation` changed nothing.

- [ ] **Step 8: Commit**

```bash
git add CoffeePdf.Core/Settings CoffeePdf.Core.Tests/Settings CoffeePdf/Platform CoffeePdf/CoffeePdf.csproj \
  CoffeePdf/Windows/SettingsWindow.axaml CoffeePdf/Windows/SettingsWindow.axaml.cs CoffeePdf/App.axaml.cs \
  CoffeePdf.Tests/Headless.cs CoffeePdf.Tests/Windows/SettingsWindowTests.cs CoffeePdf.Tests/Platform/HelperInstallTests.cs
git commit -m "Add Healdan support to Settings, and ask once at the first launch when the Helper is installed"
```

---

### Task 9: Opening a copy in its mode

**Files:**
- Modify: `CoffeePdf/App.axaml.cs`, `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Modify: `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, `CoffeePdf.Tests/Headless.cs`, `CoffeePdf.Tests/Windows/TestWindows.cs`
- Test: `CoffeePdf.Tests/Windows/HealdanOpenTests.cs`

**Interfaces:**
- Consumes: `HelperFolders`, `HealdanLink`, `HelperChannel.StateAsync` and the exceptions (Tasks 1 and 4); `CoffeeLog`
  (Task 2); `WindowDocument`, `WindowCommands`, `WindowBar` and `BarAction` (Task 5); `CopyLock` (Task 7);
  `SettingsStore.HealdanEnabledChanged` (Task 8); `FakeHelper` and `FakeHelper.Link` (Tasks 3 and 4).
- Produces:
  - `App.Log : CoffeeLog` and `App.Healdan : HealdanLink`.
  - `DocumentWindow.Healdan : HealdanLink`, internal and settable, which the tests set before `Load`.
  - `DocumentWindow.Document : WindowDocument?`, internal.
  - `DocumentWindow.AskHelperAsync() : Task`, internal: asks `/v1/state` and takes the answer's mode.
  - Private members that later tasks use: `Show(string, WindowDocument)`, `ApplyMode()`, `Allowed`,
    `TakeLockIfEditable()`, `AfterSave(string?)`, `TellAsync(string, string)`, and the fields `_document`, `_loads`,
    `_loaded` and `_lockHeld`.
  - The bar's controls, `Bar`, `BarText` and `BarButton`, and the window's panel, `DocumentPanel`.
  - `TestWindows.OpenAsync(string path, HealdanLink healdan)`, which waits until the window has its mode.

The window decides its mode at `Load` (§6.2). A file outside `Copies/` is local at once, and so is a copy with support
off. Any other copy is `Asking` until `/v1/state` answers, read-only meanwhile. Its lock file is written once the
PdfViewer has the file loaded and the answer makes it editable (§6.8, §4.4 item 8). A Helper-away copy asks again
each time its window comes forward (question 6).

- [ ] **Step 1: Point the tests at a fake Helper, and never at the real one**

In `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, add to the `ItemGroup` that links the core's fixtures:
```xml
    <Compile Include="../CoffeePdf.Core.Tests/Fakes/FakeHelper.cs" Link="Fakes/FakeHelper.cs" />
```

In `CoffeePdf.Tests/Headless.cs`, add `using CoffeePdf.Core.Helper;` and `using CoffeePdf.Core.Logging;`, and after the
line that sets `SettingsLocation.OverrideVariable`, add:
```csharp
        // The Helper's folders and the log go to folders of the run's own too. The real Helper, which may be running on
        // the Mac the tests run on, is never reached, and an app window starts with the Helper away. The flows' tests
        // give each window a fake Helper's link instead (FakeHelper.Link).
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, Directory.CreateTempSubdirectory("coffeepdf-helper-").FullName);
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, Directory.CreateTempSubdirectory("coffeepdf-log-").FullName);
```

In `CoffeePdf.Tests/Windows/TestWindows.cs`, add `using CoffeePdf.Core.Documents;` and `using CoffeePdf.Core.Helper;`,
and after the first `OpenAsync`:
```csharp
    /// <summary>
    /// Shows a DocumentWindow with <paramref name="path"/> loaded through <paramref name="healdan"/>, such as a fake
    /// Helper's (FakeHelper.Link), collecting the errors it would show, and waits until the window has its mode.
    /// </summary>
    public static async Task<(DocumentWindow Window, List<string> Errors)> OpenAsync(string path, HealdanLink healdan)
    {
        var (window, errors) = Show();
        window.Healdan = healdan;
        await Viewers.ShowAndLoadAsync(window.Viewer, () => window.Load(path));
        await WaitForAsync(() => window.Document?.Mode != DocumentMode.Asking);
        return (window, errors);
    }
```

- [ ] **Step 2: Write the failing tests**

`CoffeePdf.Tests/Windows/HealdanOpenTests.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.1 and §6.2: a Healdan copy opens in its mode, against the fake Helper.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanOpenTests : IDisposable
{
    private readonly FakeHelper _helper = new();

    public void Dispose() => _helper.Dispose();

    [PdfViewerFact]
    public async Task An_editable_copy_shows_its_doc_ref_and_keeps_its_lock_file()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (mode, title, readOnly, bar, locked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            var state = (window.Document?.Mode, window.Title, window.Viewer.IsReadOnly, window.Bar.IsVisible, File.Exists(CopyLock.PathFor(copy)));
            window.Close();
            return state;
        });

        Assert.Equal(DocumentMode.Editable, mode);
        Assert.Equal("Settlement Agreement — #1234 v2.1", title);
        Assert.False(readOnly);
        Assert.False(bar);
        Assert.True(locked);
        Assert.Equal(new[] { copy }, _helper.CallsTo(ChannelRoutes.State).Select(b => (string?)b["path"]));
    }

    [PdfViewerFact]
    public async Task A_copy_held_elsewhere_opens_read_only_with_a_bar_naming_who_holds_it()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");

        var (mode, title, readOnly, bar, locked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            var state = (window.Document?.Mode, window.Title, window.Viewer.IsReadOnly, window.BarText.Text, File.Exists(CopyLock.PathFor(copy)));
            window.Close();
            return state;
        });

        Assert.Equal(DocumentMode.ReadOnly, mode);
        Assert.Equal("Settlement Agreement — #1234 v2.1", title);
        Assert.True(readOnly);
        Assert.Equal("Read-only — checked out by Zoë Q. Reviewer", bar);
        Assert.False(locked);
    }

    [PdfViewerFact]
    public async Task With_the_Helper_away_a_copy_takes_its_title_from_its_name_and_says_so()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.GoAway();

        var (mode, title, readOnly, bar, locked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            var state = (window.Document?.Mode, window.Title, window.Viewer.IsReadOnly, window.BarText.Text, File.Exists(CopyLock.PathFor(copy)));
            window.Close();
            return state;
        });

        Assert.Equal(DocumentMode.HelperAway, mode);
        Assert.Equal("Settlement Agreement — #1234 v2.1", title);
        Assert.False(readOnly);
        Assert.Equal(WindowBar.HelperAway, bar);
        Assert.True(locked);
    }

    [PdfViewerFact]
    public async Task The_Helper_away_bar_goes_once_the_Helper_answers_again()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.GoAway();

        var (mode, bar) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            _helper.ComeBack();
            await window.AskHelperAsync(); // as the window does each time it comes forward
            var state = (window.Document?.Mode, window.Bar.IsVisible);
            window.Close();
            return state;
        });

        Assert.Equal(DocumentMode.Editable, mode);
        Assert.False(bar);
    }

    [PdfViewerFact]
    public async Task With_support_off_a_copy_opens_read_only_and_its_bar_turns_support_on()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var link = _helper.Link(enabled: false);

        var (before, readOnly, button, calls, after, enabled) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, link);
            var mode = window.Document?.Mode;
            var wasReadOnly = window.Viewer.IsReadOnly;
            var label = window.BarButton.Content as string;
            var asked = _helper.Calls.Count;
            window.BarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await TestWindows.WaitForAsync(() => window.Document?.Mode == DocumentMode.Editable && File.Exists(CopyLock.PathFor(copy)));
            var now = window.Document?.Mode;
            window.Close();
            return (mode, wasReadOnly, label, asked, now, link.Enabled);
        });

        Assert.Equal(DocumentMode.SupportOff, before);
        Assert.True(readOnly);
        Assert.Equal("Turn On Healdan Support", button);
        Assert.Equal(0, calls);
        Assert.Equal(DocumentMode.Editable, after);
        Assert.True(enabled);
    }

    [PdfViewerFact]
    public async Task A_local_file_asks_the_Helper_nothing()
    {
        var dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;
        var path = Path.Combine(dir, "Report.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        try
        {
            var (mode, title) = await Headless.RunAsync(async () =>
            {
                var (window, _) = await TestWindows.OpenAsync(path, _helper.Link());
                var state = (window.Document?.Mode, window.Title);
                window.Close();
                return state;
            });

            Assert.Equal(DocumentMode.Local, mode);
            Assert.Equal("Report.pdf", title);
            Assert.Empty(_helper.Calls);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [PdfViewerFact]
    public async Task Healdans_copies_stay_out_of_Open_Recent()
    {
        var (copy, recent) = await Headless.RunAsync(async () =>
        {
            // The app's own link: the run's Helper folder (Headless), with Healdan support off.
            var copies = App.Current.Healdan.Folders.Copies;
            Directory.CreateDirectory(copies);
            var path = Path.Combine(copies, $"Recent Check {Guid.NewGuid():N} #1 v1.pdf");
            File.WriteAllBytes(path, MinimalPdf.Build());
            App.Current.OpenFile(path);
            var window = App.Current.Windows.First(w => w.FilePath == path);
            await TestWindows.WaitForAsync(() => window.Viewer.HasDocument);
            var paths = App.Current.Recent.Paths;
            window.Close();
            return (path, paths);
        });

        Assert.DoesNotContain(copy, recent);
    }
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~HealdanOpenTests`
Expected: the build fails. `DocumentWindow.Healdan`, `Document` and the bar don't exist yet. Without the key the build
gets that far too, since only running the tests needs the key.

- [ ] **Step 4: Give the app its log and its link to Healdan**

In `CoffeePdf/App.axaml.cs`, add `using CoffeePdf.Core.Helper;` and `using CoffeePdf.Core.Logging;`, and after the
`Recent` property:
```csharp
    /// <summary>CoffeePDF's log (spec §6.9).</summary>
    public CoffeeLog Log { get; private set; } = null!;

    /// <summary>Healdan support, the Helper's folders and its channel (spec §5, §9). A test's window gets a fake Helper's instead.</summary>
    public HealdanLink Healdan { get; private set; } = null!;
```

In `Initialize`, after `Recent = …`:
```csharp
        Log = new CoffeeLog(CoffeeLog.Folder());
        Healdan = new HealdanLink(Settings, HelperFolders.ForThisUser(), Log);
```

In `OpenFile`, replace `Recent.Add(path);` with:
```csharp
        if (!Healdan.Folders.HoldsCopy(path))
            Recent.Add(path); // Healdan's copies stay out (spec §6.10): the Helper deletes each once it's checked in
```

- [ ] **Step 5: Add the bar**

In `CoffeePdf/Windows/DocumentWindow.axaml`, wrap the PdfViewer and its comment in a `DockPanel`, with the bar first:
```xml
  <DockPanel x:Name="DocumentPanel">
    <!-- A Healdan copy's bar (spec §6.1, §6.9): read-only, the Helper away, or Healdan support off. -->
    <Border x:Name="Bar" DockPanel.Dock="Top" IsVisible="False" Padding="12,8" Background="#26808080">
      <Grid ColumnDefinitions="*,Auto">
        <TextBlock x:Name="BarText" VerticalAlignment="Center" TextWrapping="Wrap" />
        <Button x:Name="BarButton" Grid.Column="1" Margin="12,0,0,0" IsVisible="False" Click="OnBarButton" />
      </Grid>
    </Border>
    <!-- Every save goes through … (the PdfViewer's comment, unchanged) -->
    <pdf:PdfViewer x:Name="Viewer" … />
  </DockPanel>
```
The PdfViewer's element and comment stay exactly as they are; only the `DockPanel` and the `Border` are new. The
grey is translucent, so it reads in light and dark appearance alike, and the owner's checks look at it.

- [ ] **Step 6: Decide the mode at Load**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, add `using Avalonia.Interactivity;`, `using CoffeePdf.Core.Helper;` and
`using CoffeePdf.Core.Locks;`.

Add to the class's remarks, after the paragraph on saving:
```csharp
/// <para>
/// A Healdan copy opens in its mode (spec §6.1, §6.2), which <see cref="WindowDocument"/> decides from the Helper's
/// answers; the window shows it, keeps the copy's lock file while it may edit it (§6.8), and asks the Helper again as
/// it answers or comes back.
/// </para>
```
and wrap the existing remark's text in `<para>` too, so the two read as paragraphs.

Add these fields after `_typedPassword`:
```csharp
    private const string TurnOnHealdanLabel = "Turn On Healdan Support";
    private HealdanLink _healdan = App.Current.Healdan;
    private WindowDocument? _document; // the file's mode and what the Helper last said of it (spec §6.1)
    private int _loads;                // counts the files shown, so an answer about an earlier one is dropped
    private bool _loaded;              // the PdfViewer has the file loaded
    private bool _lockHeld;            // this window wrote the copy's lock file (spec §6.8)
    private bool _askingHelper;        // a /v1/state for the file is on its way
    private DateTimeOffset _openedAt;  // when the file was shown, for the lock file
```

At the end of the constructor, add:
```csharp
        Viewer.DocumentLoaded += (_, _) =>
        {
            _loaded = true;
            TakeLockIfEditable();
        };
        // The Helper-away bar goes once the Helper answers again (spec §6.9): it's asked each time the window comes forward.
        Activated += (_, _) =>
        {
            if (_document?.Mode == DocumentMode.HelperAway)
                _ = AskHelperAsync();
        };
        _healdan.Settings.HealdanEnabledChanged += OnHealdanSupportChanged;
        Closed += (_, _) => _healdan.Settings.HealdanEnabledChanged -= OnHealdanSupportChanged;
        ApplyMode();
```

Add after `FilePath`:
```csharp
    /// <summary>Healdan support, the Helper's folders and its channel. The tests set a fake Helper's before Load.</summary>
    internal HealdanLink Healdan
    {
        get => _healdan;
        set
        {
            _healdan.Settings.HealdanEnabledChanged -= OnHealdanSupportChanged;
            _healdan = value;
            _healdan.Settings.HealdanEnabledChanged += OnHealdanSupportChanged;
            ApplyMode();
        }
    }

    /// <summary>The file's mode and what the Helper last said of it, or null while the window is empty.</summary>
    internal WindowDocument? Document => _document;
```

Replace `Load` with:
```csharp
    /// <summary>Shows <paramref name="path"/> in an empty window, in its mode (spec §6.2). A copy asks the Helper what it is.</summary>
    public void Load(string path)
    {
        Show(path, WindowDocument.Opened(path, Healdan.Enabled, Healdan.Folders.HoldsCopy(path)));
        if (_document!.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }

    /// <summary>Shows <paramref name="path"/>, starting afresh, in <paramref name="document"/>'s mode.</summary>
    private void Show(string path, WindowDocument document)
    {
        _path = Path.GetFullPath(path);
        _markedPages.Clear();
        _saved = _editedSinceSave = _saveFailed = false;
        _typedPassword = null;
        _loads++;
        _loaded = false;
        _openedAt = DateTimeOffset.Now;
        _document = document;
        Viewer.Source = _path;
        ApplyMode();
        MenuItem("Undo").IsEnabled = MenuItem("Redo").IsEnabled = false;
    }
```

Replace `SaveAsync` with:
```csharp
    /// <summary>Saves to the window's file (⌘S), where its mode allows it. Returns whether it saved; a failure has been shown.</summary>
    public Task<bool> SaveAsync() => _path is null || !Allowed.Save ? Task.FromResult(false) : SaveToAsync(_path);
```

In `SaveToAsync`:
- replace `string? movedTo = null;` with the two lines `string? previous = null;` and `string? movedTo = null;`;
- replace `var previous = _path;` with `previous = _path;`;
- replace `if (movedTo is not null)` (the one before `App.Current.Recent.Add(movedTo)`) with:
```csharp
            // Healdan's copies stay out of Open Recent (spec §6.10).
            if (movedTo is not null && !Healdan.Folders.HoldsCopy(movedTo))
```
- and in the same `if (failure is null)` block, add `AfterSave(previous);` just before its `return true;`.

Replace `UpdateTitle` with:
```csharp
    private void UpdateTitle() => Title = _document?.Title(HasUnsavedChanges, saving: false) ?? DocumentTitle.For(_path, HasUnsavedChanges);
```

Add these members after `ShowRecent`:
```csharp
    /// <summary>
    /// Asks the Helper what the window's file is (<c>/v1/state</c>, spec §6.2), and takes the mode its answer gives. A
    /// Helper that doesn't answer, or answers with an error, makes a copy the Helper-away one until it answers (§6.9).
    /// </summary>
    internal async Task AskHelperAsync()
    {
        if (_askingHelper || _path is not { } path || _document is null)
            return;
        _askingHelper = true;
        var load = _loads;
        CopyState? state;
        try
        {
            state = await Healdan.Channel.StateAsync(path);
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            state = null;
        }
        finally
        {
            _askingHelper = false;
        }
        if (load != _loads || _document is null)
            return; // the window has moved on to another file meanwhile
        _document = _document.Answered(state);
        ApplyMode();
        TakeLockIfEditable();
    }

    /// <summary>
    /// Writes the copy's lock file once the PdfViewer has the file loaded and the window may edit it (spec §6.8, §4.4
    /// item 8). One that can't be written is logged: the Helper then sees no lock, which its close watch handles.
    /// </summary>
    private void TakeLockIfEditable()
    {
        if (_lockHeld || !_loaded || _path is null || _document?.HoldsLock != true)
            return;
        try
        {
            CopyLock.Write(_path, Environment.ProcessId, _openedAt);
            _lockHeld = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Healdan.Log.Warn($"The lock file couldn’t be written ({e.GetType().Name})");
        }
    }

    /// <summary>After a save that worked: one that moved the window to another file takes that file's mode (spec §6.1).</summary>
    private void AfterSave(string? previous)
    {
        if (_path == previous)
            return;
        _loads++; // an answer about the file the window left is dropped
        _document = WindowDocument.Opened(_path!, Healdan.Enabled, Healdan.Folders.HoldsCopy(_path!));
        ApplyMode();
        if (_document.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }

    /// <summary>What the window allows in its mode (spec §6.1).</summary>
    private WindowCommands Allowed => _document?.Commands(Healdan.Enabled) ?? WindowCommands.Empty(Healdan.Enabled);

    /// <summary>Shows the window's mode (spec §6.1): its title and bar, and whether it may be marked up and saved.</summary>
    private void ApplyMode()
    {
        var allowed = Allowed;
        Viewer.IsReadOnly = !allowed.Markup;
        MenuItem("Save").IsEnabled = allowed.Save;
        ShowBar(_document?.Bar);
        UpdateTitle();
    }

    private void ShowBar(WindowBar? bar)
    {
        Bar.IsVisible = bar is not null;
        BarText.Text = bar?.Text;
        // Only Turn On Healdan Support has its button so far; Save as New Document's comes with its flow (Task 12).
        BarButton.IsVisible = bar?.Action == BarAction.TurnOnHealdan;
        BarButton.Content = bar?.Action == BarAction.TurnOnHealdan ? TurnOnHealdanLabel : null;
    }

    private void OnBarButton(object? sender, RoutedEventArgs e)
    {
        if (_document?.Bar?.Action == BarAction.TurnOnHealdan)
            TurnOnHealdan();
    }

    /// <summary>The bar's Turn On Healdan Support: every window follows, through HealdanEnabledChanged.</summary>
    private void TurnOnHealdan()
    {
        try
        {
            Healdan.Settings.HealdanEnabled = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ = TellAsync("Healdan support couldn’t be turned on.", e.Message);
        }
    }

    /// <summary>
    /// Healdan support changed. Turning it on takes effect at once, and a copy opened with it off asks the Helper what
    /// it is. Turning it off takes effect for a copy as its window closes (spec §5), and for a local window at once.
    /// </summary>
    private void OnHealdanSupportChanged(object? sender, EventArgs e)
    {
        if (_document is { Mode: DocumentMode.SupportOff } && Healdan.Enabled)
        {
            _document = WindowDocument.Opened(_document.Path, healdan: true, inCopies: true);
            _ = AskHelperAsync();
        }
        ApplyMode();
    }

    /// <summary>Shows a problem over the window. It never throws: the window may have closed, with nothing to show it over.</summary>
    private async Task TellAsync(string heading, string detail)
    {
        try
        {
            await ReportError(heading, detail);
        }
        catch (Exception)
        {
            // Closed meanwhile: there's nothing to show the problem over, and nothing more to do about it.
        }
    }
```

`_document!` in `Load` is safe, since `Show` has just set it.

- [ ] **Step 7: Run the tests to see them pass**

```bash
eval "$(grep -E '^[[:space:]]*export[[:space:]]+AVALONIA_LICENSE_KEY=' ~/.zshrc | tail -1)"
dotnet test CoffeePdf.Tests --filter FullyQualifiedName~HealdanOpenTests
```
Expected: PASS. Then run all of `CoffeePdf.Tests` with the key: Phase 1's tests still pass, since a local file's mode
changes nothing they see.

- [ ] **Step 8: Commit**

```bash
git add CoffeePdf/App.axaml.cs CoffeePdf/Windows/DocumentWindow.axaml CoffeePdf/Windows/DocumentWindow.axaml.cs \
  CoffeePdf.Tests/CoffeePdf.Tests.csproj CoffeePdf.Tests/Headless.cs CoffeePdf.Tests/Windows/TestWindows.cs \
  CoffeePdf.Tests/Windows/HealdanOpenTests.cs
git commit -m "Open a Healdan copy in its mode, with its title, bar and lock file"
```

---

### Task 10: Closing a copy, leaving it, and quitting

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml.cs`, `CoffeePdf/App.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/HealdanCloseTests.cs`

**Interfaces:**
- Consumes: Task 9's window members; `HelperChannel.ClosedAsync` (Task 4); `CopyLock.TryRemove` (Task 7).
- Produces:
  - `DocumentWindow.Released : Task`, internal: done once the closed window has let its copy go, and at once for a
    window without one.
  - `DocumentWindow.HoldsCopy : bool`, internal.
  - `DocumentWindow.LetGoAsync(string? path, WindowDocument? document, bool lockHeld) : Task`, private, which never throws.
  - `App.CloseAndLetGoAsync(IReadOnlyList<DocumentWindow> windows) : Task`, internal static.

Letting a copy go (§6.8) removes its lock file, then calls `/v1/closed`. It happens when the window closes, and when a
save moves the window to another file (question 8). Quitting closes every copy's window and waits until the Helper
has heard of each, so CoffeePDF doesn't end with a `/v1/closed` still on its way.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/HealdanCloseTests.cs`:
```csharp
using System.Text.Json.Nodes;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.8: a copy's window lets the copy go when it closes or moves, and quitting waits for that.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanCloseTests : IDisposable
{
    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.Close();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private IEnumerable<string?> ClosedPaths => _helper.CallsTo(ChannelRoutes.Closed).Select(b => (string?)b["path"]);

    [PdfViewerFact]
    public async Task Closing_a_copy_removes_its_lock_then_tells_the_Helper()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        bool? lockedWhenTold = null;
        _helper.Answer(ChannelRoutes.Closed, _ =>
        {
            lockedWhenTold = File.Exists(CopyLock.PathFor(copy));
            return Task.FromResult<JsonNode>(new JsonObject());
        });

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await CloseAsync(window);
            return true;
        });

        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.False(lockedWhenTold);
        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task Closing_with_changes_asks_saves_and_then_lets_the_copy_go()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            window.AskToSave = () => Task.FromResult(SaveChoice.Save);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.CloseWithPromptAsync();
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return true;
        });

        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
    }

    [PdfViewerFact]
    public async Task A_read_only_copy_is_let_go_too()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true);

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await CloseAsync(window);
            return true;
        });

        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task With_support_off_the_Helper_hears_nothing()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link(enabled: false));
            await CloseAsync(window);
            return true;
        });

        Assert.Empty(_helper.Calls);
    }

    [PdfViewerFact]
    public async Task Saving_a_copy_as_a_local_file_lets_the_copy_go()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var local = Path.Combine(_dir, "Kept here.pdf");

        var (mode, title, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveToAsync(local);
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Closed).Count == 1);
            var state = (window.Document?.Mode, window.Title, window.FilePath);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(DocumentMode.Local, mode);
        Assert.Equal("Kept here.pdf", title);
        Assert.Equal(local, shown);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }

    [PdfViewerFact]
    public async Task Quitting_closes_every_copy_and_waits_until_the_Helper_has_heard()
    {
        string first = _helper.AddCopy("First", 1, "1"), second = _helper.AddCopy("Second", 2, "1");

        var (held, told) = await Headless.RunAsync(async () =>
        {
            var (a, _) = await TestWindows.OpenAsync(first, _helper.Link());
            var (b, _) = await TestWindows.OpenAsync(second, _helper.Link());
            var holding = a.HoldsCopy && b.HoldsCopy;
            await App.CloseAndLetGoAsync([a, b]);
            return (holding, ClosedPaths.ToList());
        });

        Assert.True(held);
        Assert.Equal(new[] { first, second }.Order(), told.Order());
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~HealdanCloseTests`
Expected: the build fails. `Released`, `HoldsCopy` and `App.CloseAndLetGoAsync` don't exist yet.

- [ ] **Step 3: Let the copy go**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, add after the fields Task 9 added:
```csharp
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
```

At the end of the constructor, add:
```csharp
        Closed += async (_, _) =>
        {
            var (path, document, lockHeld) = (_path, _document, _lockHeld);
            _lockHeld = false;
            await LetGoAsync(path, document, lockHeld);
            _released.TrySetResult();
        };
```

Add after `Document`:
```csharp
    /// <summary>Done once the closed window has let its copy go (spec §6.8), and at once for a window without one.</summary>
    internal Task Released => _released.Task;

    /// <summary>Whether the window has a copy to let go when it closes.</summary>
    internal bool HoldsCopy => _lockHeld || _document?.TellsHelper == true;
```

Replace `AfterSave` with:
```csharp
    /// <summary>
    /// After a save that worked: one that moved the window to another file lets the copy it left go, as a close would
    /// (spec §6.6 step 3, §6.8), and takes the new file's mode (§6.1).
    /// </summary>
    private void AfterSave(string? previous)
    {
        if (_path == previous)
            return;
        var (document, lockHeld) = (_document, _lockHeld);
        _lockHeld = false;
        _ = LetGoAsync(previous, document, lockHeld);
        _loads++; // an answer about the file the window left is dropped
        _document = WindowDocument.Opened(_path!, Healdan.Enabled, Healdan.Folders.HoldsCopy(_path!));
        ApplyMode();
        if (_document.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }
```

and add after it:
```csharp
    /// <summary>
    /// Lets a copy go (spec §6.8): removes its lock file, if the window wrote one, then tells the Helper with
    /// <c>/v1/closed</c>, so it sends anything unsent, checks the document in and deletes the copy. It never throws: the
    /// Helper watches the lock file too, and its next start closes a copy it wasn't told of (the Desktop Clients page).
    /// </summary>
    private async Task LetGoAsync(string? path, WindowDocument? document, bool lockHeld)
    {
        if (path is null)
            return;
        if (lockHeld)
            CopyLock.TryRemove(path);
        if (document?.TellsHelper != true)
            return;
        try
        {
            await Healdan.Channel.ClosedAsync(path);
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            Healdan.Log.Info($"Healdan Helper wasn’t told a copy closed ({e.GetType().Name}); it watches the lock file too");
        }
    }
```

- [ ] **Step 4: Quit only once the Helper has heard**

In `CoffeePdf/App.axaml.cs`, replace `OnShutdownRequested` and `QuitAsync` with:
```csharp
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Windows with changes ask first, and every copy is let go before CoffeePDF ends (spec §6.8).
        if (_quitConfirmed || !_windows.Any(w => w.HasUnsavedChanges || w.HoldsCopy))
            return;
        e.Cancel = true;
        _ = QuitAsync(sender as IClassicDesktopStyleApplicationLifetime);
    }

    private async Task QuitAsync(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        if (!await CloseAllEditedAsync(() => _windows))
            return;
        // No window still open has changes, and nothing awaits before they're closed, so none can be edited in between.
        await CloseAndLetGoAsync([.. _windows]);
        _quitConfirmed = true;
        desktop?.Shutdown();
    }

    /// <summary>
    /// Closes <paramref name="windows"/>, which have no unsaved changes, and waits until each has let its copy go, so
    /// the Helper hears of each before CoffeePDF ends (spec §6.8). Each <c>/v1/closed</c> waits 10 seconds at most.
    /// </summary>
    internal static async Task CloseAndLetGoAsync(IReadOnlyList<DocumentWindow> windows)
    {
        foreach (var window in windows)
            window.Close();
        await Task.WhenAll(windows.Select(w => w.Released));
    }
```

- [ ] **Step 5: Run the tests to see them pass**

Run, with the key: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~HealdanCloseTests|FullyQualifiedName~ClosingTests"`
Expected: PASS. Phase 1's `ClosingTests` show the prompts are unchanged.

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf/App.axaml.cs CoffeePdf.Tests/Windows/HealdanCloseTests.cs
git commit -m "Let a copy go when its window closes or moves, and quit only once the Helper has heard"
```

---

### Task 11: Saving a copy, following it, and Save as New Version

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/HealdanSaveTests.cs`

**Interfaces:**
- Consumes: Tasks 9 and 10's window members; `SaveFollower.FollowAsync` (Task 6); `HelperChannel.SavedAsync` and
  `NewVersionAsync` (Task 4).
- Produces:
  - `DocumentWindow.Pause : Func<TimeSpan, CancellationToken, Task>`, internal and settable, `Task.Delay` by default.
  - `DocumentWindow.SaveAsNewVersionAsync() : Task<bool>`, internal.
  - `DocumentWindow.OnHelperAway()`, private, which Tasks 12 and 13 call when the Helper doesn't answer.

A save of an editable copy, or of one while the Helper is away, is followed with `/v1/saved` (§6.4). Each answer moves
the title on. While a save is on its way, the title says so (question 10), and a read-only answer is a refused save
(§6.9). Following ends when the window closes, a newer save takes over, or the window is saved elsewhere.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/HealdanSaveTests.cs`:
```csharp
using System.Text.Json.Nodes;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.4, §6.5 and §6.9: a copy's save, followed to Healdan, and Save as New Version.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanSaveTests : IDisposable
{
    private readonly FakeHelper _helper = new();

    public void Dispose() => _helper.Dispose();

    /// <summary>The copy's state with Healdan at <paramref name="version"/>.</summary>
    private CopyState Version(string copy, string version) =>
        _helper.StateOf(copy) with { Title = $"Settlement Agreement #1234 v{version}", DocRef = $"#1234 v{version}" };

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PdfViewerFact]
    public async Task A_copys_save_is_followed_until_Healdan_has_it()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        _helper.Answer(ChannelRoutes.Saved, async _ =>
        {
            if (Interlocked.Increment(ref asked) == 1)
            {
                await held.Task; // the first answer waits, so the test sees the save on its way
                return FakeHelper.Json(_helper.StateOf(copy) with { Pending = true });
            }
            return FakeHelper.Json(Version(copy, "2.2"));
        });

        var (during, after, pauses) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            var taken = new List<TimeSpan>();
            window.Pause = (pause, _) =>
            {
                taken.Add(pause);
                return Task.CompletedTask;
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            var onItsWay = window.Title;
            held.SetResult();
            await TestWindows.WaitForAsync(() => window.Title == "Settlement Agreement — #1234 v2.2");
            var done = window.Title;
            await CloseAsync(window);
            return (onItsWay, done, taken);
        });

        Assert.Equal("Settlement Agreement — #1234 v2.1 — Saving to Healdan…", during);
        Assert.Equal("Settlement Agreement — #1234 v2.2", after);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, pauses);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_refused_save_makes_the_copy_read_only_and_offers_Save_as_New_Document()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        // As spec §6.9 has it: the Helper ends the check-out, and the copy's state says read-only (Phase 2's plan, question 3).
        _helper.Answer(ChannelRoutes.Saved, _ => Task.FromResult<JsonNode>(FakeHelper.Json(_helper.StateOf(copy) with { ReadOnly = true })));

        var (mode, readOnly, bar, action) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await TestWindows.WaitForAsync(() => window.Document?.Mode == DocumentMode.ReadOnly);
            var state = (window.Document?.Mode, window.Viewer.IsReadOnly, window.BarText.Text, window.Document?.Bar?.Action);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(DocumentMode.ReadOnly, mode);
        Assert.True(readOnly);
        Assert.Equal(WindowBar.Refused, bar);
        Assert.Equal(BarAction.SaveAsNewDocument, action);
    }

    [PdfViewerFact]
    public async Task With_the_Helper_away_a_save_stays_on_this_Mac_and_goes_once_it_answers()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (barWhileAway, pauses, title, barAfter, notes) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            _helper.GoAway();
            var taken = new List<TimeSpan>();
            string? shown = null;
            window.Pause = (pause, _) =>
            {
                taken.Add(pause);
                if (taken.Count == 2)
                {
                    shown = window.BarText.Text;
                    _helper.SetState(Version(copy, "2.2")); // the Helper, back, has sent the save
                    _helper.ComeBack();
                }
                return Task.CompletedTask;
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await TestWindows.WaitForAsync(() => window.Title == "Settlement Agreement — #1234 v2.2");
            var state = (shown, taken, window.Title, window.Bar.IsVisible, PdfAnnotations.Read(File.ReadAllBytes(copy), 0).Count);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(WindowBar.HelperAway, barWhileAway);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, pauses);
        Assert.Equal("Settlement Agreement — #1234 v2.2", title);
        Assert.False(barAfter);
        Assert.Equal(1, notes);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_asks_for_a_version_then_saves()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (saved, routes) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveAsNewVersionAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Saved).Count == 1);
            await CloseAsync(window);
            return (result, _helper.Routes);
        });

        Assert.True(saved);
        Assert.Equal(new[] { "state", "new-version", "saved", "closed" }, routes);
        Assert.Equal(new[] { copy }, _helper.CallsTo(ChannelRoutes.NewVersion).Select(b => (string?)b["path"]));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_with_the_Helper_away_says_so_and_keeps_the_changes()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (saved, errors, unsaved, mode) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            _helper.GoAway();
            var result = await window.SaveAsNewVersionAsync();
            var state = (result, errorList.ToList(), window.HasUnsavedChanges, window.Document?.Mode);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.Equal("“Settlement Agreement” couldn’t be saved as a new version. Healdan Helper isn’t running. Try again once it is.", Assert.Single(errors));
        Assert.True(unsaved);
        Assert.Equal(DocumentMode.HelperAway, mode);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~HealdanSaveTests`
Expected: the build fails. `Pause` and `SaveAsNewVersionAsync` don't exist yet.

- [ ] **Step 3: Follow each save of a copy**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, add after the fields Task 9 added:
```csharp
    private bool _savingToHealdan;                // a save is being followed, and Healdan doesn't have it yet (spec §6.4)
    private CancellationTokenSource? _following;  // stops the save being followed
```

Add after `WriteFile`:
```csharp
    /// <summary>The pause between questions while a save is followed. The tests replace it, so none waits for the real pauses.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Pause { get; set; } = Task.Delay;
```

In the constructor, change the `Closed` handler Task 10 added so it stops following first:
```csharp
        Closed += async (_, _) =>
        {
            StopFollowing(); // following ends when the window closes (spec §6.4)
            var (path, document, lockHeld) = (_path, _document, _lockHeld);
            _lockHeld = false;
            await LetGoAsync(path, document, lockHeld);
            _released.TrySetResult();
        };
```

Replace `AfterSave` with:
```csharp
    /// <summary>
    /// After a save that worked: a copy's save is followed to Healdan (spec §6.4). One that moved the window to another
    /// file ends the following, lets the copy it left go, as a close would (§6.6 step 3, §6.8), and takes the new
    /// file's mode (§6.1).
    /// </summary>
    private void AfterSave(string? previous)
    {
        if (_path == previous)
        {
            if (_document?.FollowsSaves == true)
                Follow();
            return;
        }
        StopFollowing();
        var (document, lockHeld) = (_document, _lockHeld);
        _lockHeld = false;
        _ = LetGoAsync(previous, document, lockHeld);
        _loads++; // an answer about the file the window left is dropped
        _document = WindowDocument.Opened(_path!, Healdan.Enabled, Healdan.Folders.HoldsCopy(_path!));
        ApplyMode();
        if (_document.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }
```

Replace `UpdateTitle` with:
```csharp
    private void UpdateTitle() => Title = _document?.Title(HasUnsavedChanges, _savingToHealdan) ?? DocumentTitle.For(_path, HasUnsavedChanges);
```

Add after `AfterSave`:
```csharp
    /// <summary>Follows the save just written to the copy until Healdan has it (spec §6.4). A newer save takes over.</summary>
    private void Follow()
    {
        StopFollowing();
        var following = _following = new CancellationTokenSource();
        _savingToHealdan = true; // from the write on: the first answer can take 120 seconds
        UpdateTitle();
        _ = FollowAsync(_path!, following);
    }

    private async Task FollowAsync(string path, CancellationTokenSource following)
    {
        await SaveFollower.FollowAsync(stop => Healdan.Channel.SavedAsync(path, stop), state => OnSavedAnswer(path, state), OnHelperAway,
            Pause, following.Token);
        if (_following != following)
            return; // a newer save, a close or a move has taken over
        _following = null;
        _savingToHealdan = false;
        UpdateTitle();
    }

    private void StopFollowing()
    {
        _following?.Cancel();
        _following = null;
        _savingToHealdan = false;
    }

    /// <summary>An answer to <c>/v1/saved</c>: its version moves the title on, and a read-only one is a refused save (spec §6.9).</summary>
    private void OnSavedAnswer(string path, CopyState state)
    {
        if (_document is null || _path != path)
            return;
        _document = _document.Answered(state);
        _savingToHealdan = state.Known && state.Pending;
        ApplyMode();
    }

    /// <summary>The Helper didn't answer: a copy's saves stay on this Mac until it's back, and its bar says so (spec §6.9).</summary>
    private void OnHelperAway()
    {
        if (_document is null)
            return;
        _document = _document.Answered(null);
        ApplyMode();
    }

    /// <summary>
    /// Save as New Version (spec §6.5): the Helper sends the copy's next save as a version, and the changes are saved.
    /// With nothing to save, the save already written is followed, and the Helper sends the version with the next.
    /// With the Helper away, the version can't be asked for, so nothing is saved, and the user is told (Phase 2's plan,
    /// question 9).
    /// </summary>
    internal async Task<bool> SaveAsNewVersionAsync()
    {
        if (_document is not { Mode: DocumentMode.Editable } document || _path is not { } path)
            return false;
        try
        {
            await Healdan.Channel.NewVersionAsync(path);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            await TellAsync($"“{document.NameForHealdan}” couldn’t be saved as a new version.", "Healdan Helper isn’t running. Try again once it is.");
            return false;
        }
        catch (HelperErrorException e)
        {
            await TellAsync($"“{document.NameForHealdan}” couldn’t be saved as a new version.", e.Message);
            return false;
        }
        if (HasUnsavedChanges)
            return await SaveAsync();
        Follow();
        return true;
    }
```

- [ ] **Step 4: Run the tests to see them pass**

Run, with the key: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~HealdanSaveTests|FullyQualifiedName~HealdanCloseTests|FullyQualifiedName~DocumentWindowSaveTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf.Tests/Windows/HealdanSaveTests.cs
git commit -m "Follow a copy's saves to Healdan in its title, and add Save as New Version"
```

---

### Task 12: Save to Healdan and Save as New Document

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/SaveToHealdanTests.cs`

**Interfaces:**
- Consumes: Tasks 9 to 11's window members; `HelperChannel.SaveNewAsync`, `FileAsync` and `AdoptedAsync` (Task 4);
  `WindowDocument.NameForHealdan` (Task 5).
- Produces:
  - `DocumentWindow.SaveToAsync(string target, bool asCopy = false) : Task<bool>`, still the one save path.
  - `DocumentWindow.SaveNewAsync() : Task<bool>`, internal: Save to Healdan for a local PDF, and Save as New Document
    for a copy.
  - `DocumentWindow.SaveAsWithPanelAsync() : Task<bool>`, internal: the Mac's own Save panel.
  - `DocumentWindow.PickSaveTarget : Func<string?, Task<string?>>`, internal and settable: the Mac's Save panel, given
    the name it starts with, answering the file picked or null.

§6.6's steps:
1. `/v1/save-new` shows the Helper's Save window.
2. On `save`, the PDF with its markups is written to `tempPath` through `SaveToAsync(…, asCopy: true)`. The author goes
   in as in any save, and the window stays on its own file with its changes unsaved. Then `/v1/file` gets the answer's
   values unchanged.
3. The window moves to the new copy, taking no input meanwhile: it reopens the file there, writes the new lock file,
   lets the old copy go, if there was one, and calls `/v1/adopted`.
4. On `local`, the Mac's Save panel shows. On `cancel`, nothing happens.

A Helper that doesn't answer shows the Mac's Save panel. An error is shown first (question 4), and a filing the Helper
refuses also shows the panel, as the LibreOffice extension does, since the Helper has told the user why.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/SaveToHealdanTests.cs`:
```csharp
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.6: Save to Healdan for a local PDF, and Save as New Document for a copy, against the fake Helper.</summary>
[Trait("Category", "PdfViewer")]
public sealed class SaveToHealdanTests : IDisposable
{
    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public SaveToHealdanTests() =>
        // The Helper's Save window, filled in and saved.
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject
        {
            ["action"] = "save",
            ["folderId"] = 42,
            ["name"] = "Report",
            ["format"] = "pdf",
            ["properties"] = new JsonObject { ["Client"] = "Acme" },
            ["tempPath"] = Path.Combine(_helper.Folders.Temp, "save-new.pdf"),
        });

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    /// <summary>The copy the fake Helper files the document as.</summary>
    private string Filed => Path.Combine(_helper.Folders.Copies, "Report #1 v1.pdf");

    private string Local()
    {
        var path = Path.Combine(_dir, "Report.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PdfViewerFact]
    public async Task Save_to_Healdan_files_a_local_pdf_and_the_window_moves_to_its_copy()
    {
        var local = Local();

        var (saved, shown, mode, title, locked, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, window.FilePath, window.Document?.Mode, window.Title, File.Exists(CopyLock.PathFor(Filed)), window.HasUnsavedChanges);
            await CloseAsync(window);
            return state;
        });

        Assert.True(saved);
        Assert.Equal(Filed, shown);
        Assert.Equal(DocumentMode.Editable, mode);
        Assert.Equal("Report — #1 v1", title);
        Assert.True(locked);
        Assert.False(unsaved);
        Assert.Equal(new[] { "save-new", "file", "adopted", "closed" }, _helper.Routes);
        var asked = Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew));
        Assert.Equal("Report", (string?)asked["name"]);
        Assert.Null(asked["path"]);
        var filing = Assert.Single(_helper.CallsTo(ChannelRoutes.File));
        Assert.Equal(42, (int)filing["folderId"]!);
        Assert.Equal("Acme", (string?)filing["properties"]!["Client"]);
        Assert.Null(filing["replaces"]);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(Filed), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Document_files_a_copys_markups_and_lets_the_old_copy_go()
    {
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        bool? oldLocked = null, newLocked = null;
        _helper.Answer(ChannelRoutes.Closed, _ =>
        {
            if (oldLocked is null) // the old copy's, during the move
                (oldLocked, newLocked) = (File.Exists(CopyLock.PathFor(old)), File.Exists(CopyLock.PathFor(Filed)));
            return Task.FromResult<JsonNode>(new JsonObject());
        });

        var shown = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(old, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveNewAsync();
            var path = window.FilePath;
            await CloseAsync(window);
            return path;
        });

        Assert.Equal(Filed, shown);
        Assert.Equal(new[] { "state", "save-new", "file", "closed", "adopted", "closed" }, _helper.Routes);
        var asked = Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew));
        Assert.Equal(old, (string?)asked["path"]);
        Assert.Equal("Settlement Agreement", (string?)asked["name"]);
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.File))["replaces"]);
        Assert.Equal(new[] { old, Filed }, _helper.CallsTo(ChannelRoutes.Closed).Select(b => (string?)b["path"]));
        Assert.False(oldLocked);
        Assert.True(newLocked);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(old));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(Filed), 0)).Author);
    }

    [PdfViewerFact]
    public async Task The_Helpers_way_back_shows_the_Macs_Save_panel()
    {
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "local" });
        var local = Local();
        var elsewhere = Path.Combine(_dir, "Elsewhere.pdf");

        var (saved, suggested, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            string? name = null;
            window.PickSaveTarget = start =>
            {
                name = start;
                return Task.FromResult<string?>(elsewhere);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, name, window.FilePath);
            await CloseAsync(window);
            return state;
        });

        Assert.True(saved);
        Assert.Equal("Report.pdf", suggested);
        Assert.Equal(elsewhere, shown);
        Assert.Equal(new[] { "save-new" }, _helper.Routes);
    }

    [PdfViewerFact]
    public async Task Cancel_leaves_the_window_and_its_changes()
    {
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "cancel" });
        var local = Local();

        var (saved, panel, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, shownPanel, window.HasUnsavedChanges);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.False(panel);
        Assert.True(unsaved);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
    }

    [PdfViewerFact]
    public async Task With_the_Helper_away_its_the_Macs_Save_panel_with_nothing_to_say()
    {
        _helper.GoAway();
        var local = Local();

        var (panel, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveNewAsync();
            var state = (shownPanel, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.True(panel);
        Assert.Empty(errors);
    }

    [PdfViewerFact]
    public async Task An_error_from_the_Helper_is_shown_then_the_Macs_Save_panel()
    {
        _helper.Fail(ChannelRoutes.SaveNew, 500, "internal-error");
        var local = Local();

        var (panel, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveNewAsync();
            var state = (shownPanel, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.True(panel);
        Assert.Equal("Healdan Helper couldn’t show its Save window. Healdan Helper answered 500 (internal-error)", Assert.Single(errors));
    }

    [PdfViewerFact]
    public async Task A_filing_that_fails_keeps_the_window_on_its_file_with_its_changes()
    {
        _helper.Fail(ChannelRoutes.File, 500, "internal-error");
        var local = Local();

        var (saved, shown, unsaved, panel) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, window.FilePath, window.HasUnsavedChanges, shownPanel);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.Equal(local, shown);
        Assert.True(unsaved);
        Assert.True(panel);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
    }

    [PdfViewerFact]
    public async Task The_read_only_bar_offers_Save_as_New_Document()
    {
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");

        var (label, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(old, _helper.Link());
            var button = window.BarButton.IsVisible ? window.BarButton.Content as string : null;
            window.BarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // The window has the new copy's path as soon as the move begins, and takes input again once it's done.
            await TestWindows.WaitForAsync(() => window.FilePath == Filed && window.DocumentPanel.IsEnabled);
            var path = window.FilePath;
            await CloseAsync(window);
            return (button, path);
        });

        Assert.Equal("Save as New Document…", label);
        Assert.Equal(Filed, shown);
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew))["path"]);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~SaveToHealdanTests`
Expected: the build fails. `SaveNewAsync` and `PickSaveTarget` don't exist yet.

- [ ] **Step 3: Write a copy for Healdan through the one save path**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, add after the fields Task 11 added:
```csharp
    private const string SaveAsNewDocumentLabel = "Save as New Document…";
    private bool _writtenForHealdan; // a copy was written for Healdan, which cleared IsDirty, but the window's file lacks the changes
    private bool _moving;            // moving to the new copy Healdan filed: the window takes no input (spec §6.6 step 3)
    private bool _closeWhenMoved;    // a close came during the move, and goes through once it's done
    private bool _closed;            // the window has closed: a flow still running stops short
    private TaskCompletionSource<bool>? _loadWaiter; // completed when the PdfViewer has loaded the file, or failed to
```

Replace `HasUnsavedChanges` with:
```csharp
    /// <summary>Whether the document has changes its file doesn't.</summary>
    public bool HasUnsavedChanges =>
        Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed || _writtenForHealdan || _savesRunning > 0);
```

In `Show`, change `_saved = _editedSinceSave = _saveFailed = false;` to
`_saved = _editedSinceSave = _saveFailed = _writtenForHealdan = false;`.

Change `SaveToAsync`:
- Its summary and first lines become:
```csharp
    /// <summary>
    /// Writes the document to <paramref name="target"/> (spec §6.4), and the window then shows that file. The
    /// author goes into the new markups, and the file is replaced whole. Returns whether it saved. With
    /// <paramref name="asCopy"/>, it writes a copy for Healdan to file instead (§6.6): the window stays on its own
    /// file, whose changes stay unsaved.
    /// </summary>
    internal async Task<bool> SaveToAsync(string target, bool asCopy = false)
    {
        if (!Viewer.HasDocument || _moving)
            return false;
```
- Just before `using var memory = new MemoryStream();`, add:
```csharp
            // Whether the window's own file lacks changes, read before the snapshot clears IsDirty: a copy written for
            // Healdan leaves them unsaved there.
            var unsaved = Viewer.IsDirty || _editedSinceSave || _saveFailed || _writtenForHealdan;
```
- After `_editedSinceSave = false;`, add:
```csharp
            if (asCopy)
                _writtenForHealdan = unsaved;
```
- Replace
```csharp
            _path = Path.GetFullPath(target);
            _saveFailed = false;
            if (_path != previous)
                movedTo = _path;
```
  with
```csharp
            if (!asCopy)
            {
                _path = Path.GetFullPath(target);
                _saveFailed = false;
                _writtenForHealdan = false;
                if (_path != previous)
                    movedTo = _path;
            }
```
- In the `catch`, replace `_saveFailed = true;` with:
```csharp
            if (!asCopy)
                _saveFailed = true; // a copy for Healdan that fails leaves the window as it was
```
- Replace `AfterSave(previous);` with:
```csharp
            if (!asCopy)
                AfterSave(previous);
```
- Replace the failure's report, `await ReportError($"“{Path.GetFileName(target)}” couldn’t be saved.", failure.Message);`, with:
```csharp
            await ReportError(asCopy ? $"“{Path.GetFileName(_path)}” couldn’t be written for Healdan." : $"“{Path.GetFileName(target)}” couldn’t be saved.",
                failure.Message);
```

The window check at the top ("is open in another window") stays: a copy for Healdan goes to the Helper's `Temp/`,
which no window shows.

- [ ] **Step 4: Give the Mac's Save panel a seam, and add the flow**

Add after `WriteFile`:
```csharp
    /// <summary>The Mac's Save panel, given the name it starts with, answering the file picked or null. The tests replace it.</summary>
    internal Func<string?, Task<string?>> PickSaveTarget { get; set; }
```

In the constructor, after `AskToSave = …`, add:
```csharp
        PickSaveTarget = async name => (await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = name,
            DefaultExtension = "pdf",
            FileTypeChoices = [FilePickerFileTypes.Pdf],
            ShowOverwritePrompt = true,
        }))?.TryGetLocalPath();
```

and change the `DocumentLoaded` handler Task 9 added, and add one for `LoadError`:
```csharp
        Viewer.DocumentLoaded += (_, _) =>
        {
            _loaded = true;
            TakeLockIfEditable();
            _loadWaiter?.TrySetResult(true);
        };
        Viewer.LoadError += (_, _) => _loadWaiter?.TrySetResult(false);
```

and in the `Closed` handler Task 11 changed, add `_closed = true;` as its first line.

Replace `SaveAsAsync` with:
```csharp
    /// <summary>Save As (⇧⌘S): the Mac's Save panel. Task 13 sends it to the Helper first.</summary>
    public Task<bool> SaveAsAsync() => SaveAsWithPanelAsync();

    /// <summary>The Mac's own Save panel: writes the document to the file picked, and the window moves to it.</summary>
    internal async Task<bool> SaveAsWithPanelAsync() =>
        Viewer.HasDocument && await PickSaveTarget(Path.GetFileName(_path)) is { } target && await SaveToAsync(target);

    /// <summary>
    /// Save to Healdan for a local PDF, and Save as New Document for a copy (spec §6.6): the Helper's Save window, then
    /// the PDF with its markups written to the answer's temporary file, filed, and the window moved to the new copy.
    /// The Helper's way back shows the Mac's Save panel, as does a Helper that doesn't answer; an error is shown first.
    /// Returns whether the document was saved.
    /// </summary>
    internal async Task<bool> SaveNewAsync()
    {
        if (!Viewer.HasDocument || _document is not { } document || _moving)
            return false;
        var original = document.TellsHelper ? _path : null;
        SaveNewAnswer answer;
        try
        {
            answer = await Healdan.Channel.SaveNewAsync(document.NameForHealdan, original);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            return await SaveAsWithPanelAsync();
        }
        catch (HelperErrorException e)
        {
            await TellAsync("Healdan Helper couldn’t show its Save window.", e.Message);
            return await SaveAsWithPanelAsync();
        }
        if (_closed)
            return false; // the window closed while the Helper's was open
        if (answer.Action == SaveNewAction.Local)
            return await SaveAsWithPanelAsync();
        if (answer.Action != SaveNewAction.Save)
            return false;
        if (answer.Format != "pdf" || answer.TempPath is not { Length: > 0 } temp)
        {
            await TellAsync("Healdan Helper asked for a file CoffeePDF can’t write.", "CoffeePDF writes PDFs only, so the Mac’s Save panel is shown instead.");
            return await SaveAsWithPanelAsync();
        }
        if (!await SaveToAsync(temp, asCopy: true))
        {
            TryDeleteTemporary(temp); // the Helper deletes it only after /v1/file
            return false;
        }
        if (await FileAndMoveAsync(answer, original, temp))
            return true;
        return await SaveAsWithPanelAsync();
    }

    /// <summary>
    /// Files the PDF written to <paramref name="temp"/>, and moves the window to the new copy (spec §6.6 steps 2 and
    /// 3), taking no input meanwhile. Returns whether it was filed. When it wasn't, the window is as it was, with its
    /// changes unsaved.
    /// </summary>
    private async Task<bool> FileAndMoveAsync(SaveNewAnswer answer, string? original, string temp)
    {
        _moving = true;
        DocumentPanel.IsEnabled = false;
        try
        {
            CopyState filed;
            try
            {
                filed = await Healdan.Channel.FileAsync(answer, original);
            }
            catch (HelperUnreachableException)
            {
                TryDeleteTemporary(temp);
                OnHelperAway();
                return false;
            }
            catch (HelperErrorException e)
            {
                // The Helper has told the user why, in a notification, and deleted the file (the Desktop Clients page).
                Healdan.Log.Warn($"Filing: {e.Message}");
                return false;
            }
            if (!filed.Known || filed.Path.Length == 0)
            {
                await TellAsync("The document is filed in Healdan, but Healdan Helper gave no copy to open.",
                    "Close this window without saving, and open the document from Healdan.");
                return true;
            }
            await MoveToCopyAsync(filed);
            return true;
        }
        finally
        {
            _moving = false;
            DocumentPanel.IsEnabled = true;
            if (_closeWhenMoved)
                Close();
        }
    }

    /// <summary>
    /// Moves the window to the copy Healdan filed (spec §6.6 step 3), in its order: reopens the file there, writes its
    /// lock file (as the PdfViewer loads it), lets the old copy go, if there was one, and tells the Helper the window
    /// has adopted the new copy, whose file as it is now is the starting point.
    /// </summary>
    private async Task MoveToCopyAsync(CopyState filed)
    {
        StopFollowing();
        var (oldPath, oldDocument, oldLock) = (_path, _document, _lockHeld);
        _lockHeld = false;
        var loaded = _loadWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Show(filed.Path, WindowDocument.Opened(filed.Path, healdan: true, inCopies: true).Answered(filed));
        var shown = await loaded.Task;
        _loadWaiter = null;
        await LetGoAsync(oldPath, oldDocument, oldLock);
        if (!shown)
            return; // the PdfViewer couldn't open the new copy, and says so in the window
        try
        {
            var adopted = await Healdan.Channel.AdoptedAsync(filed.Path);
            if (adopted.Known && _document is not null && _path == Path.GetFullPath(filed.Path))
            {
                _document = _document.Answered(adopted);
                ApplyMode();
            }
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            // An older Helper, or one that's gone: it takes the first write that settles as the starting point instead.
            Healdan.Log.Warn($"/v1/adopted: {e.GetType().Name}");
        }
    }

    /// <summary>Deletes the file written for Healdan when the Helper won't, as it deletes one only after <c>/v1/file</c>.</summary>
    private void TryDeleteTemporary(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Healdan.Log.Warn($"The file written for Healdan couldn’t be deleted ({e.GetType().Name})");
        }
    }
```

- [ ] **Step 5: Take no input while moving, and give the read-only bar its button**

In `CloseWithPromptAsync`, change `if (_asking)` to `if (_asking || _moving)`. In `SaveAsNewVersionAsync`, change
`if (_document is not { Mode: DocumentMode.Editable } document || _path is not { } path)` to
`if (_moving || _document is not { Mode: DocumentMode.Editable } document || _path is not { } path)`. The menu bar still
works while the window takes no input, so each Healdan command checks.

In `OnClosing`, after `base.OnClosing(e);`, add:
```csharp
        if (_moving)
        {
            // The window takes no input until it's the new copy's (spec §6.6 step 3); the close follows then.
            e.Cancel = true;
            _closeWhenMoved = true;
            return;
        }
```

Replace `ShowBar` and `OnBarButton` with:
```csharp
    private void ShowBar(WindowBar? bar)
    {
        Bar.IsVisible = bar is not null;
        BarText.Text = bar?.Text;
        BarButton.IsVisible = bar is { Action: not BarAction.None };
        BarButton.Content = bar?.Action switch
        {
            BarAction.SaveAsNewDocument => SaveAsNewDocumentLabel,
            BarAction.TurnOnHealdan => TurnOnHealdanLabel,
            _ => null,
        };
    }

    private async void OnBarButton(object? sender, RoutedEventArgs e)
    {
        switch (_document?.Bar?.Action)
        {
            case BarAction.SaveAsNewDocument:
                await SaveNewAsync();
                break;
            case BarAction.TurnOnHealdan:
                TurnOnHealdan();
                break;
        }
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run, with the key: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~SaveToHealdanTests|FullyQualifiedName~SaveAsAndRecentTests|FullyQualifiedName~DocumentWindowSaveTests|FullyQualifiedName~ClosingTests"`
Expected: PASS. Phase 1's save and close tests show the one save path is unchanged for a local file.

- [ ] **Step 7: Commit**

```bash
git add CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf.Tests/Windows/SaveToHealdanTests.cs
git commit -m "Add Save to Healdan and Save as New Document, which move the window to the new copy"
```

---

### Task 13: ⌘O and ⇧⌘S through the Helper, Properties, and the Healdan menu

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/HealdanMenuTests.cs`

**Interfaces:**
- Consumes: Tasks 9 to 12's window members; `HelperChannel.OpenDialogAsync` and `PropertiesAsync` (Task 4).
- Produces:
  - `DocumentWindow.OpenAsync() : Task`, internal: Open… (⌘O).
  - `DocumentWindow.SaveAsAsync() : Task<bool>`: Save As (⇧⌘S), now through the Helper while support is on.
  - `DocumentWindow.PropertiesAsync() : Task`, internal.
  - `DocumentWindow.PickFilesToOpen : Func<Task<IReadOnlyList<string>>>`, internal and settable: the Mac's Open panel.
  - The Healdan menu: `Save to Healdan…`, `Save as New Version`, `Save as New Document…` and `Properties…`.

§6.3: while support is on, ⌘O calls `/v1/open-dialog` and ⇧⌘S calls `/v1/save-new`. The Helper's way back (`local`),
a Helper that doesn't answer, or support off gives the Mac's own boxes. The Healdan menu follows §6.1's table, and
shows only while support is on (§2.5 step 7).

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/HealdanMenuTests.cs`:
```csharp
using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.1, §6.3 and §6.7: the Healdan menu in each mode, ⌘O and ⇧⌘S through the Helper, and Properties.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanMenuTests : IDisposable
{
    private static readonly string[] Items = ["Save to Healdan…", "Save as New Version", "Save as New Document…", "Properties…"];

    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    private string Local()
    {
        var path = Path.Combine(_dir, $"Report {Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>The Healdan menu, shown or hidden, with a 1 or 0 for each of its items, and whether Save is on: "shown 1001 save".</summary>
    private static string Menu(DocumentWindow window) =>
        $"{(window.MenuItem("Healdan").IsVisible ? "shown" : "hidden")} " +
        $"{string.Concat(Items.Select(item => window.MenuItem(item).IsEnabled ? "1" : "0"))} " +
        $"{(window.MenuItem("Save").IsEnabled ? "save" : "no save")}";

    [PdfViewerFact]
    public async Task Each_mode_has_its_Healdan_items()
    {
        var local = Local();
        var editable = _helper.AddCopy("Editable", 1, "1");
        var readOnly = _helper.AddCopy("Held", 2, "1", readOnly: true);
        var away = _helper.AddCopy("Away", 3, "1");

        var menus = await Headless.RunAsync(async () =>
        {
            var found = new List<string>();
            foreach (var path in new[] { local, editable, readOnly })
            {
                var (window, _) = await TestWindows.OpenAsync(path, _helper.Link());
                found.Add(Menu(window));
                await CloseAsync(window);
            }
            _helper.GoAway();
            var (awayWindow, _) = await TestWindows.OpenAsync(away, _helper.Link());
            found.Add(Menu(awayWindow));
            await CloseAsync(awayWindow);
            return found;
        });

        // Local: Save to Healdan and Properties. Editable: everything. Read-only: Save as New Document. Helper away: none.
        Assert.Equal(new[] { "shown 1001 save", "shown 1111 save", "shown 0010 no save", "shown 0000 save" }, menus);
    }

    [PdfViewerFact]
    public async Task With_support_off_there_is_no_Healdan_menu_until_its_turned_on()
    {
        var link = _helper.Link(enabled: false);

        var (before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), link);
            var hidden = Menu(window);
            link.Settings.HealdanEnabled = true;
            var shown = Menu(window);
            await CloseAsync(window);
            return (hidden, shown);
        });

        Assert.Equal("hidden 0000 save", before);
        Assert.Equal("shown 1001 save", after);
    }

    [PdfViewerFact]
    public async Task Open_shows_the_Helpers_Open_window_whose_way_back_is_the_Macs()
    {
        var answers = new Queue<string>(["opened", "cancel", "local"]);
        _helper.Answer(ChannelRoutes.OpenDialog, _ => Task.FromResult<JsonNode>(new JsonObject { ["action"] = answers.Dequeue() }));

        var (afterTwo, afterThree) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var panels = 0;
            window.PickFilesToOpen = () =>
            {
                panels++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await window.OpenAsync(); // opened: the Helper opens what was chosen
            await window.OpenAsync(); // cancel
            var two = panels;
            await window.OpenAsync(); // local: the Mac's Open panel
            var three = panels;
            await CloseAsync(window);
            return (two, three);
        });

        Assert.Equal(0, afterTwo);
        Assert.Equal(1, afterThree);
        Assert.Equal(3, _helper.CallsTo(ChannelRoutes.OpenDialog).Count);
        Assert.All(_helper.CallsTo(ChannelRoutes.OpenDialog), body => Assert.Equal("CoffeePDF", (string?)body["app"]));
    }

    [PdfViewerFact]
    public async Task With_support_off_Open_and_Save_As_are_the_Macs_and_the_Helper_isnt_asked()
    {
        var (opened, saved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link(enabled: false));
            int open = 0, save = 0;
            window.PickFilesToOpen = () =>
            {
                open++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            window.PickSaveTarget = _ =>
            {
                save++;
                return Task.FromResult<string?>(null);
            };
            await window.OpenAsync();
            await window.SaveAsAsync();
            await CloseAsync(window);
            return (open, save);
        });

        Assert.Equal(1, opened);
        Assert.Equal(1, saved);
        Assert.Empty(_helper.Calls);
    }

    [PdfViewerFact]
    public async Task An_error_from_the_Open_window_is_shown_then_the_Macs_Open_panel()
    {
        _helper.Fail(ChannelRoutes.OpenDialog, 500, "internal-error");

        var (panels, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var shown = 0;
            window.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await window.OpenAsync();
            var state = (shown, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(1, panels);
        Assert.Equal("Healdan Helper couldn’t show its Open window. Healdan Helper answered 500 (internal-error)", Assert.Single(errors));
    }

    [PdfViewerFact]
    public async Task Save_As_is_the_Helpers_Save_window_while_support_is_on()
    {
        var panel = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var shown = false;
            window.PickSaveTarget = _ =>
            {
                shown = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveAsAsync(); // the fake's Save window answers cancel
            await CloseAsync(window);
            return shown;
        });

        Assert.False(panel);
        Assert.Null(Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew))["path"]);
    }

    [PdfViewerFact]
    public async Task Properties_of_a_pdf_that_isnt_in_Healdan_runs_Save_to_Healdan()
    {
        _helper.Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = "file" });

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            await window.PropertiesAsync();
            await CloseAsync(window);
            return true;
        });

        Assert.Equal(new[] { "properties", "save-new" }, _helper.Routes);
    }

    [PdfViewerFact]
    public async Task Properties_saved_moves_the_title_on()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.Answer(ChannelRoutes.Properties, _ =>
        {
            // Renamed in the Helper's Properties window. The copy keeps its file name while it's open.
            _helper.SetState(_helper.StateOf(copy) with { Title = "Signed Settlement Agreement #1234 v2.1" });
            return Task.FromResult<JsonNode>(new JsonObject { ["action"] = "saved" });
        });

        var title = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.PropertiesAsync();
            var shown = window.Title;
            await CloseAsync(window);
            return shown;
        });

        Assert.Equal("Signed Settlement Agreement — #1234 v2.1", title);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~HealdanMenuTests`
Expected: the build fails. `OpenAsync`, `PropertiesAsync`, `PickFilesToOpen` and the Healdan menu don't exist yet.

- [ ] **Step 3: Add the Healdan menu**

In `CoffeePdf/Windows/DocumentWindow.axaml`, add after the `Tools` menu:
```xml
      <!-- Healdan's commands (spec §6.1): shown while Healdan support is on, each item on as the window's mode allows. -->
      <NativeMenuItem Header="Healdan">
        <NativeMenu>
          <NativeMenuItem Header="Save to Healdan…" Click="OnSaveToHealdan" />
          <NativeMenuItem Header="Save as New Version" Click="OnSaveAsNewVersion" />
          <NativeMenuItem Header="Save as New Document…" Click="OnSaveAsNewDocument" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Properties…" Click="OnProperties" />
        </NativeMenu>
      </NativeMenuItem>
```

- [ ] **Step 4: Send ⌘O, ⇧⌘S and the menu's items to the Helper**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, add after `PickSaveTarget`:
```csharp
    /// <summary>The Mac's Open panel, answering the files picked. The tests replace it.</summary>
    internal Func<Task<IReadOnlyList<string>>> PickFilesToOpen { get; set; }
```

In the constructor, after `PickSaveTarget = …`, add:
```csharp
        PickFilesToOpen = async () => (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.Pdf],
        })).Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
```

Replace `SaveAsAsync` with:
```csharp
    /// <summary>Save As (⇧⌘S): the Helper's Save window while Healdan support is on (spec §6.3), else the Mac's Save panel.</summary>
    public Task<bool> SaveAsAsync() => Allowed.HelperBoxes ? SaveNewAsync() : SaveAsWithPanelAsync();
```

Add after `SaveNewAsync` and its helpers:
```csharp
    /// <summary>
    /// Open… (⌘O): the Helper's Open window while Healdan support is on (spec §6.3). Its way back, a Helper that doesn't
    /// answer, or support off shows the Mac's Open panel; an error is shown first.
    /// </summary>
    internal async Task OpenAsync()
    {
        if (Allowed.HelperBoxes)
        {
            try
            {
                // Opened: the Helper opens what was chosen in the app for its kind, so a PDF comes back to CoffeePDF.
                if (await Healdan.Channel.OpenDialogAsync() is OpenDialogAction.Opened or OpenDialogAction.Cancel)
                    return;
            }
            catch (HelperUnreachableException)
            {
                OnHelperAway();
            }
            catch (HelperErrorException e)
            {
                await TellAsync("Healdan Helper couldn’t show its Open window.", e.Message);
            }
        }
        foreach (var path in await PickFilesToOpen())
            App.Current.OpenFile(path);
    }

    /// <summary>Properties… (spec §6.7): the Helper's Properties window, or Save to Healdan for a PDF that isn't in Healdan.</summary>
    internal async Task PropertiesAsync()
    {
        if (_moving || _path is not { } path)
            return;
        PropertiesAction action;
        try
        {
            action = await Healdan.Channel.PropertiesAsync(path);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            await TellAsync("Healdan Helper isn’t running.", "Start it to see this document’s properties in Healdan.");
            return;
        }
        catch (HelperErrorException e)
        {
            await TellAsync("Healdan Helper couldn’t show its Properties window.", e.Message);
            return;
        }
        if (action == PropertiesAction.File)
            await SaveNewAsync();
        else if (action == PropertiesAction.Saved)
            await AskHelperAsync(); // a new name in Healdan moves the title on
    }
```

Replace `OnOpen` with:
```csharp
    private async void OnOpen(object? sender, EventArgs e) => await OpenAsync();
```

and add beside `OnSaveAs`:
```csharp
    // Save to Healdan on a copy is its ordinary save, as in the LibreOffice extension; on a local PDF it files it (§6.6).
    private async void OnSaveToHealdan(object? sender, EventArgs e)
    {
        if (_document?.Mode == DocumentMode.Editable)
            await SaveAsync();
        else
            await SaveNewAsync();
    }

    private async void OnSaveAsNewVersion(object? sender, EventArgs e) => await SaveAsNewVersionAsync();

    private async void OnSaveAsNewDocument(object? sender, EventArgs e) => await SaveNewAsync();

    private async void OnProperties(object? sender, EventArgs e) => await PropertiesAsync();
```

In `ApplyMode`, after `MenuItem("Save").IsEnabled = allowed.Save;`, add:
```csharp
        MenuItem("Healdan").IsVisible = allowed.HealdanMenu; // none with support off (spec §2.5 step 7)
        MenuItem("Save to Healdan…").IsEnabled = allowed.SaveToHealdan;
        MenuItem("Save as New Version").IsEnabled = allowed.SaveAsNewVersion;
        MenuItem("Save as New Document…").IsEnabled = allowed.SaveAsNewDocument;
        MenuItem("Properties…").IsEnabled = allowed.Properties;
```

- [ ] **Step 5: Run the tests to see them pass**

Run, with the key: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~HealdanMenuTests|FullyQualifiedName~MenuTests"`
Expected: PASS. Phase 1's `MenuTests` show the shortcuts are unchanged: the Healdan items have none.

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf/Windows/DocumentWindow.axaml CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf.Tests/Windows/HealdanMenuTests.cs
git commit -m "Send ⌘O and ⇧⌘S to the Helper's windows, and add the Healdan menu and Properties"
```

---

### Task 14: Docs, the run on the Mac, and the owner's checks

**Files:**
- Modify: `CLAUDE.md`, `README.md`, `docs/ROADMAP.md`, `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`

- [ ] **Step 1: Record the rules this phase added**

In `CLAUDE.md`, add these bullets under **Rules**, after the one on the real settings:
```markdown
- **CoffeePDF reaches Healdan only through the Helper's channel** (spec §9). `ChannelRoutes` lists every route it
  calls. The client calls only those, and `FakeHelper` serves only those. A change to the channel in Healdan changes
  the list in the same release.
- **Tests never touch the real Helper:** `Headless` sets `HEALDAN_HELPER_DEV_ROOT` and `COFFEEPDF_LOG_DIR` to folders of
  the run's own, and the flows' tests give each window a `FakeHelper`'s link.
- **Nothing logs the channel's secret or a file's contents** (`CoffeeLog`). Log an exception by its type.
```

In `README.md`, change the status line's "in Phase 1 (the standalone app)" to "in Phase 2 (Healdan in CoffeePDF)".

- [ ] **Step 2: Run everything, with and without the key**

```bash
dotnet test CoffeePdf.sln
```
Expected without the key: everything passes, and the PdfViewer's tests skip. The core's tests, the fake Helper's among
them, all run.

```bash
eval "$(grep -E '^[[:space:]]*export[[:space:]]+AVALONIA_LICENSE_KEY=' ~/.zshrc | tail -1)"
dotnet test CoffeePdf.sln
```
Expected with the key: everything passes, and nothing skips except `What_the_viewer_does_without_a_licence`.

Run the app's tests five more times with the key. The fake Helper runs a listener per test, in parallel, so a flaky
port or a test that waits on the wrong condition shows here:
```bash
for i in 1 2 3 4 5; do dotnet test CoffeePdf.Tests --filter Category=PdfViewer || break; done
```

- [ ] **Step 3: Build a package for the owner's checks**

Sign it with the owner's Developer ID, with `SIGN_IDENTITY` set as for Phase 1's checks:
```bash
packaging/mac/package.sh 0.2.0-phase2
```
Expected: `packaging/mac/out/CoffeePDF-0.2.0-phase2.dmg`.

- [ ] **Step 4: Update the roadmap and the spec**

In `docs/ROADMAP.md`, set Phase 2's status to "built YYYY-MM-DD; the owner's checks pending", with the day it's built,
and add the plan's link as Phase 1's has it.

In the spec's §16, add a short "Phase 2 (built YYYY-MM-DD)" part after Phase 1's. It records:
- what the tests showed, with and without the key;
- the owner's answers to this plan's questions, and what changed with them;
- the items this plan found for Healdan's milestone (the read-only state after a refusal, the holder's computer, and
  Save as New Version with nothing new to send), and whether the owner put them in §4.4;
- anything the build found that this plan didn't expect.

- [ ] **Step 5: List the owner's checks in the pull request**

Each pull request that changes the look or the Mac's integration lists its checks for the owner (spec §8). Today's
Helper doesn't yet send PDFs to CoffeePDF: that's Healdan's milestone (§4.4, Phase 3), so the full flows wait for
Phase 4's acceptance (§2.5). These checks cover what the Mac shows now. Run them with the package from Step 3,
installed in Applications.

PDFs you check may carry your name as their markup author, so keep them outside the repository, on the Desktop for
example. When a result goes into the spec, describe a name rather than quoting it.

1. **The first launch's question (§5):**
   - Quit CoffeePDF. In `~/Library/Application Support/CoffeePDF/settings.json`, delete `"asked"` from the `healdan`
     section, and set `"enabled"` to `false`. With the Helper installed, open CoffeePDF.
   - Expected: "Use CoffeePDF with Healdan?" shows over the first window. Not Now leaves Settings' box unticked, and
     the next launch doesn't ask.
   - Do it again and choose Use with Healdan. Expected: the box is ticked, and the Healdan menu shows.
2. **Settings (⌘,):** the Healdan section, its box and its note. No label is clipped or overlapping (§14 item 6).
   Ticking and unticking the box shows and hides the Healdan menu in an open local PDF's window at once.
3. **Support off (§2.5 step 7):** there's no Healdan menu, and ⌘O and ⇧⌘S are the Mac's own boxes.
4. **⌘O with the Helper running and support on:**
   - Expected: the Helper's Open window shows. Its way back to the Mac's box shows the Mac's Open panel, and its Cancel
     does nothing.
   - Choose a PDF in it. Today's Helper opens a PDF read-only in the default app, so with CoffeePDF the default PDF app,
     a read-only copy opens in CoffeePDF. Expected: the read-only bar, the title `<name> — #<doc #> v<version>`, and the
     markup tools off.
   - Close the window. Expected: the Helper deletes its copy.
   - If today's Helper does something else, record what.
5. **⇧⌘S on a local PDF with the Helper running:** the Helper's Save window shows, and its way back shows the Mac's
   Save panel. Today's Helper may not file a PDF from CoffeePDF until §4.4 item 4: record what it does.
6. **The Helper quit:** ⌘O and ⇧⌘S are the Mac's own boxes, with no message.
7. **A copy's bar, title and lock file, with a Helper folder of the check's own,** so the real Helper's isn't touched.
   Quit CoffeePDF, put any PDF without personal details on the Desktop as `check.pdf`, then:
   ```bash
   mkdir -p ~/Desktop/helper-check/Copies
   cp ~/Desktop/check.pdf ~/Desktop/helper-check/Copies/"Check #1 v1.pdf"
   HEALDAN_HELPER_DEV_ROOT=~/Desktop/helper-check /Applications/CoffeePDF.app/Contents/MacOS/CoffeePDF ~/Desktop/helper-check/Copies/"Check #1 v1.pdf"
   ```
   - With support on, expected: the title `Check — #1 v1`, the bar saying Healdan Helper isn't running, the markup
     tools on, and `.~coffeepdf.Check #1 v1.pdf#` beside the copy while its window is open (`ls -a` in the folder).
     Closing the window removes it.
   - With support off, expected: the read-only bar, with Turn On Healdan Support. The button turns support on, and the
     bar becomes the Helper-away one.
   - Then delete `~/Desktop/helper-check` and `~/Desktop/check.pdf`.
8. **The log:** `~/Library/Logs/CoffeePDF/coffeepdf.log` names the routes checks 4 and 5 called. With the Helper
   running, this prints how many of the log's lines hold the channel's secret, never the secret itself:
   ```bash
   grep -cF "$(plutil -extract secret raw ~/Library/Application\ Support/Healdan\ Helper/channel.json)" ~/Library/Logs/CoffeePDF/coffeepdf.log
   ```
   Expected: `0`.
9. **Recent documents (§6.10):** the copy from check 4 isn't in File › Open Recent, nor in the Apple menu's Recent
   Items.

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md README.md docs
git commit -m "Record Phase 2's build, and list the owner's checks"
```
