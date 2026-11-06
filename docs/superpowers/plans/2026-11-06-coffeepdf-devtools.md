# Developer tools in CoffeePDF's test builds Implementation Plan

Written 2026-11-06 from the devtools spec, which the owner approved the same day. Its code hasn't been built yet. The
spec's §3 facts were checked against `avdt` 2.2.3 and `AvaloniaUI.DiagnosticsSupport` 2.2.3 beforehand, with a throwaway
Avalonia 12.1.4 app outside the repository. That check also found the extension's class:
`WithDeveloperTools(this AppBuilder)` is in `Avalonia.DeveloperToolsExtensions`, so `using Avalonia;` reaches it.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A test build of CoffeePDF can carry Avalonia's Developer Tools, so Claude can inspect and drive the app on the
Mac before handing it to the owner, while a release provably never carries them.

**Architecture:**
- **The switch.** The MSBuild property `CoffeePdfDevTools` (off unless set to `true`) adds the
  `AvaloniaUI.DiagnosticsSupport` package and the constant `COFFEEPDF_DEVTOOLS`, under which `Program.BuildAvaloniaApp`
  calls `.WithDeveloperTools()`.
- **The package.** `package.sh` takes `COFFEEPDF_DEVTOOLS=1` and refuses it for a signed build or a release's version.
  `check-notices.py --devtools` exempts only the developer tools' packages and refuses them without the flag, and
  `package.sh` checks the binary itself.
- **The pass.** A launch script, a probe that writes and reads back the pass's PDFs, and `.mcp.json` make Claude's pass
  before a hand-off repeatable.

**Tech Stack:** .NET 10, C#, xUnit 2.9, Avalonia 12.1.4 (headless for the app's tests), `AvaloniaUI.DiagnosticsSupport`
2.2.3 and the `avdt` 2.2.3 global tool, bash (`package.sh`), Python 3 (`check-notices.py` and its unittest tests), the
Claude app's computer use.

**Spec:** `docs/superpowers/specs/2026-11-06-coffeepdf-devtools-design.md`. Read it first. The v1 spec's §8, §10 and
§12, and `CLAUDE.md`, give the rules it builds on.

## Global Constraints

- **`TreatWarningsAsErrors` is on:** a warning fails the build.
- **`CoffeePdf.Core` references no Avalonia assembly** (`ArchitectureTests`). Nothing in `CoffeePdf.Core` changes in
  this plan.
- **`CoffeePdfDevTools` is off by default,** Debug included. Only the value `true` turns it on.
- **`COFFEEPDF_DEVTOOLS` is `1` or unset.** Any other value that isn't empty stops `package.sh`.
- **Versions:** `AvaloniaUI.DiagnosticsSupport` 2.2.3, the version of the installed `avdt` (`dotnet tool list -g`).
  Never reference the old `Avalonia.Diagnostics` package.
- **Avalonia stays at 12.1.4,** with the patched `libAvaloniaNative.dylib`. Nothing here changes either.
- **A release never carries the developer tools.** That means a signed build, a version with no label (`1.2.3`), and a
  label beginning with `rc` (`1.2.3-rc1`).
- **Exact strings:**
  - the disk image is `CoffeePDF-<version>-devtools.dmg`;
  - `package.sh`'s warning is `WITH the developer tools: any process running as you can read and drive this app. Never
    hand it beyond this Mac.`;
  - the binary check's file name is `AvaloniaUI.DiagnosticsSupport.Avalonia.dll`.
- **Tests wait for conditions,** never for fixed times.
- **The app as Claude runs it never touches the owner's settings, Helper or documents.**
  - `COFFEEPDF_SETTINGS_DIR`, `COFFEEPDF_LOG_DIR` and `HEALDAN_HELPER_DEV_ROOT` point at folders outside the
    repository.
  - The markup author is `Test Reviewer`.
- **Personal details stay out of the repository.**
  - No PDF is ever committed.
  - No file or commit names the owner's home folder, full name or account name: write `~/.dotnet/tools`, `$TMPDIR` and
    `$SCRATCH`, never a real path.
  - The hooks in `.githooks` refuse them.
- **The Avalonia licence key** is never printed or logged. Never build with `-v:d`, `-v:diag` or `-bl`.
- **`package.sh` takes minutes,** so run it in the background and wait for it to finish.
- **Never push, tag or open a pull request** without the owner's go-ahead.

In the steps, `$SCRATCH` is a folder of the session's own outside the repository, such as Claude's scratchpad. Each
shell starts fresh, so set it in each command (`SCRATCH=<that folder>; …`).

---

### Task 1: The build switch

**Files:**
- Create: `CoffeePdf.Tests/DeveloperToolsTests.cs`
- Modify: `CoffeePdf/Program.cs` (all of it, 16 lines)
- Modify: `CoffeePdf/CoffeePdf.csproj` (after the `AvaloniaNative.targets` import)
- Modify: `Directory.Packages.props` (the app's runtime packages)
- Modify: `.github/workflows/ci.yml` (after the step `Build and test`)

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces:
  - the MSBuild property `CoffeePdfDevTools`, which Task 3's `package.sh` sets with `-p:CoffeePdfDevTools=true|false`;
  - `internal static bool Program.CarriesDeveloperTools`, in namespace `CoffeePdf`.

- [ ] **Step 1: Write the failing tests**

Create `CoffeePdf.Tests/DeveloperToolsTests.cs`:

```csharp
namespace CoffeePdf.Tests;

/// <summary>
/// The devtools spec's §4: Avalonia's Developer Tools are in the app only when the build set <c>CoffeePdfDevTools</c>,
/// which only a test build does.
/// </summary>
public sealed class DeveloperToolsTests
{
    private const string Assembly = "AvaloniaUI.DiagnosticsSupport.Avalonia";

    [Fact]
    public void The_app_references_the_developer_tools_exactly_when_it_was_built_with_them() =>
        Assert.Equal(Program.CarriesDeveloperTools,
            typeof(App).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == Assembly));

    [Fact]
    public void A_build_that_does_not_ask_for_them_carries_no_developer_tools() =>
        Assert.False(Program.CarriesDeveloperTools,
            "This build set CoffeePdfDevTools, which only a test build may (the devtools spec's §4)");
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests/CoffeePdf.Tests.csproj --filter "FullyQualifiedName~DeveloperToolsTests"`
Expected: the build fails with CS0117, `'Program' does not contain a definition for 'CarriesDeveloperTools'`.

- [ ] **Step 3: Pin the package's version**

In `Directory.Packages.props`, in the first `<ItemGroup>`, after the line for `AvaloniaUI.Licensing`, add:

```xml
    <!-- Avalonia's Developer Tools, the app's side, for test builds only (the devtools spec's §4). Its version is the avdt
         tool's (dotnet tool list -g): Avalonia asks for both to be updated together, and avdt warns when they differ. -->
    <PackageVersion Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.3" />
```

- [ ] **Step 4: Add the switch to the app's project**

In `CoffeePdf/CoffeePdf.csproj`, after the line
`<Import Project="../packaging/avalonia-native/AvaloniaNative.targets" />`, add:

```xml
  <!-- Avalonia's Developer Tools, for a test build only (the devtools spec's §4). With CoffeePdfDevTools=true, which
       package.sh's COFFEEPDF_DEVTOOLS=1 passes, the app references their package and Program calls WithDeveloperTools.
       It's off unless a build sets it, Debug included, and package.sh and check-notices.py stop a release that carries
       them. -->
  <PropertyGroup>
    <CoffeePdfDevTools Condition="'$(CoffeePdfDevTools)' == ''">false</CoffeePdfDevTools>
  </PropertyGroup>
  <PropertyGroup Condition="'$(CoffeePdfDevTools)' == 'true'">
    <DefineConstants>$(DefineConstants);COFFEEPDF_DEVTOOLS</DefineConstants>
  </PropertyGroup>
  <ItemGroup Condition="'$(CoffeePdfDevTools)' == 'true'">
    <PackageReference Include="AvaloniaUI.DiagnosticsSupport" />
  </ItemGroup>
