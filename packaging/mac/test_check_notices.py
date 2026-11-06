"""Tests for check-notices.py, package.sh's check of THIRD-PARTY-NOTICES.md (spec §12).

    python3 -I packaging/mac/test_check_notices.py

Most run the check on a made-up publish's deps.json and notices. The last reads the real THIRD-PARTY-NOTICES.md, so a
pull request that breaks the file's shape fails here, not at a release's package step. CI runs them on Linux.
"""
import contextlib
import importlib.util
import io
import json
import os
import tempfile
import unittest

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("check_notices", os.path.join(_here, "check-notices.py"))
check = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(check)

NOTICES = """# Third-party notices

The native libraries `libWidget.dylib` and `libRuntime.dylib`.

| Component | Version | Licence | In the app |
|---|---|---|---|
| [.NET runtime](#net-runtime) | 10.0.11 | MIT | inside the binary |
| [Widget](#widget) | 2.0.1 | MIT | `libWidget.dylib` |
| [Helper.Lib](#helperlib) | 0.5.0 | MIT | inside the binary |

## .NET runtime

- **Version:** 10.0.11, the `Microsoft.NETCore.App.Runtime.osx-arm64` package, built from
  dotnet/dotnet at `abc`.
- **In the app:** the runtime, and `libRuntime.dylib`.

```text
The MIT License, which mentions `Widget.Extra` and `libStray.dylib` only here.
```

## Widget

- **Version:** 2.0.1, the packages `Widget` and
  `Widget.NativeAssets.macOS`.
- **In the app:** `Widget.dll`, and `libWidget.dylib`.

## Helper.Lib

- **Version:** 0.5.0: built from somewhere.
"""


def deps(*packages, runtime="10.0.11"):
    """A publish's deps.json carrying the runtime pack and the packages, each (name, version, kind of files)."""
    target, libraries = {"CoffeePDF/1.0.0": {"runtime": {"CoffeePDF.dll": {}}}}, {"CoffeePDF/1.0.0": {"type": "project"}}
    key = "runtimepack.Microsoft.NETCore.App.Runtime.osx-arm64/" + runtime
    target[key], libraries[key] = {"runtime": {"System.Private.CoreLib.dll": {}}}, {"type": "runtimepack"}
    for name, version, kind in packages:
        target["%s/%s" % (name, version)] = {kind: {"file": {}}} if kind else {"dependencies": {}}
        libraries["%s/%s" % (name, version)] = {"type": "package"}
    return {"runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/osx-arm64"},
            "targets": {".NETCoreApp,Version=v10.0/osx-arm64": target}, "libraries": libraries}


CARRIED = (("Widget", "2.0.1", "runtime"), ("Widget.NativeAssets.macOS", "2.0.1", "native"),
           ("Helper.Lib", "0.5.0", "runtime"))
LIBRARIES = ["libRuntime.dylib", "libWidget.dylib"]
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


