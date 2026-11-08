# CoffeePDF roadmap

- **Status:** v1 designed on 2026-10-29 ([spec](superpowers/specs/2026-10-29-coffeepdf-v1-design.md)). Phase 0 is
  built ([plan](superpowers/plans/2026-10-29-phase-0-foundation-spike.md)), its keyed run and the owner's checks on the
  Mac are done, and the owner answered its decisions on 2026-10-30 (the spec's §16). Phase 1 is built (2026-10-30,
  [plan](superpowers/plans/2026-10-30-phase-1-standalone-app.md)), and the owner's checks on the Mac passed on
  2026-10-30, four after fixes.
  Its findings are in the spec's §16 (Phase 1). Phase 2 is built (2026-10-31,
  [plan](superpowers/plans/2026-10-30-phase-2-healdan-in-coffeepdf.md)), and the owner's checks on the Mac passed on
  2026-10-31, one after a fix.
  The owner answered its questions on 2026-10-30 (the spec's §16, Phase 2's questions), and its findings are in the
  spec's §16 (Phase 2). Phase 3 is Healdan's M3.6, "PDFs in CoffeePDF", built on 2026-11-01
  ([spec](https://github.com/Healdan-Project/healdan/blob/master/docs/superpowers/specs/2026-11-01-m3-6-pdfs-in-coffeepdf-design.md)).
  The owner's checks on Healdan Helper 0.5.0-rc1 passed on 2026-11-01, including the spec's §2.5 walkthrough with its
  first step by a typed link, and left three findings for CoffeePDF (the spec's §16, Phase 3). Step 1 then passed from
  the web page, and Healdan Helper 0.5.0 was released, the same day. Finding 3, the full rewrite, is settled
  (2026-11-02): nothing was lost, but an undone markup stays in the saved file, unseen. Its fix, a full save for
  unsigned PDFs, is built (2026-11-03, the spec's §16). The final review found that the PdfViewer's save breaks a
  signature nested under a parent field. On 2026-11-05 the owner's checks of the fix passed on the test build
  0.3.1-fullsave.1, finding 1 was checked and finding 2 settled, and the owner decided that a save which breaks a
  nested signature does as now, a known limit for v1. Only the v1.0 release is left.
  - The first signed, notarised pre-release, `v0.0.1-spike4`, passed Gatekeeper on 2026-10-30
    ([Releasing](RELEASING.md)).
  - The first release, `v0.3.0`, passed Gatekeeper on 2026-11-01. It has Phase 3's fix to Save as New Version and the
    menu bar with no window open, but not the full save, which came after it.
  - [Issue #9](https://github.com/coffeemuse/CoffeePDF/issues/9), the macOS 27 menu bar, is worked around by a
    temporary patch to Avalonia's macOS library (AvaloniaUI/Avalonia#22285), and the owner's check passed on
    2026-10-31. Removing it is in the backlog.
  - The two checks that need Acrobat are best effort, in
    [issue #3](https://github.com/coffeemuse/CoffeePDF/issues/3), and hold no phase.
  - The third-party notices are in `THIRD-PARTY-NOTICES.md` (2026-11-05, the spec's §12): each component the app
    carries, with its version, its licence and its notice. `package.sh` puts the file in the app's
    `Contents/Resources`, and stops without it, or when it doesn't give a component, or a version, the publish carries
    (`check-notices.py`). `v1.0.0` shipped without them, so the next release is the first to carry them. Until then,
    the owner can attach the file to `v1.0.0`'s GitHub release.
  - Avalonia's Developer Tools in test builds, designed on 2026-11-06
    ([spec](superpowers/specs/2026-11-06-coffeepdf-devtools-design.md),
    [plan](superpowers/plans/2026-11-06-coffeepdf-devtools.md)) and built on 2026-11-06. Claude runs a pass on each test
    build before the owner's checks ([how](DEVTOOLS.md)), and the acceptance run on the toolbar's bug is next.

## v1: read and mark up

| Phase | What | Status |
|---|---|---|
| 0. Foundation spike | The solution, CI, the licence key, a window that shows a PDF, a signed and notarised package; the spec's §14 checks | built 2026-10-29; keyed run and the owner's checks on the Mac done 2026-10-29, except the Acrobat checks (best effort, issue #3); decisions answered 2026-10-30; first notarised pre-release `v0.0.1-spike4` passed Gatekeeper 2026-10-30; findings in the spec's §16 |
| 1. Standalone app | Viewing, markup (with its author written through PDFium), forms, printing, Preferences, Open Recent, the default PDF app | built 2026-10-30; the owner's checks on the Mac passed 2026-10-30, four after fixes; findings in the spec's §16 |
| 2. Healdan in CoffeePDF | The channel client, the window modes and the flows of spec §6, against a fake Helper | built 2026-10-31; the owner's checks on the Mac passed 2026-10-31, one after a fix; the owner's questions answered 2026-10-30; findings in the spec's §16 |
| 3. Healdan's milestone | Spec §4.4, through its own spec in Healdan's repository: Healdan's M3.6, "PDFs in CoffeePDF" | built 2026-11-01 in Healdan ([spec](https://github.com/Healdan-Project/healdan/blob/master/docs/superpowers/specs/2026-11-01-m3-6-pdfs-in-coffeepdf-design.md)), merged in Healdan-Project/healdan#150 and Healdan-Project/healdan#151; the owner's checks on Healdan Helper 0.5.0-rc1 passed 2026-11-01; Healdan Helper 0.5.0 released 2026-11-01; findings in the spec's §16 |
| 4. Acceptance | Spec §2.5 on the owner's Mac against the owner's server, then v1.0 | walkthrough passed 2026-11-01 on Healdan Helper 0.5.0-rc1 (M3.6's checks), with step 1 by a typed link, then from the web page after #151; Save as New Version with nothing to save fixed 2026-11-01; release `v0.3.0`, with the fix, passed Gatekeeper 2026-11-01; finding 3 (the full rewrite) settled 2026-11-02, nothing lost; its fix, a full save for unsigned PDFs, built 2026-11-03 (the spec's §16, Phase 3); on 2026-11-05, on the test build 0.3.1-fullsave.1, the owner's checks of that fix passed and the Save as New Version fix was checked, finding 2 was settled with no bug, and the owner decided that a save breaking a signature nested under a parent field does as now (a known limit for v1); left: v1.0 |

## After v1.0

| Piece | What | Status |
|---|---|---|
| CoffeePDF's own toolbar | The PdfViewer's toolbar hidden, and a row of CoffeePDF's own that drives the viewer through its public API ([spec](superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md)) | designed 2026-11-05; built on that date ([plan](superpowers/plans/2026-11-05-coffeepdf-toolbar.md)); Fluent's notice added to `THIRD-PARTY-NOTICES.md` 2026-11-06, after #16 merged; the owner's checks on the Mac passed 2026-11-06 on the test build 1.1.0-toolbar.5, check 1 after a fix (the colour swatch) and check 6 after a change: the PdfViewer can't restyle a selection from outside it, so the format menus hide while markup is selected (the spec's §10) |

## v2

Each gets its own design session first. Where the PdfViewer lacks a feature, CoffeePDF calls PDFium itself, and it
moves to the control's own when a newer PdfViewer adds the feature (the spec's §16 Decision 1). Their buttons go in
CoffeePDF's own toolbar (After v1.0).

- Page operations: rotate, delete, reorder, insert, combine, split and extract.
- Signatures and true redaction.
- Legal tooling: Bates numbering, exhibit stamps, a doc # footer stamp, bundles from several Healdan documents.

## Backlog

- **Restyle a selected markup from CoffeePDF's toolbar** once a PdfViewer allows it. The PdfViewer 12.0.1 ends a
  selection at any press outside it, and its style properties don't restyle a selection, though its docs say they do;
  its right-click menu does (the toolbar spec's §10, probes 8 and 8b, which show whether a newer PdfViewer fixes them).
  Both are worth reporting to Avalonia.

- **Remove the temporary patch to Avalonia's macOS library** once an Avalonia release fixes
  [AvaloniaUI/Avalonia#22285](https://github.com/AvaloniaUI/Avalonia/issues/22285), which is the cause of
  [issue #9](https://github.com/coffeemuse/CoffeePDF/issues/9). `packaging/avalonia-native/README.md` has the steps.
  Avalonia 12.1.5 (2026-11-02) doesn't fix it.
- **A writer for signed PDFs' updates,** so an undone markup doesn't stay in one: it rewrites only what follows the
  signed bytes, keeping only what the document uses. v1 leaves it as a known limit (the spec's §16, finding 3's fix).
- **A save that breaks a signature nested under a parent field.** The PdfViewer's save rewrites such a PDF whole, and
  v1 saves as it does: a known limit (owner, 2026-11-05; the spec's §16, finding 3's fix). It's in the note for
  Avalonia. Revisit it when a PdfViewer saves it incrementally, or with the writer above.
- OCR and comparing versions, which may belong on Healdan's server.
- Linux, after the Helper's Linux phase. Windows later. Both make their icons from the artwork in
  `packaging/icon/CoffeePDF.png`, as `packaging/mac/make-icon.sh` does the Mac's.

## Open decisions for the owner

- The licence, before the repository goes public (spec §12).