```

- [ ] **Step 5: Call it from `Program`**

Replace the whole of `CoffeePdf/Program.cs` with:

```csharp
using Avalonia;
using Avalonia.Controls;

namespace CoffeePdf;

/// <summary>Starts CoffeePDF. On macOS one instance runs, and the system hands it every PDF opened (spec §4.3).</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        // Running with no window open, as Preview does (owner, 2026-10-30): ⌘Q quits.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
#if COFFEEPDF_DEVTOOLS
        // Avalonia's Developer Tools, in a test build only (the devtools spec's §4): from launch the app listens for avdt
        // on a pipe of its own, and F12 opens avdt's window. A release never carries them, which package.sh checks.
        builder = builder.WithDeveloperTools();
#endif
        return builder;
    }

    /// <summary>
    /// Whether this build carries Avalonia's Developer Tools, which only a build with <c>CoffeePdfDevTools</c> set does
    /// (the devtools spec's §4).
    /// </summary>
    internal static bool CarriesDeveloperTools =>
#if COFFEEPDF_DEVTOOLS
        true;
#else
        false;
#endif
}
```

- [ ] **Step 6: Run the tests to see them pass, without the switch**

Run: `dotnet test CoffeePdf.Tests/CoffeePdf.Tests.csproj --filter "FullyQualifiedName~DeveloperToolsTests"`
Expected: 2 passed, 0 warnings.

- [ ] **Step 7: Run the first test with the switch, to see the reference follow it**

Run:
`dotnet test CoffeePdf.Tests/CoffeePdf.Tests.csproj -p:CoffeePdfDevTools=true --filter "FullyQualifiedName~The_app_references_the_developer_tools_exactly_when_it_was_built_with_them"`

Expected: 1 passed, which means `CarriesDeveloperTools` is true and the app references the assembly. The second test
fails in this build by design, so don't run it here.

- [ ] **Step 8: Build the app with the switch in Release, as CI will**

Run: `dotnet build CoffeePdf/CoffeePdf.csproj --configuration Release -p:CoffeePdfDevTools=true`
Expected: `Build succeeded`, with 0 warnings and 0 errors. No warning comes from `AvaloniaUI.DiagnosticsSupport`'s
targets, since Avalonia is 12.

- [ ] **Step 9: Build CI's step**

In `.github/workflows/ci.yml`, after the step `Build and test` (its `run:` line ends
`--filter "Category!=Probe"`), add:

```yaml

      # The developer tools' code (the devtools spec's §4) compiles only when CoffeePdfDevTools is set, which no package
      # CI makes sets, so this builds it once to keep it compiling under TreatWarningsAsErrors.
      - name: Build with the developer tools
        if: matrix.runner == 'macos-15'
        run: dotnet build CoffeePdf/CoffeePdf.csproj --configuration Release -p:CoffeePdfDevTools=true
```

- [ ] **Step 10: Build without the switch again, and run the app's tests**

The `obj` folder is shared, so this also puts it back as a build without the switch.

Run: `dotnet test CoffeePdf.Tests/CoffeePdf.Tests.csproj --filter "Category!=Probe"`
Expected: everything passes, the `PdfViewer` tests too when `AVALONIA_LICENSE_KEY` is set.

- [ ] **Step 11: Commit**

```bash
git add CoffeePdf.Tests/DeveloperToolsTests.cs CoffeePdf/Program.cs CoffeePdf/CoffeePdf.csproj Directory.Packages.props .github/workflows/ci.yml
git commit -m "Let a test build carry Avalonia's Developer Tools, behind CoffeePdfDevTools" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `check-notices.py --devtools`

**Files:**
- Modify: `packaging/mac/check-notices.py` (the docstring, a new `DEVTOOLS` constant and `devtools_only()`,
  `problems()`, `main()`)
- Modify: `packaging/mac/test_check_notices.py` (new helpers, and a new class `TheDeveloperTools` before
  `TheRealNotices`)

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces:
  - `check-notices.py [--devtools] <deps.json> <THIRD-PARTY-NOTICES.md> <publish folder>`, which Task 3's `package.sh`
    calls;
  - in the module, `DEVTOOLS = "AvaloniaUI.DiagnosticsSupport"`, `devtools_only(deps) -> set[str]` and
    `problems(deps, notices, libraries, devtools=False) -> list[str]`.

- [ ] **Step 1: Write the failing tests**

In `packaging/mac/test_check_notices.py`, after `LIBRARIES = ["libRuntime.dylib", "libWidget.dylib"]`, add:

```python
DEVTOOLS = (("AvaloniaUI.DiagnosticsSupport", "2.2.3", "runtime"),
            ("Microsoft.Extensions.Logging.Abstractions", "8.0.0", "runtime"),
            ("Microsoft.Extensions.DependencyInjection.Abstractions", "8.0.0", "runtime"),
            ("Microsoft.IO.RecyclableMemoryStream", "3.0.1", "runtime"))


def depending(document, needs):
    """<document>, with each library's dependencies set from <needs>: {"Name/version": {"Name": "version"}}."""
    target = document["targets"][document["runtimeTarget"]["name"]]
    for key, names in needs.items():
        target[key]["dependencies"] = names
    return document


def with_devtools(*extra, project=None, devtools=None):
    """A test build's publish: CARRIED, the developer tools and <extra>. The app's project needs Widget, Helper.Lib, the
    developer tools and <project>. AvaloniaUI.DiagnosticsSupport needs two of its packages and <devtools>, and the third
    comes through Logging.Abstractions, as in the real deps.json."""
    return depending(deps(*CARRIED, *DEVTOOLS, *extra), {
        "CoffeePDF/1.0.0": {"Widget": "2.0.1", "Helper.Lib": "0.5.0", "AvaloniaUI.DiagnosticsSupport": "2.2.3",
                            **(project or {})},
        "Widget/2.0.1": {"Widget.NativeAssets.macOS": "2.0.1"},
        "AvaloniaUI.DiagnosticsSupport/2.2.3": {"Microsoft.Extensions.Logging.Abstractions": "8.0.0",
                                                "Microsoft.IO.RecyclableMemoryStream": "3.0.1", **(devtools or {})},
        "Microsoft.Extensions.Logging.Abstractions/8.0.0": {
            "Microsoft.Extensions.DependencyInjection.Abstractions": "8.0.0"},
    })
```

