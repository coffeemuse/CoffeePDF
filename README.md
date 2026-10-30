# CoffeePDF

An everyday PDF app for macOS, in the spirit of Preview, that works directly with documents stored and versioned in
[Healdan](https://github.com/Healdan-Project/healdan), through the Healdan Helper.

**Status:** pre-release, in Phase 1 (the standalone app). See [the roadmap](docs/ROADMAP.md) and
[the v1 design](docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md).

## Building

You need the .NET 10 SDK (`global.json` pins it). The PdfViewer needs an Avalonia licence that covers it, whose key
the build reads from `AVALONIA_LICENSE_KEY`. Without the key everything builds and the PdfViewer's tests are skipped,
but the PdfViewer throws as soon as a window creates it, so the app can't show a PDF (the probe
`What_the_viewer_does_without_a_licence`, spec §16).

```bash
export AVALONIA_LICENSE_KEY=…   # from portal.avaloniaui.net; never commit it
dotnet test CoffeePdf.sln
dotnet run --project CoffeePdf -- path/to/file.pdf
packaging/mac/package.sh 0.0.1   # CoffeePDF.app and its .dmg, on a Mac
```

Run `git config core.hooksPath .githooks` once per clone, so the hooks (which need `python3`) keep personal details
out of the history.

Signed, notarised releases come from `v*` tags in CI. [Releasing](docs/RELEASING.md) covers their secrets.

## Licence

Proprietary for now; see [LICENSE](LICENSE).