class TheCheck(unittest.TestCase):

    def assertSays(self, said, *fragments):
        self.assertEqual(len(said), len(fragments), said)
        for problem, fragment in zip(said, fragments):
            self.assertIn(fragment, problem)

    def test_notices_that_match_the_publish_pass(self):
        self.assertEqual(check.problems(deps(*CARRIED), NOTICES, LIBRARIES), [])

    def test_a_newer_runtime_than_the_notices_give_is_refused(self):
        self.assertSays(check.problems(deps(*CARRIED, runtime="10.0.14"), NOTICES, LIBRARIES),
                        "Microsoft.NETCore.App.Runtime.osx-arm64 10.0.14, but the section \".NET runtime\" gives 10.0.11",
                        "the table's row for \".NET runtime\" gives 10.0.11")

    def test_a_package_no_section_names_is_refused(self):
        self.assertSays(check.problems(deps(*CARRIED, ("New.Managed", "1.0.0", "runtime")), NOTICES, LIBRARIES),
                        "New.Managed 1.0.0, which no section names")

    def test_a_package_named_only_in_a_notice_is_refused(self):
        self.assertSays(check.problems(deps(*CARRIED, ("Widget.Extra", "2.0.1", "runtime")), NOTICES, LIBRARIES),
                        "Widget.Extra 2.0.1, which no section names")

    def test_a_new_version_of_a_package_is_refused(self):
        carried = (("Widget", "2.1.0", "runtime"), ("Widget.NativeAssets.macOS", "2.0.1", "native"),
                   ("Helper.Lib", "0.5.0", "runtime"))
        self.assertSays(check.problems(deps(*carried), NOTICES, LIBRARIES),
                        "Widget 2.1.0, but the section \"Widget\" gives 2.0.1", "the table's row for \"Widget\" gives 2.0.1")

    def test_a_section_without_its_row_is_refused(self):
        notices = NOTICES.replace("| [Helper.Lib](#helperlib) | 0.5.0 | MIT | inside the binary |\n", "")
        self.assertSays(check.problems(deps(*CARRIED), notices, LIBRARIES),
                        "the table's row for \"Helper.Lib\" gives nothing")

    def test_a_removed_section_is_refused_though_the_top_still_names_its_library(self):
        notices = NOTICES[:NOTICES.index("## Widget")] + NOTICES[NOTICES.index("## Helper.Lib"):]
        self.assertSays(check.problems(deps(*CARRIED), notices, LIBRARIES),
                        "Widget 2.0.1, which no section names", "Widget.NativeAssets.macOS 2.0.1, which no section names",
                        "the publish has libWidget.dylib, which no section names")

    def test_a_library_named_only_at_the_top_or_in_a_notice_is_refused(self):
        self.assertSays(check.problems(deps(*CARRIED), NOTICES, LIBRARIES + ["libStray.dylib"]),
                        "the publish has libStray.dylib")

    def test_a_package_with_no_files_in_the_app_is_left_out(self):
        self.assertEqual(check.problems(deps(*CARRIED, ("Meta.Package", "1.0.0", None)), NOTICES, LIBRARIES), [])

    def test_a_package_two_sections_name_is_refused(self):
        notices = NOTICES.replace("0.5.0: built from somewhere.", "0.5.0, the package `Widget`.")
        self.assertSays(check.problems(deps(*CARRIED), notices, LIBRARIES),
                        "Widget is named by more than one section (Widget, Helper.Lib)")

    def test_a_file_it_cannot_read_fails(self):
        with tempfile.TemporaryDirectory() as folder, contextlib.redirect_stderr(io.StringIO()) as said:
            notices = os.path.join(folder, "THIRD-PARTY-NOTICES.md")
            with open(notices, "w", encoding="utf-8") as file:
                file.write(NOTICES)
            self.assertEqual(check.main([os.path.join(folder, "missing.json"), notices, folder]), 1)
            broken = os.path.join(folder, "broken.json")
            with open(broken, "w", encoding="utf-8") as file:
                json.dump({"libraries": {}}, file)
            self.assertEqual(check.main([broken, notices, folder]), 1)
        self.assertEqual(said.getvalue().count("couldn't be checked"), 2)


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


class TheRealNotices(unittest.TestCase):
    """THIRD-PARTY-NOTICES.md itself, in the shape the check reads."""

    def test_each_section_has_a_row_and_both_give_the_same_version(self):
        with open(os.path.join(_here, "..", "..", "THIRD-PARTY-NOTICES.md"), encoding="utf-8") as file:
            rows, found, repeated = check.sections(file.read())
        self.assertEqual(repeated, [])
        self.assertEqual(sorted(rows), sorted(found), "each section has a row in the table, and each row a section")
        for heading, body in found.items():
            self.assertEqual(check.version_given(body), rows[heading],
                             "\"%s\": its **Version:** line and its row give the same version" % heading)


if __name__ == "__main__":
    unittest.main()