Then, before `class TheRealNotices(unittest.TestCase):`, add:

```python
class TheDeveloperTools(unittest.TestCase):
    """The devtools spec's §5: a test build made with COFFEEPDF_DEVTOOLS=1 needs no notices for the developer tools,
    and nothing else may carry them."""

    assertSays = TheCheck.assertSays

    def test_they_are_found_through_the_dependencies(self):
        self.assertEqual(check.devtools_only(with_devtools()), {
            "AvaloniaUI.DiagnosticsSupport", "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.IO.RecyclableMemoryStream"})

    def test_a_publish_without_them_has_none(self):
        self.assertEqual(check.devtools_only(deps(*CARRIED)), set())

    def test_a_test_build_leaves_out_only_them(self):
        self.assertEqual(check.problems(with_devtools(), NOTICES, LIBRARIES, devtools=True), [])

    def test_a_build_without_the_flag_that_carries_them_is_refused(self):
        self.assertSays(check.problems(with_devtools(), NOTICES, LIBRARIES),
                        "the publish carries AvaloniaUI.DiagnosticsSupport, the developer tools, which only a test "
                        "build made with COFFEEPDF_DEVTOOLS=1 may carry")

    def test_the_flag_without_them_is_refused(self):
        self.assertSays(check.problems(deps(*CARRIED), NOTICES, LIBRARIES, devtools=True),
                        "COFFEEPDF_DEVTOOLS=1 asked for the developer tools, but the publish doesn't carry "
                        "AvaloniaUI.DiagnosticsSupport")

    def test_a_package_the_app_needs_too_stays_checked(self):
        document = with_devtools(("Shared.Lib", "1.0.0", "runtime"), project={"Shared.Lib": "1.0.0"},
                                 devtools={"Shared.Lib": "1.0.0"})
        self.assertSays(check.problems(document, NOTICES, LIBRARIES, devtools=True),
                        "Shared.Lib 1.0.0, which no section names")

    def test_a_test_build_still_needs_every_other_notice(self):
        document = with_devtools(("New.Managed", "1.0.0", "runtime"), project={"New.Managed": "1.0.0"})
        self.assertSays(check.problems(document, NOTICES, LIBRARIES, devtools=True),
                        "New.Managed 1.0.0, which no section names")

    def test_main_takes_the_flag(self):
        with tempfile.TemporaryDirectory() as folder, contextlib.redirect_stderr(io.StringIO()):
            published, notices = os.path.join(folder, "CoffeePDF.deps.json"), os.path.join(folder, "notices.md")
            with open(published, "w", encoding="utf-8") as file:
                json.dump(with_devtools(), file)
            with open(notices, "w", encoding="utf-8") as file:
                file.write(NOTICES)
            for library in LIBRARIES:
                open(os.path.join(folder, library), "w").close()
            self.assertEqual(check.main(["--devtools", published, notices, folder]), 0)
            self.assertEqual(check.main([published, notices, folder]), 1)
```

- [ ] **Step 2: Run them to see them fail**

Run: `python3 -I packaging/mac/test_check_notices.py`
Expected: the new tests fail with `AttributeError: module 'check_notices' has no attribute 'devtools_only'` and
`TypeError: problems() got an unexpected keyword argument 'devtools'`. The old tests pass.

- [ ] **Step 3: Find the developer tools' packages through the dependencies**

In `packaging/mac/check-notices.py`, after the line `ROW = re.compile(...)`, add:

```python
# Avalonia's Developer Tools, which only a test build made with COFFEEPDF_DEVTOOLS=1 carries (the devtools spec's §5).
DEVTOOLS = "AvaloniaUI.DiagnosticsSupport"
```

After `components(deps)`, add:

```python
def devtools_only(deps):
    """The names of AvaloniaUI.DiagnosticsSupport and of each package only it brings in, which are those the app's
    projects reach only through it, by the dependencies deps.json lists for each library. Empty when it isn't there."""
    target = deps["targets"][deps["runtimeTarget"]["name"]]
    keys = {key.rsplit("/", 1)[0]: key for key in target}
    if DEVTOOLS not in keys:
        return set()

    def reached(starts, avoid=None):
        seen, todo = set(), list(starts)
        while todo:
            key = todo.pop()
            if key in seen or key == avoid:
                continue
            seen.add(key)
            todo.extend(keys[name] for name in target[key].get("dependencies", {}) if name in keys)
        return seen

    projects = [key for key, library in deps["libraries"].items() if library["type"] == "project" and key in target]
    kept = reached(projects, avoid=keys[DEVTOOLS])
    return {key.rsplit("/", 1)[0] for key in reached([keys[DEVTOOLS]]) - kept}
```

- [ ] **Step 4: Refuse them, or leave them out, in `problems()`**

Replace the start of `problems()`, from its `def` line through the `for name, version in components(deps):` line, with:

```python
def problems(deps, notices, libraries, devtools=False):
    rows, found, repeated = sections(notices)
    said = ["the heading \"## %s\" comes more than once" % heading for heading in repeated]
    # A test build made with COFFEEPDF_DEVTOOLS=1 (devtools) needs no notices for the developer tools, since it never
    # leaves the owner's Mac. Anything else that carries them is refused, and its notices for them aren't asked for.
    left_out = devtools_only(deps)
    if left_out and not devtools:
        said.append("the publish carries %s, the developer tools, which only a test build made with "
                    "COFFEEPDF_DEVTOOLS=1 may carry" % DEVTOOLS)
    if devtools and not left_out:
        said.append("COFFEEPDF_DEVTOOLS=1 asked for the developer tools, but the publish doesn't carry %s" % DEVTOOLS)
    for name, version in components(deps):
        if name in left_out:
            continue
```

The rest of the loop's body, and of `problems()`, stays as it is.

- [ ] **Step 5: Take the flag in `main()`**

Replace the first four lines of `main()`'s body (the `if len(arguments) != 3:` check, its `print` and `return 2`, and
`deps_path, notices_path, publish = arguments`) with:

```python
    devtools = "--devtools" in arguments
    arguments = [argument for argument in arguments if argument != "--devtools"]
    if len(arguments) != 3:
        print("usage: check-notices.py [--devtools] <deps.json> <THIRD-PARTY-NOTICES.md> <publish folder>",
              file=sys.stderr)
        return 2
    deps_path, notices_path, publish = arguments
```

And in the `try:` block, change `said = problems(deps, notices, libraries)` to
`said = problems(deps, notices, libraries, devtools)`.

- [ ] **Step 6: Say so in the docstring**

In the module docstring, add `[--devtools] ` to the usage line, before `<deps.json>`, so it reads
`python3 -I check-notices.py [--devtools] <deps.json> <THIRD-PARTY-NOTICES.md> <publish folder>`. Before the
docstring's last paragraph ("It prints each problem …"), add:

