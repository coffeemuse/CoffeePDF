# CoffeePDF roadmap

- **Status:** v1 designed on 2026-10-29 ([spec](superpowers/specs/2026-10-29-coffeepdf-v1-design.md)). Phase 0 is
  built ([plan](superpowers/plans/2026-10-29-phase-0-foundation-spike.md)), and its keyed run is done. Phase 1's plan
  waits on the owner's checks on the Mac, the owner's decisions in the spec's §16, and the save-pattern question in its
  row 2.

## v1: read and mark up

| Phase | What | Status |
|---|---|---|
| 0. Foundation spike | The solution, CI, the licence key, a window that shows a PDF, a signed and notarised package; the spec's §14 checks | built 2026-10-29; keyed run done 2026-10-29; the owner's checks on the Mac pending; findings in the spec's §16 |
| 1. Standalone app | Viewing, markup, forms, printing, Preferences, Open Recent, the default PDF app | not started |
| 2. Healdan in CoffeePDF | The channel client, the window modes and the flows of spec §6, against a fake Helper | not started |
| 3. Healdan's milestone | Spec §4.4, through its own spec in Healdan's repository | not started |
| 4. Acceptance | Spec §2.5 on the owner's Mac against the owner's server, then v1.0 | not started |

## v2

Each gets its own design session first.

- Page operations: rotate, delete, reorder, insert, combine, split and extract.
- Signatures and true redaction.
- Legal tooling: Bates numbering, exhibit stamps, a doc # footer stamp, bundles from several Healdan documents.

## Backlog

- OCR and comparing versions, which may belong on Healdan's server.
- Linux, after the Helper's Linux phase. Windows later.

## Open decisions for the owner

- The licence, before the repository goes public (spec §12).
- Phase 0's decisions (the markup author, and builds without the key), and the owner's to-do list for the pending checks: the spec's §16.
