# Sourced by package.sh and launch-test-build.sh: how a binary shows that it carries Avalonia's Developer Tools (the
# devtools spec's §3). Avalonia's own assemblies name the package in an InternalsVisibleTo, so this looks for the
# assembly's file name, which only the bundle of a build that carries it holds. When the package renames or splits that
# assembly, this is the one place to change it. check-notices.py and DeveloperToolsTests.cs go by the package's name,
# AvaloniaUI.DiagnosticsSupport, as CoffeePdf.csproj's PackageReference does.
devtools_marker=AvaloniaUI.DiagnosticsSupport.Avalonia.dll

carries_devtools() { # <binary>
  grep -a -q -F "$devtools_marker" "$1"
}