```text
--devtools is for a test build made with COFFEEPDF_DEVTOOLS=1 (the devtools spec's §5). It needs no notices for
Avalonia's Developer Tools, since it never leaves the owner's Mac, so AvaloniaUI.DiagnosticsSupport, and each package
the app's projects reach only through it, are left out. Everything else is checked as for a release. Without the flag, a
publish that carries the developer tools is refused, and with it, one that doesn't.
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `python3 -I packaging/mac/test_check_notices.py`
Expected: `OK`, every test passing.

- [ ] **Step 8: Commit**

```bash
git add packaging/mac/check-notices.py packaging/mac/test_check_notices.py
git commit -m "Refuse the developer tools in check-notices.py, unless --devtools asks for a test build's check" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `package.sh`'s `COFFEEPDF_DEVTOOLS`

**Files:**
- Modify: `packaging/mac/package.sh` (its header, the checks after the tools' loop, `dmg`, the licence warning, the
  publish, the check after the Mach-O loop, the notices check, the summary)

**Interfaces:**
- Consumes: `-p:CoffeePdfDevTools` from Task 1, and `check-notices.py --devtools` from Task 2.
- Produces: `COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh <version>`, which makes `CoffeePDF.app` and
  `CoffeePDF-<version>-devtools.dmg` in `OUT_DIR`. Tasks 4 and 6 use them.

- [ ] **Step 1: Document the option in the header**

In the header's `# Environment (all optional):` list, after the `OUT_DIR` entry, add:

```bash
#   COFFEEPDF_DEVTOOLS  1 for a test build with Avalonia's Developer Tools (the devtools spec, docs/DEVTOOLS.md), which
#                   avdt can then inspect and drive. It stops with SIGN_IDENTITY set, or for a version with no label
#                   (1.2.3) or a release candidate's (1.2.3-rc1), and the disk image is CoffeePDF-<version>-devtools.dmg.
#                   Unset, the build must not carry them, and the checks below stop one that does.
```

In the header's paragraph of steps, change `check-notices.py's check that THIRD-PARTY-NOTICES.md gives each component
the publish carries, at its version;` to `the check that only a build with COFFEEPDF_DEVTOOLS=1 carries the developer tools;
check-notices.py's check that THIRD-PARTY-NOTICES.md gives each component the publish carries, at its version;`.

- [ ] **Step 2: Check the option before anything is built**

After the `for tool in dotnet codesign …; done` loop, add:

```bash
# Avalonia's Developer Tools, for a test build only (the devtools spec's §5): avdt can then read and drive the app
# through a pipe any process running as this user can reach, so a release never carries them.
devtools=false
case "${COFFEEPDF_DEVTOOLS:-}" in
  "") ;;
  1) devtools=true ;;
  *) die "COFFEEPDF_DEVTOOLS is 1, for a test build with the developer tools, or unset, not '$COFFEEPDF_DEVTOOLS'" ;;
esac
if [ "$devtools" = true ]; then
  [ -z "${SIGN_IDENTITY:-}" ] ||
    die "COFFEEPDF_DEVTOOLS=1 asks for a test build, but SIGN_IDENTITY asks for a signed build, which is a release candidate and never carries the developer tools"
  label=${version#"$numeric"}
  [ -n "$label" ] ||
    die "COFFEEPDF_DEVTOOLS=1 is for a test build, whose version has a label, such as 1.1.0-toolbar.5, but '$version' is a release's"
  [[ "$label" != -rc* ]] ||
    die "COFFEEPDF_DEVTOOLS=1 is for a test build, but '$version' is a release candidate's, which never carries the developer tools"
fi
```

- [ ] **Step 3: Name the disk image, and warn**

Change `dmg="$out/CoffeePDF-$version.dmg"` to:

```bash
suffix=
[ "$devtools" = false ] || suffix=-devtools
dmg="$out/CoffeePDF-$version$suffix.dmg"
```

After the `if [ -z "${AVALONIA_LICENSE_KEY:-}" ]; then … fi` block, add:

```bash
[ "$devtools" = false ] ||
  warn "WITH the developer tools: any process running as you can read and drive this app. Never hand it beyond this Mac."
```

- [ ] **Step 4: Pass the switch to the publish**

In the `dotnet publish` command, change the line `-p:PublishDepsFilePath="$work/CoffeePDF.deps.json" -nologo` to:

```bash
  -p:PublishDepsFilePath="$work/CoffeePDF.deps.json" -p:CoffeePdfDevTools="$devtools" -nologo
```

- [ ] **Step 5: Check the binary itself**

After the line `[ -f "$work/publish/CoffeePDF" ] || die "the publish made no CoffeePDF binary"`, add:

```bash
# The bytes that ship carry the developer tools only in a build made with COFFEEPDF_DEVTOOLS=1. Avalonia's own
# assemblies name the package in an InternalsVisibleTo, so this looks for the assembly's file name, which only the
# bundle of a build that carries it holds (the devtools spec's §3).
if grep -a -q -F 'AvaloniaUI.DiagnosticsSupport.Avalonia.dll' "$work/publish/CoffeePDF"; then
  [ "$devtools" = true ] ||
    die "the binary carries the developer tools (AvaloniaUI.DiagnosticsSupport), which only a test build made with COFFEEPDF_DEVTOOLS=1 may carry"
else
  [ "$devtools" = false ] || die "COFFEEPDF_DEVTOOLS=1 asked for the developer tools, but the binary doesn't carry them"
fi
```

- [ ] **Step 6: Pass the flag to the notices check**

Change the line `python3 -I "$here/check-notices.py" "$work/CoffeePDF.deps.json" "$notices" "$work/publish" ||` to:

```bash
notices_flags=()
[ "$devtools" = false ] || notices_flags=(--devtools)
python3 -I "$here/check-notices.py" ${notices_flags[@]+"${notices_flags[@]}"} "$work/CoffeePDF.deps.json" "$notices" "$work/publish" ||
```

The next line (`die "THIRD-PARTY-NOTICES.md doesn't cover what the publish carries (above)"`) stays.

- [ ] **Step 7: Say it in the summary**

After the summary's line `say "  sha256 $(shasum -a 256 "$dmg" | cut -d' ' -f1)"`, add:

```bash
[ "$devtools" = false ] || warn "WITH the developer tools: a test build for this Mac only"
```

- [ ] **Step 8: Check the refusals, which come before any build**

Run each of these. Each must exit 1, with the message shown, in a second or two:

```bash
COFFEEPDF_DEVTOOLS=yes packaging/mac/package.sh 0.3.1-devtools.1   # "COFFEEPDF_DEVTOOLS is 1, … or unset, not 'yes'"
COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 1.2.3                # "… but '1.2.3' is a release's"
COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 1.2.3-rc1            # "… '1.2.3-rc1' is a release candidate's …"
COFFEEPDF_DEVTOOLS=1 SIGN_IDENTITY=anything packaging/mac/package.sh 1.2.3-test.1   # "… SIGN_IDENTITY asks for a signed build …"
```

- [ ] **Step 9: Package without the developer tools (in the background)**

