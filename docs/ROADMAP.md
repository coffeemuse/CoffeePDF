# CoffeePDF roadmap

- **Status:** v1 designed on 2026-10-29 ([spec](superpowers/specs/2026-10-29-coffeepdf-v1-design.md)). Phase 0 is
  built ([plan](superpowers/plans/2026-10-29-phase-0-foundation-spike.md)), its keyed run and most of the owner's
  checks on the Mac are done, and the owner answered its decisions on 2026-10-30 (the spec's §16). Phase 1's plan is
  next.
  - The first signed, notarised pre-release is out, `v0.0.1-spike4` (2026-10-30). Gatekeeper's first open of it is
    the owner's check ([Releasing](RELEASING.md)).
  - The two checks that need Acrobat are best effort, in
    [issue #3](https://github.com/coffeemuse/CoffeePDF/issues/3), and hold no phase.

## v1: read and mark up

| Phase | What | Status |
|---|---|---|
| 0. Foundation spike | The solution, CI, the licence key, a window that shows a PDF, a signed and notarised package; the spec's §14 checks | built 2026-10-29; keyed run and the owner's checks on the Mac done 2026-10-29, except Gatekeeper and the Acrobat checks (best effort, issue #3); decisions answered 2026-10-30; first notarised pre-release `v0.0.1-spike4` 2026-10-30; findings in the spec's §16 |
| 1. Standalone app | Viewing, markup (with its author written through PDFium), forms, printing, Preferences, Open Recent, the default PDF app | not started; its plan is next |
| 2. Healdan in CoffeePDF | The channel client, the window modes and the flows of spec §6, against a fake Helper | not started |
| 3. Healdan's milestone | Spec §4.4, through its own spec in Healdan's repository, including the Helper's watch handling a save by rename | not started |
| 4. Acceptance | Spec §2.5 on the owner's Mac against the owner's server, then v1.0 | not started |

## v2

Each gets its own design session first. Where the PdfViewer lacks a feature, CoffeePDF calls PDFium itself, and it
moves to the control's own when a newer PdfViewer adds the feature (the spec's §16 Decision 1).

- Page operations: rotate, delete, reorder, insert, combine, split and extract.
- Signatures and true redaction.
- Legal tooling: Bates numbering, exhibit stamps, a doc # footer stamp, bundles from several Healdan documents.

## Backlog

- OCR and comparing versions, which may belong on Healdan's server.
- Linux, after the Helper's Linux phase. Windows later.

## Open decisions for the owner

- The licence, before the repository goes public (spec §12).