Run: `SCRATCH=…; OUT_DIR="$SCRATCH/out-plain" packaging/mac/package.sh 0.3.1-plain.1`
Expected:
- `Built …/CoffeePDF-0.3.1-plain.1.dmg`, with no developer tools warning, and the notices check passing.
- `grep -a -c -F AvaloniaUI.DiagnosticsSupport.Avalonia.dll "$SCRATCH/out-plain/CoffeePDF.app/Contents/MacOS/CoffeePDF"`
  prints `0`.

- [ ] **Step 10: Package with them (in the background)**

Run: `SCRATCH=…; COFFEEPDF_DEVTOOLS=1 OUT_DIR="$SCRATCH/out-devtools" packaging/mac/package.sh 0.3.1-devtools.1`
Expected:
- `Built …/CoffeePDF-0.3.1-devtools.1-devtools.dmg`, with both developer tools warnings, and the notices check passing
  with `--devtools`.
- `codesign --verify --strict` passes, as in every build.
- The same `grep -a -c` on this build's binary prints `1` or more.
- `ls "$SCRATCH/out-devtools/CoffeePDF.app/Contents/MacOS"` lists the same files as the plain build's: no new native
  library.

- [ ] **Step 11: Check the checks' refusals against the real publishes**

The checks would refuse a publish that carried the wrong thing. `package.sh` deletes its work folder, so publish both
ways as it does, keeping each `deps.json`, and run the check on the real files:

```bash
SCRATCH=…
for devtools in false true; do
  dotnet publish CoffeePdf/CoffeePdf.csproj --configuration Release --runtime osx-arm64 --self-contained true \
    --output "$SCRATCH/publish-$devtools" -p:CoffeePdfiumRid=osx-arm64 -p:PublishSingleFile=true -p:DebugType=none \
    -p:DebugSymbols=false -p:PublishDepsFilePath="$SCRATCH/deps-$devtools.json" -p:CoffeePdfDevTools=$devtools -nologo
done
python3 -I packaging/mac/check-notices.py --devtools "$SCRATCH/deps-false.json" THIRD-PARTY-NOTICES.md "$SCRATCH/publish-false"
python3 -I packaging/mac/check-notices.py "$SCRATCH/deps-true.json" THIRD-PARTY-NOTICES.md "$SCRATCH/publish-true"
python3 -I packaging/mac/check-notices.py --devtools "$SCRATCH/deps-true.json" THIRD-PARTY-NOTICES.md "$SCRATCH/publish-true"
dotnet build CoffeePdf.sln
```

Expected:
- The first check exits 1: "COFFEEPDF_DEVTOOLS=1 asked for the developer tools, but the publish doesn't carry
  AvaloniaUI.DiagnosticsSupport".
- The second exits 1: "the publish carries AvaloniaUI.DiagnosticsSupport, the developer tools, …", and nothing else.
- The third exits 0, so the real `deps.json` leaves out exactly the four packages of the spec's §3.
- The last build puts the shared `obj` folder back to the default.

- [ ] **Step 12: Commit**

```bash
git add packaging/mac/package.sh
git commit -m "Take COFFEEPDF_DEVTOOLS in package.sh, for a test build only, and check the binary either way" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The pass's tools

**Files:**
- Create: `.mcp.json`
- Create: `packaging/mac/launch-test-build.sh`
- Create: `CoffeePdf.Core.Tests/PassFolderFactAttribute.cs`
- Create: `CoffeePdf.Core.Tests/Pdf/PassFilesProbe.cs`

**Interfaces:**
- Consumes:
  - Task 3's `CoffeePDF.app` with the developer tools;
  - `MinimalPdf.Build(bool withForeignNote = false)` and `MinimalPdf.ForeignNoteAuthor` (namespace
    `CoffeePdf.Tests.Fixtures`);
  - `PdfAnnotations.Read(byte[] pdf, int pageIndex, string? password = null)`, which returns `AnnotationFacts(Subtype,
    Name, Author)` (namespace `CoffeePdf.Core.Pdf`).
- Produces:
  - `packaging/mac/launch-test-build.sh <folder> [<file.pdf> …]`, which runs until the app quits. It prints
    `CoffeePDF starting, process <pid>` and writes the pid to `<folder>/coffeepdf.pid`;
  - the probes `PassFilesProbe.Writes_the_pass_pdfs` (writing `plain.pdf` and `with-note.pdf`) and
    `PassFilesProbe.Reads_back_the_pass_pdfs`, in the folder `COFFEEPDF_PASS_DIR` names;
  - the MCP server `avalonia_devtools`.

- [ ] **Step 1: Write the probe's attribute**

Create `CoffeePdf.Core.Tests/PassFolderFactAttribute.cs`:

```csharp
namespace CoffeePdf.Core.Tests;

/// <summary>
/// A probe for Claude's pass on a test build (docs/DEVTOOLS.md). It runs only with <see cref="Variable"/> naming a folder
/// outside the repository, where the pass's PDFs go, and is skipped otherwise.
/// </summary>
public sealed class PassFolderFactAttribute : FactAttribute
{
    public const string Variable = "COFFEEPDF_PASS_DIR";

    public PassFolderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 })
            Skip = $"For Claude's pass on a test build: set {Variable} to a folder outside the repository";
    }
}
```

- [ ] **Step 2: Write the probe**

Create `CoffeePdf.Core.Tests/Pdf/PassFilesProbe.cs`:

```csharp
using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;
using Xunit.Abstractions;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>
/// The PDFs for Claude's pass on a test build (docs/DEVTOOLS.md, the devtools spec's §6.3), in the folder that
/// <see cref="PassFolderFactAttribute.Variable"/> names, outside the repository. One probe writes them before the pass,
/// and the other reads back what the test build's saves wrote into them. Run with: COFFEEPDF_PASS_DIR=&lt;folder&gt;
/// dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~PassFilesProbe --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "Probe")]
public sealed class PassFilesProbe(ITestOutputHelper output)
{
    /// <summary>The authors a pass's PDFs may hold: the tests' made-up one, and that of the fixture's note from another app.</summary>
    private static readonly string[] Authors = ["Test Reviewer", MinimalPdf.ForeignNoteAuthor];

    private static string Folder => Environment.GetEnvironmentVariable(PassFolderFactAttribute.Variable)!;

    [PassFolderFact]
    public void Writes_the_pass_pdfs()
    {
        Directory.CreateDirectory(Folder);
        Write("plain.pdf", MinimalPdf.Build());
        Write("with-note.pdf", MinimalPdf.Build(withForeignNote: true));
    }

    /// <summary>Each PDF's markups on its one page, and their authors, which must be one of <see cref="Authors"/>.</summary>
    [PassFolderFact]
    public void Reads_back_the_pass_pdfs()
    {
        var files = Directory.GetFiles(Folder, "*.pdf").Order().ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var annotations = PdfAnnotations.Read(File.ReadAllBytes(file), pageIndex: 0);
            output.WriteLine($"{Path.GetFileName(file)}: {annotations.Count} annotation(s) on page 1");
            foreach (var annotation in annotations)
                output.WriteLine($"  {annotation.Subtype} by {annotation.Author ?? "no author"} ({annotation.Name ?? "no /NM"})");
            Assert.All(annotations.Where(annotation => annotation.Author is not null),
                annotation => Assert.Contains(annotation.Author!, Authors));
        }
    }

    private void Write(string name, byte[] pdf)
    {
        File.WriteAllBytes(Path.Combine(Folder, name), pdf);
        output.WriteLine($"Wrote {name}");
    }
}
```

- [ ] **Step 3: Run the probes, without the folder and with it**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PassFilesProbe"`
Expected: 2 skipped, with the attribute's message, and 0 warnings.

Run:
`SCRATCH=…; COFFEEPDF_PASS_DIR="$SCRATCH/pdfs" dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PassFilesProbe" --logger "console;verbosity=detailed"`

Expected: 2 passed. The output says `Wrote plain.pdf` and `Wrote with-note.pdf`. The read-back shows `plain.pdf: 0
annotation(s)`, and `with-note.pdf: 1 annotation(s)` with a `Text by Acrobat User` line. xUnit doesn't promise an order,
so if the read-back ran first and found no files, run the second probe again.

- [ ] **Step 4: Write the launch script**

Create `packaging/mac/launch-test-build.sh`, and `chmod +x` it:

```bash
#!/usr/bin/env bash
# Starts a test build of CoffeePDF walled off, for Claude's pass before a hand-off (docs/DEVTOOLS.md, the devtools
# spec's §6.3). Its settings, log and Helper folders go in <folder>, the settings giving the markup author Test Reviewer
# and having answered the first launch's Healdan question, so nothing touches the owner's settings, Helper or documents.
#
#   packaging/mac/launch-test-build.sh <folder> [<file.pdf> ...]
#
# <folder> must exist and be outside the repository, such as Claude's scratchpad. Each launch writes its settings
# afresh, so a pass starts from the same state.
#
# Environment (optional):
#   APP   the CoffeePDF.app to start: packaging/mac/out/CoffeePDF.app by default.
#
# It runs the bundle's binary directly, not through `open`, which would let LaunchServices hand the files to a running
# CoffeePDF with the same bundle identifier. It ends by exec-ing the binary, so the process id it prints is the app's,
# which avdt's attach-to-app takes. It also writes that id to <folder>/coffeepdf.pid. It runs until the app quits, so
# start it in the background (Claude: Bash's run_in_background). The app's output goes to <folder>/app.log, and
# `kill "$(cat <folder>/coffeepdf.pid)"` ends it.
set -euo pipefail

die() { printf 'launch-test-build.sh: %s\n' "$*" >&2; exit 1; }

[ "$#" -ge 1 ] || die "usage: launch-test-build.sh <folder> [<file.pdf> ...]"
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
[ -d "$1" ] || die "$1 isn't a folder: give one that exists, outside the repository"
folder=$(cd "$1" && pwd)
case "$folder/" in
  "$repo"/*) die "the folder must be outside the repository, so no PDF or setting lands in it" ;;
esac
shift
app=${APP:-"$here/out/CoffeePDF.app"}
binary="$app/Contents/MacOS/CoffeePDF"
[ -x "$binary" ] || die "$binary isn't there: build it with COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh <version>"

mkdir -p "$folder/settings" "$folder/log" "$folder/helper"
# The shape SettingsStore reads (spec §5): no Healdan support, the question asked, and the tests' made-up author.
cat > "$folder/settings/settings.json" <<'EOF'
{
  "healdan": { "enabled": false, "asked": true },
  "markup": { "author": "Test Reviewer" }
}
EOF

printf '%s\n' "$$" > "$folder/coffeepdf.pid"
printf 'CoffeePDF starting, process %s\n' "$$"
export COFFEEPDF_SETTINGS_DIR="$folder/settings" COFFEEPDF_LOG_DIR="$folder/log" HEALDAN_HELPER_DEV_ROOT="$folder/helper"
exec "$binary" "$@" >> "$folder/app.log" 2>&1
```

- [ ] **Step 5: Check its refusals**

```bash
packaging/mac/launch-test-build.sh                    # usage, exit 1
packaging/mac/launch-test-build.sh /no/such/folder    # "isn't a folder", exit 1
packaging/mac/launch-test-build.sh docs               # "must be outside the repository", exit 1
```

- [ ] **Step 6: Declare the MCP server**

Create `.mcp.json` at the repository's root:

```json
{
  "mcpServers": {
    "avalonia_devtools": {
      "type": "stdio",
      "command": "avdt",
      "args": ["mcp"]
    }
  }
}
```

A session reads `.mcp.json` when it starts, and the owner approves the server the first time. So a session that's
already running uses the throwaway client below for this task's checks. Keep the client in `$SCRATCH`, and never commit
it.

```python
# $SCRATCH/avdt_call.py: python3 -I avdt_call.py '[["attach-to-app", {}], ["search", {"query": "Window"}]]'
import base64, json, select, subprocess, sys, time
calls = json.loads(sys.argv[1])
server = subprocess.Popen(["avdt", "mcp"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
def send(message):
    server.stdin.write(json.dumps(message) + "\n"); server.stdin.flush()
def answer(wanted, timeout=90):
    end = time.time() + timeout
    while time.time() < end:
        if select.select([server.stdout], [], [], 1)[0]:
            line = server.stdout.readline()
            if not line:
                return None
            if line.strip() and json.loads(line).get("id") == wanted:
                return json.loads(line)
    return None
send({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "pass", "version": "0"}}})
answer(1)
send({"jsonrpc": "2.0", "method": "notifications/initialized"})
for number, (name, arguments) in enumerate(calls, start=10):
    send({"jsonrpc": "2.0", "id": number, "method": "tools/call", "params": {"name": name, "arguments": arguments}})
    reply = answer(number)
    print("==", name, json.dumps(arguments))
    for content in (reply or {}).get("result", {}).get("content", []):
        if content.get("type") == "text":
            print(content["text"][:4000])
        else:
            open("shot.png", "wb").write(base64.b64decode(content["data"])); print("[saved shot.png]")
server.terminate()
```

A new attach makes node ids stale, so each run of the client attaches and then searches again.

- [ ] **Step 7: Run the pass's first steps on Task 3's build**

First quit any other CoffeePDF, since the test build shares its bundle identifier. Then start this, in the background:

```bash
SCRATCH=…; APP="$SCRATCH/out-devtools/CoffeePDF.app" packaging/mac/launch-test-build.sh "$SCRATCH" "$SCRATCH/pdfs/plain.pdf"
```

Expected: `cat "$SCRATCH/coffeepdf.pid"` gives the pid, and a window shows the fixture's text ("CoffeePDF fixture").

Then, with that pid, run:
`python3 -I "$SCRATCH/avdt_call.py" '[["attach-to-app", {}], ["attach-to-app", {"id": <pid>}], ["search", {"query": "PdfViewer"}], ["tree", {"depth": 1}]]'`

Expected:
- The first call lists an app whose `processId` is the pid, with `diagnosticsSupportVersion` `2.2.3`.
- The second attaches.
- `search` finds a `PdfViewer` node, and `tree` shows a `DocumentWindow` root.
- Take the window's `screenshot` (its node id from `tree`) and look at it: the page shows.

Then `kill "$(cat "$SCRATCH/coffeepdf.pid")"`. The background task ends, and `ps -p <pid>` finds nothing.

- [ ] **Step 8: Check that a build without them can't be reached**

Start this in the background:

```bash
SCRATCH=…; APP="$SCRATCH/out-plain/CoffeePDF.app" packaging/mac/launch-test-build.sh "$SCRATCH" "$SCRATCH/pdfs/plain.pdf"
```

Once its window shows, `attach-to-app` with no id lists no app with its pid, and `ls "$TMPDIR" | grep CoreFxPipe_avdt_`
shows no pipe for it. Then kill it as above.

- [ ] **Step 9: Commit**

```bash
git add .mcp.json packaging/mac/launch-test-build.sh CoffeePdf.Core.Tests/PassFolderFactAttribute.cs CoffeePdf.Core.Tests/Pdf/PassFilesProbe.cs
git commit -m "Add the pass's tools: the MCP server, a walled-off launch of a test build, and its PDFs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The docs

**Files:**
- Create: `docs/DEVTOOLS.md`
- Modify: `CLAUDE.md` (Build and test, the Packaging rule, the Third-party notices rule)
- Modify: `README.md` (the build block)
- Modify: `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (§8's "Checked by hand", §10)
- Modify: `docs/ROADMAP.md` (the status list)
- Modify: `docs/superpowers/specs/2026-11-06-coffeepdf-devtools-design.md` (its status line)

**Interfaces:**
- Consumes: the names from Tasks 1 to 4: `CoffeePdfDevTools`, `COFFEEPDF_DEVTOOLS`, `check-notices.py --devtools`,
  `launch-test-build.sh`, `PassFilesProbe`, `COFFEEPDF_PASS_DIR` and `avalonia_devtools`.
- Produces: `docs/DEVTOOLS.md`, which Task 6 updates with the findings.

- [ ] **Step 1: Write `docs/DEVTOOLS.md`**

Create it with this content:

````markdown
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
    never carries the tools.
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

Any value but `1` stops it too.

A release can't carry them, for three independent reasons:
- a signed build refuses them;
- `check-notices.py` without `--devtools` refuses a publish that carries `AvaloniaUI.DiagnosticsSupport`;
- the binary check refuses a binary holding `AvaloniaUI.DiagnosticsSupport.Avalonia.dll`.

`check-notices.py --devtools` leaves out only the developer tools' packages: `AvaloniaUI.DiagnosticsSupport`, and what
only it brings in. A build with them never leaves the owner's Mac (owner, 2026-11-06).

## The pass before a hand-off

Test packages handed to the owner carry the developer tools, and Claude runs this pass on them first. In the commands,
`$SCRATCH` is a folder of the session's own, outside the repository.

1. **Quit any other CoffeePDF.** The test build has the same bundle identifier, which computer use and the Dock go by.
2. **Make the PDFs:**
   `COFFEEPDF_PASS_DIR="$SCRATCH/pdfs" dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PassFilesProbe.Writes_the_pass_pdfs"`.
   That writes `plain.pdf` and `with-note.pdf`, the second with a note by another app.
3. **Launch it walled off, in the background:**
   `APP=<the build's CoffeePDF.app> packaging/mac/launch-test-build.sh "$SCRATCH" "$SCRATCH/pdfs/plain.pdf"`.
   - The settings, log and Helper folders go in `$SCRATCH`, with the markup author `Test Reviewer`.
   - It runs until the app quits, and its process id, which is the app's, is in `$SCRATCH/coffeepdf.pid`.
4. **Attach:** `attach-to-app` with that id. A new attach makes node ids stale, so search again after one.
5. **Go through the pull request's list of the owner's checks,** item by item:

   | What | How |
   |---|---|
   | A click on a toolbar button, in each state that matters: nothing selected, a tool armed, a shape selected, text being edited | `avdt`'s `input Click`, or computer use where §Limits says so |
   | What a click did | `search` and `props`: the window's pseudo-classes (`:shape-selected`, `:editing-text`), the button's `:flyout-open`, which element has `:focus` |
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
- **Tests click by position,** as `ToolbarTesting.Click` does (a move, a press, a release). They never call a handler or
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
````

- [ ] **Step 2: Update `CLAUDE.md`**

In the `bash` block under "Build and test", after the line `packaging/mac/package.sh 1.2.3 …`, add:

```bash
COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 1.2.3-name.1   # a test build with Avalonia's Developer Tools (docs/DEVTOOLS.md)
```

Under "Rules", after the "Packaging" bullet, add:

```markdown
- **Test builds carry Avalonia's Developer Tools; releases never do.** A test build handed to the owner is made with
  `COFFEEPDF_DEVTOOLS=1`, and gets Claude's pass first (`docs/DEVTOOLS.md`). `package.sh` refuses them in a signed
  build or for a release's or a release candidate's version, and stops a package that carries them without the switch.
  `AvaloniaUI.DiagnosticsSupport` moves with the `avdt` tool's version.
```

At the end of the "Third-party notices" bullet, add the sentence: "`check-notices.py --devtools` leaves out only the
developer tools' packages, for a build made with `COFFEEPDF_DEVTOOLS=1`, which never leaves the owner's Mac."

- [ ] **Step 3: Update the README**

In `README.md`'s build block, after `packaging/mac/package.sh 0.0.1   # CoffeePDF.app and its .dmg, on a Mac`, add:

```bash
COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh 0.0.1-test.1   # a test build with Avalonia's Developer Tools (docs/DEVTOOLS.md)
```

- [ ] **Step 4: Update the v1 spec**

- In §8, extend the bullet "- **Checked by hand:** the look, focus, macOS integration and the Helper's windows. Each
  pull request that changes them lists its checks for the owner." by adding the sentence: "Before the hand-off, Claude runs
  what it can of them on a test build with Avalonia's Developer Tools (`docs/DEVTOOLS.md`, the devtools spec)."
- In §10, after the bullet ending "the package builds unsigned and is only uploaded as an artifact.", add:

```markdown
- **Test builds** for the owner's checks carry Avalonia's Developer Tools (`COFFEEPDF_DEVTOOLS=1`), and a signed build
  or a release's version never does (`2026-11-06-coffeepdf-devtools-design.md`, `docs/DEVTOOLS.md`).
```

- [ ] **Step 5: Update the roadmap and the devtools spec's status**

- In `docs/ROADMAP.md`, at the end of the status's bullet list (after the third-party notices bullet), add the bullet
  below, giving the day this task runs as `YYYY-MM-DD`:

```markdown
  - Avalonia's Developer Tools in test builds, designed on 2026-11-06
    ([spec](superpowers/specs/2026-11-06-coffeepdf-devtools-design.md),
    [plan](superpowers/plans/2026-11-06-coffeepdf-devtools.md)) and built on YYYY-MM-DD. Claude runs a pass on each test
    build before the owner's checks ([how](DEVTOOLS.md)), and the acceptance run on the toolbar's bug is next.
```

- In the devtools spec's status line, replace its last sentence, the one naming the implementation plan, with this,
  giving the same day:

```markdown
  Built on YYYY-MM-DD (`docs/superpowers/plans/2026-11-06-coffeepdf-devtools.md`). The acceptance run (§7) is next.
```

- [ ] **Step 6: Check the hooks pass the docs**

Run: `python3 -I .githooks/test_check_personal_details.py`
Expected: `OK`. The commit below also runs the hook on the changed files.

- [ ] **Step 7: Commit**

```bash
git add docs/DEVTOOLS.md CLAUDE.md README.md docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md docs/ROADMAP.md docs/superpowers/specs/2026-11-06-coffeepdf-devtools-design.md
git commit -m "Document the developer tools in test builds, and Claude's pass before a hand-off" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The acceptance run (the spec's §7)

This task needs the owner:
- Computer use turned on, and access to CoffeePDF approved.
- `avdt` signed in.
- The MCP server approved, in a session started after Task 4.
- The commit `1.1.0-toolbar.4` was built from, from the owner or the toolbar's session.
- For Step 6, the toolbar's fixed commit.

Ask for what's missing, then wait. Nothing from this task's merge is committed or pushed.

**Files:**
- Modify: `docs/superpowers/specs/2026-11-06-coffeepdf-devtools-design.md` (§10, and §6.5 if the outcome changes it)
- Modify: `docs/DEVTOOLS.md` (the pass's table and Limits, from the findings)
- Modify: `docs/ROADMAP.md` (the developer tools' bullet)

**Interfaces:**
- Consumes: Tasks 1 to 5.
- Produces: the spec's §10 findings, and the pass as the findings set it.

- [ ] **Step 1: Make the merge in a worktree of its own**

```bash
SCRATCH=…; git worktree add --detach "$SCRATCH/acceptance" <the commit 1.1.0-toolbar.4 came from>
git -C "$SCRATCH/acceptance" merge --no-commit --no-ff claude/frosty-hypatia-55150e
```

Resolve any conflicts:
- **In docs** (`CLAUDE.md`, `docs/`, `README.md`) and in `test_check_notices.py`, keep the toolbar's side with
  `git -C "$SCRATCH/acceptance" checkout --ours -- <file>`. This merge is thrown away, so only the code matters.
- **In code** (`CoffeePdf/`, `Directory.Packages.props`, `packaging/mac/package.sh`, `packaging/mac/check-notices.py`),
  keep both sides' changes.

- [ ] **Step 2: Package it with the developer tools (in the background)**

```bash
SCRATCH=…; cd "$SCRATCH/acceptance" && COFFEEPDF_DEVTOOLS=1 OUT_DIR="$SCRATCH/out-toolbar4" packaging/mac/package.sh 1.1.0-acceptance.1
```

Expected: `Built …/CoffeePDF-1.1.0-acceptance.1-devtools.dmg`.

- [ ] **Step 3: Launch it, and attach**

Ask the owner to quit any other CoffeePDF. Then make the PDFs (Task 4, Step 3), and launch the build in the background:

```bash
SCRATCH=…; APP="$SCRATCH/out-toolbar4/CoffeePDF.app" packaging/mac/launch-test-build.sh "$SCRATCH" "$SCRATCH/pdfs/plain.pdf"
```

Then attach with `attach-to-app` and the pid. Load computer use's tools (ToolSearch, query `computer`), and
`request_access` for CoffeePDF.

- [ ] **Step 4: Reproduce the bug by `avdt`'s click**

1. With computer use, arm the Rectangle tool (the Shapes button) and drag a rectangle on the page.
2. Disarm the tool, and click the rectangle's edge, so the PdfViewer shows `:shape-selected`. Read it with `search` on
   `PdfViewer`.
3. Find the Colour button with `search`, by its `x:Name` in `CoffeePdf/Toolbar/DocumentToolbar.axaml`, and send it
   `input Click`.
4. Read the PdfViewer's pseudo-classes, the Colour button's `:flyout-open`, and the element with `:focus`.
5. Record: was the selection ended, and were the menus hidden (the owner's finding)?

- [ ] **Step 5: Reproduce it by computer use's click**

Select the rectangle again as in Step 4, then click Colour with computer use. Try the background tool (`app_click`)
first, and note whether it reached CoffeePDF without bringing it to the front. Use the full-screen click only with the
owner's go-ahead. Read and record the same states as in Step 4.

- [ ] **Step 6: Run both on the toolbar's fixed commit**

Do Steps 1 to 5 again with the fixed commit, `1.1.0-acceptance.2` and `$SCRATCH/out-toolbar-fixed`. Expected: both
clicks keep the selection and open the menu. If the fix hasn't landed yet, record that this step is pending, and come
back to it once it has.

- [ ] **Step 7: Measure what the developer tools cost at launch**

Start each of Task 3's two builds three times, with `launch-test-build.sh` and `plain.pdf`. Once computer use's
screenshot shows the page drawn, read `ps -o rss=,time= -p <pid>` (memory, and CPU time to the first window), then kill
the build. Record the averages for the build with the developer tools and the build without them.

- [ ] **Step 8: Record the findings, and set the pass by them**

Replace "None yet. The run in §7 records its findings here." in the spec's §10 with one bullet per finding, in the
spec's style: date, build, what was done, what was seen. The findings are:
- Step 4's result;
- Step 5's result, background click included;
- Step 6's result, or that it's pending;
- Step 7's numbers.

Then, by the spec's §7 outcome:
- **If `avdt`'s click reproduced the bug,** `docs/DEVTOOLS.md`'s first table row stays as it is.
- **If only computer use did,** change that row's "How" to "computer use, for the format buttons and anything that can
  take focus from the viewer; `avdt`'s `input Click` for the rest". Then add to the spec's §6.5, and to the DEVTOOLS
  Limits, a bullet giving the reason from §10.

In the roadmap's bullet, change "and the acceptance run on the toolbar's bug is next." to "and the acceptance run on the
toolbar's bug passed on YYYY-MM-DD (the spec's §10).", or say what's left.

- [ ] **Step 9: Clean up, and commit**

```bash
SCRATCH=…; git worktree remove --force "$SCRATCH/acceptance"
git add docs/superpowers/specs/2026-11-06-coffeepdf-devtools-design.md docs/DEVTOOLS.md docs/ROADMAP.md
git commit -m "Record the developer tools' acceptance run on the toolbar's bug" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Final checks

- [ ] **Step 1: Run everything CLAUDE.md lists**

```bash
dotnet test CoffeePdf.sln
python3 -I .githooks/test_check_personal_details.py
python3 -I packaging/mac/test_check_notices.py
dotnet build CoffeePdf/CoffeePdf.csproj --configuration Release -p:CoffeePdfDevTools=true
dotnet build CoffeePdf.sln
```

Expected: every test passes, with the PdfViewer's too when the key is set. Both builds report 0 warnings. The last build
puts the shared `obj` folder back to the default.

- [ ] **Step 2: Check what's committed**

Run: `git status --short` (expect it to be clean), and `git log --oneline main..HEAD` (expect the spec and the plan,
then Tasks 1 to 6).

- [ ] **Step 3: Report to the owner**

Report the commits, the acceptance run's findings and what's left. Don't push or open a pull request until the owner
says to.
