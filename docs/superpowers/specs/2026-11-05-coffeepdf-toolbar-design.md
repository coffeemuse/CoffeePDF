# CoffeePDF's own toolbar

- **Date:** 2026-11-05
- **Status:** agreed with the owner section by section in a brainstorming session on 2026-11-05, and the owner approved
  this written spec the same day. Built on 2026-11-05
  (`docs/superpowers/plans/2026-11-05-coffeepdf-toolbar.md`). The owner's checks on the Mac are next (§7). Fluent's
  notice is in `THIRD-PARTY-NOTICES.md`, added on 2026-11-06 once coffeemuse/CoffeePDF#16 had created it (§8, §10).
- **Related:** the v1 spec (`2026-10-29-coffeepdf-v1-design.md`), its §4.3 (the app's parts), §6.1 (a window's mode),
  §8 (testing) and §16 Decision 1 (CoffeePDF does what the PdfViewer can't); `docs/ROADMAP.md`.

Throughout, **(owner)** marks something the owner decided. **(call)** marks a proposal the owner heard and approved as
part of a section.

## 1. Why

CoffeePDF needs buttons of its own in the toolbar, starting with v2's features. The PdfViewer (12.0.1) has no way to
add them (checked 2026-11-02, §10):

- No slot, items collection or content property exists for its toolbar. Its only toolbar settings are switches:
  `IsToolbarVisible`, `ToolbarLayoutMode`, `IsMoreOptionsVisible`, `IsPrintVisible` and the `Is…ToolVisible` flags.
- A new template is allowed, since only the page host is a required part. But the shipped template is compiled into
  the assembly and its source isn't in the package, so a new one would rebuild the toolbar, sidebar and overlays from
  nothing.
- Adding buttons to its template's panels at run time (`PART_RightCluster`, `PART_CenterToolbar`) would work, but the
  part names aren't a public contract, and its overflow menu mirrors its own buttons only.

What the PdfViewer does offer is a public API for everything its toolbar does. Its docs name this use: `SetToolCommand`
"lets a host with its own toolbar drive the same tools as the built-in one", and `PdfViewerTool` "covers every toolbar
tool, so a host that hides the built-in toolbar can drive them". So CoffeePDF hides the built-in toolbar and draws its
own. (owner)

## 2. Decisions (owner, 2026-11-05)

| Question | Decision |
|---|---|
| How | Hide the PdfViewer's toolbar and build CoffeePDF's own on the viewer's public API. The other three ways were a second row of our own beside the built-in one, a new template, and adding buttons to the template's panels (§1). |
| When | After v1.0 and before v2, so v2's features add their buttons to it from the start. v1.0 shipped first on the toolbar the owner's checks had passed. |
| Scope | Everything CoffeePDF uses from the built-in toolbar, the format controls included, so no feature is lost. Open, Save, Save As and the view modes stay in the menus, where they already are, and Share goes. |
| Placement | A row under the title bar, as now, not in the title bar as Preview has it. It works the same on every platform, leaves the title bar (and Healdan's `#1234 v2.1` title) to macOS, and touches nothing in the patched native library. Moving it into the title bar later changes where the toolbar is placed, not the toolbar. |
| Format controls | Small menus in the row, as the built-in toolbar has them. A second row was turned down: shown only when needed, it makes the page jump under the pointer when a markup is selected, and shown always, it costs every window a row. |
| Icons | Fluent UI System Icons, the regular 20-pixel set (MIT, Microsoft): the style of Avalonia's Fluent theme, which already draws CoffeePDF's own controls, and the largest of the sets compared. Lucide and Tabler were the others. Apple's SF Symbols were left out, since Apple licenses them only for apps on Apple platforms. |
| Build | A toolbar control of its own (§4). Toolbar items described as data, which a Customize Toolbar sheet would need, are more than this needs now. Building it into `DocumentWindow` would add several hundred lines to a window of about 1,050. |

## 3. What's on the toolbar (call)

One row under the title bar, below the Healdan bar when that shows:

- **Left:** the sidebar button, the page box and the zoom box.
  - The sidebar button shows and hides the sidebar, which keeps its own Thumbnails, Table of Contents and Bookmarks tabs.
    Hiding it remembers the mode it had, and showing it brings that mode back.
  - The page box shows the current page and the count ("3 of 12"), and takes a typed page number.
  - The zoom box shows the zoom as a percentage and takes a typed one. Its menu has Zoom In, Zoom Out, Actual Size,
    Zoom to Fit, Fit Width, and 50, 75, 100, 125, 150, 200 and 400%.
- **Centre:** the tools, in the Tools menu's order: Highlight, Underline, Strikethrough, Squiggly, Draw, Text, Shapes,
  Note, Link and Stamp.
  - Shapes has a menu of Line, Arrow, Rectangle, Circle, Polygon and Star. The button arms the last one chosen.
  - Stamp has a menu of the labels (`StampLabels`), the five styles (Classic, Flat, Outline, Legal, Pill) and Include
    Date. The button arms the last label chosen, and choosing a label arms it.
  - A second click on the armed tool disarms it (`ActiveTool = None`), so text selection comes back.
  - Redact stays hidden, as now: true redaction is v2's (v1 spec §2.2).
- **The format menus** follow the tools while a tool is armed, or a shape or text box is selected. They never overflow.

  | Armed tool, or selected markup | Format menus | The viewer's properties they show and set |
  |---|---|---|
  | Highlight, Underline, Strikethrough, Squiggly | Colour | `HighlightColor`, `UnderlineColor`, `StrikeoutColor`, `SquigglyColor` |
  | Note | Colour | `NoteColor` |
  | Stamp | Colour | `StampColor` |
  | Draw | Colour, Line | `DrawStrokeColor`, `DrawStrokeWidth` |
  | Shapes, or a selected shape | Colour (fill and stroke), Line | `ShapeFillColor`, `ShapeStrokeColor`, `ShapeStrokeWidth` |
  | Text, or a selected text box | Colour (fill, border and text), Line, Text | `TextAnnotationFillColor`, `TextAnnotationStrokeColor`, `TextAnnotationTextColor`, `TextAnnotationStrokeWidth`, `TextAnnotationFont`, `TextAnnotationFontSize`, `TextAnnotationFontAttributes`, `TextAnnotationTextAlign` |
  | Link, or no tool | none | |

  - **Colour:** a row of swatches, No Colour (for a fill, a border or a stroke), and a typed hex colour. A stamp's none
    is Label's Colour, which leaves each label its own colour. Shapes and text boxes have a tab for each colour they
    carry.
  - **Line:** the width, 0.5 to 20 points (the viewer's own range), with a preview.
  - **Text:** font, size, bold, italic, underline, and left, centre or right alignment.
  - While a shape or text box is selected, the viewer shows and edits its style through those same properties, and puts
    the defaults back when the selection ends (its docs on `ShapeStrokeWidth` and `TextAnnotationFont`).
- **Right:** search, then Print.
  - Typing in the search box searches (`SearchQuery`). Return finds the next match and ⇧Return the previous one, and
    Escape clears the box. While there's a query, the box shows the count ("3 of 17") and previous and next buttons.
    Its options menu has Match Case and Whole Word.
  - Print goes through the window, as ⌘P does (§4).
- **Narrow windows:** only the tools overflow. They collapse from the right into a "more" button at the end of their
  group, whose menu lists them, Shapes and Stamp with their own submenus. The window gets a minimum width, measured in
  the build, so the rest always fits.
- **Every button** has an accessible name and a tooltip, and the tooltip names the shortcut where the menu has one:
  Highlight (⌃⌘H), Underline (⌃⌘U), Strikethrough (⌃⌘S), Text (⌃⌘T) and Note (⌃⌘N).
- **Icons:** Fluent's regular 20-pixel shapes. Fluent has no stamp and no wavy underline, so CoffeePDF draws those two
  in its style (§10).

## 4. How it drives the viewer (call)

- **One control.** `CoffeePdf/Toolbar/DocumentToolbar.axaml` holds the row. The window hands it its PdfViewer when the
  window is built, and the toolbar touches the viewer only through public API.
  - **It reads** the viewer's properties as they change: `ActiveTool`, `SelectedShapeType`, `CurrentPage`,
    `PageCount`, `ZoomLevel`, `SearchResultCount`, `CurrentSearchResultIndex`, `SidebarMode`, `CanEditAnnotations` and
    `HasDocument`, and the colours of §3's table, which the Colour button's swatch shows (the viewer loads a selection's
    colours after its state shows). It reads the viewer's `:shape-selected` and `:editing-text` states, which the
    viewer documents for host styles, as they come and go, to show the format menus for a selection. The rest of the
    format properties, `StampLabels`, `StampStyle`, `IncludeStampDate`, `SearchMatchCase` and `SearchMatchWholeWord` it
    reads as their menus open.
  - **It sets** `ActiveTool`, `SelectedShapeType`, the format properties, `StampLabels`, `StampStyle`,
    `IncludeStampDate`, `CurrentPage`, `ZoomLevel` and `ZoomMode`, `SearchQuery`, `SearchMatchCase`,
    `SearchMatchWholeWord` and `SidebarMode`, and calls `FindNext` and `FindPrevious`.
- **The window's part.**
  - It sets `IsToolbarVisible="False"` on the viewer, beside `AutoSave` and `AllowDocumentSaving`, and puts the toolbar
    in `DocumentPanel`, docked at the top under the Healdan bar.
  - It gives the toolbar Print as the window's `PrintAndReportAsync`, so a failure is reported, not lost.
  - The window's mode (v1 spec §6.1) needs no wiring of its own. `ApplyMode` already sets `IsReadOnly`, so
    `CanEditAnnotations` turns false and the toolbar hides the tools and format menus, as the built-in toolbar does.
    The Healdan bar already says why.
- **The menu bar.**
  - Tools › Annotate and the toolbar both set `ActiveTool`, so the toolbar shows the armed tool whichever armed it.
  - Edit gets Find… (⌘F), which puts the cursor in the toolbar's search box. Find Next and Find Previous stay as they are.
  - Undo and Redo already act on a focused text box first (the owner's check 10), so the search, page and zoom boxes
    keep their own ⌘Z.
- **Overflow.** The tools sit in the viewer's `OverflowToolbarPanel`, which is public (§10). It takes its whole column
  and centres the tools in it itself. The format menus are marked `IsPinned`, so they stay while tools collapse. When
  `OverflowChanged` fires, the toolbar hides each child in `OverflowedChildren`, which the panel leaves drawn, taking
  clicks and the focus (it can't be made invisible, or the panel stops counting it), shows the rest, shows the "more"
  button only while something has overflowed, and rebuilds the "more" menu, whose items act on the buttons they stand
  for. The menu is rebuilt as it opens too, so its stamp checks are current.
- **Icons.** `CoffeePdf/Toolbar/Icons.axaml` is a resource dictionary of the shapes the toolbar uses, each under a name.
  `App.axaml` includes it, and the buttons draw them with `PathIcon`, which takes its colour from the theme.
- **Where the logic lives.** `CoffeePdf.Core` can't reference the PdfViewer's types, such as `PdfViewerTool`. So the
  small rules stay in the app, as static methods with no Avalonia state: which format menus a tool or selection gets,
  reading a typed zoom or page, and ordering `StampLabels` for a chosen label.

## 5. Edge cases (call)

- **No document yet, loading, or a load error.** The page, zoom, search, tool and Print controls are unavailable until
  `HasDocument`. The sidebar button stays available. The viewer's error banner and password prompt draw over the
  pages, not in the toolbar, so they don't change, nor does the window's hook on `PART_PasswordTextBox`.
- **Read-only, the Helper away, Healdan support off.** These follow `CanEditAnnotations` (§4). If the mode changes
  while a tool is armed, the toolbar sets `ActiveTool = None`, so no hidden tool stays armed.
- **Moving to Healdan's new copy (v1 spec §6.6).** The window sets `DocumentPanel.IsEnabled = false`, which takes the
  toolbar with it.
- **Typed values.** A typed zoom ("135%" or "135") is clamped to `MinZoom` and `MaxZoom`, and a typed page to 1 to
  `PageCount`. Anything else puts back the value shown before. A box follows the viewer, focused or not, until the
  person types in it, and leaving it commits only what was typed.
- **Light and dark.** The icons and controls take their colours from the Fluent theme's resources, so they follow the
  Mac's appearance, as the menus do.
- **The markup author.** Nothing changes. The toolbar arms the same tools as the Tools menu, and
  `DocumentWindow.SaveToAsync` writes the author on every markup type (v1 spec §16 Decision 1).
- **What stops appearing.** The built-in toolbar's Go to Page and custom zoom overlays open only from its own buttons.
  The page and zoom boxes take typed values instead.

## 6. Probes (call)

The docs don't settle five things. A keyed probe run answers them before any toolbar code is written (the plan's first
task), and the answers go in §10:

1. **The viewer's shortcuts with its toolbar hidden.** What ⌘F does, and whether any other key of the viewer's acts on
   a hidden control. If ⌘F goes to the hidden search box, Edit › Find… has to take the key first.
2. **Recolouring a highlight that's already there.** The docs say only that `HighlightColor` and its siblings colour new
   markups, and no state says a highlight is selected. As §3 stands, the Colour menu shows for those tools only while
   they're armed. If the probe finds a selected highlight, underline, strikethrough or squiggle that a state shows and
   those properties restyle, the Colour menu shows for it too. If not, §10 records the limit until a newer PdfViewer
   has it.
3. **Choosing a stamp's label.** The Stamp tool places "the last chosen label, or the first in `StampLabels`", and
   nothing public sets the chosen label. Expected: putting the chosen label first in `StampLabels` and arming Stamp
   places it. If not, the fallback is `AddStampAsync` at the point clicked.
4. **`OverflowToolbarPanel` outside the viewer's template.** That it collapses its children and reports
   `OverflowedChildren` as its docs say.
5. **The selection states.** That `:shape-selected` and `:editing-text` appear in `Viewer.Classes` and change as a shape
   or text box is selected and deselected, and what tells a selected text box from a shape, so the Text menu shows for
   the one and not the other.

## 7. Testing (call)

As v1 spec §8 has it: test-first, waiting for conditions, never for fixed times, and the owner's checks listed in the
pull request.

- **The probes:** `CoffeePdf.Tests/Toolbar/ToolbarProbes.cs`, `[PdfViewerFact]` tests with `Category=Probe` that print
  what they find.
- **The rules, without a window:** the format menus for each tool and selection, reading a typed zoom and page with
  their clamps, and ordering `StampLabels` for a chosen label.
- **The toolbar, headless:** `CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs`, `[PdfViewerFact]` tests with
  `Category=PdfViewer`, against a real window and PDF:
  - the built-in toolbar is hidden;
  - each tool button arms its tool, a second click disarms it, and the toolbar follows the Tools menu arming one;
  - the format menus show as §3's table says, for an armed tool, a selected shape and a text box being edited;
  - read-only hides the tools and disarms an armed one;
  - the page and zoom boxes move the viewer, and bad values are put back;
  - the search box searches and shows the count, and Edit › Find… puts the cursor in it;
  - Print goes through the window's reporting;
  - a narrow window moves tools into the "more" menu, and choosing one there arms it;
  - every button has an accessible name and a tooltip.
- **The tests already there pass,** among them the menu tests that arm tools and the password prompt's.
- **The owner's checks on the Mac,** listed in the pull request: the look in light and dark beside Preview's; the
  icons sharp on Retina; overflow while resizing; every tool marking up, with each saved markup showing the author in
  Preview; the format menus restyling a selected shape and text box; stamps' labels, styles and date; search and ⌘F;
  a Healdan read-only copy showing no tools; and VoiceOver reading the buttons.

## 8. Docs and licence (call)

- **With this spec:** the roadmap gets an "After v1.0" section above v2, with this toolbar as its first entry, and its
  v2 list says v2's features add their buttons to it.
- **With the build:**
  - The v1 spec's §4.3 bullet "the menu bar, with its Healdan menu, and the toolbar" points to this spec.
  - `CLAUDE.md` gets a rule beside the `AutoSave` one: the PdfViewer's own toolbar stays hidden, `DocumentToolbar`
    drives the viewer through its public API only, and every button has an icon from `Toolbar/Icons.axaml`, an
    accessible name and a tooltip.
  - **Not done in the build.** `THIRD-PARTY-NOTICES.md` and `package.sh`'s copy of it came in coffeemuse/CoffeePDF#16,
    then still open. Its `package.sh` stops a package whose notices don't name every native library, and copying its
    file here would have merged it by hand. So the build added neither, and Fluent's notice waited for #16 (§10).
  - **Done once #16 merged (2026-11-06).** This branch was brought up to date with main, and `THIRD-PARTY-NOTICES.md`
    got Fluent UI System Icons' section and row: version 1.1.344 and its MIT notice (© Microsoft Corporation). The npm
    package carries no licence file, so the notice is the repository's `LICENSE` at the tag `1.1.344`, as #16 does for
    such packages. The repository's `NOTICE` lists three tools its build uses, none of whose code is in the icons, so
    the app doesn't carry it. Stamp is CoffeePDF's own. Squiggly is drawn on Fluent's underline letter, so the section
    names it too. No `deps.json` lists the icons, so `check-notices.py` can't see them; `test_check_notices.py` checks
    instead that the section gives the version `make-icons.sh` fetches.
  - §10 gets the probes' answers and the build's findings.

## 9. Not in this design

- A Customize Toolbar sheet, or buttons a user adds or moves. §4's control can move to items described as data if it's
  ever wanted.
- The toolbar in the title bar (§2, Placement).
- v2's own buttons. Each comes with its feature's design.
- Linux and Windows. The row has nothing of macOS's, so it should carry over, but it's checked when those phases come.

## 10. Findings

What this design rests on, and what the probes and the build find. The probes' answers are added when they run.

- **No way to add buttons (checked 2026-11-02).** In `Avalonia.Controls.PdfViewer.xml` 12.0.1 and the assembly's
  strings, `PdfViewer` has no toolbar content, items or slot property. Its toolbar settings are the switches listed in
  §1.
- **The template is compiled (checked 2026-11-02).** The assembly holds `Themes/PdfViewerTemplate.axaml` only as
  compiled XAML (its `!AvaloniaResources` entries are `Build:` and `Populate:`), and no XAML text. Its docs say every
  template part but the page host is optional.
- **The public API covers the toolbar (checked 2026-11-02 and 2026-11-05).** Each control of §3 has a public property,
  command or method (§4), and `PdfViewerTool` has a value for every tool: `None`, `Highlight`, `Underline`,
  `Strikeout`, `Squiggly`, `Redact`, `Draw`, `Shape`, `Text`, `Note`, `Link` and `Stamp`. The viewer's states are
  pseudo-classes: `:annotating`, `:editing-text`, `:error`, `:form-field-focused`, `:has-document`, `:loading`,
  `:mobile`, `:read-only`, `:shape-selected`, `:sidebar-open` and `:text-selected`.
- **`OverflowToolbarPanel` is public (checked 2026-11-05).** The assembly's metadata marks
  `Avalonia.Controls.Pdf.Controls.OverflowToolbarPanel` public. The compiler writes XML docs for internal types too, so
  the docs alone didn't show it.
- **A stamp's label can't be chosen directly (checked 2026-11-05).** The public API has `StampLabels`, `StampStyle`,
  `StampColor` and `IncludeStampDate`, but nothing that sets the label the Stamp tool places (§6, probe 3).
- **Fluent's gaps (checked 2026-11-05).** The Fluent set has no stamp (`stamp_20_regular`) and no wavy underline
  (`text_underline_wavy_20_regular`).
- **Probe 1 (observed 2026-11-05, keyed run).** With the toolbar hidden, the viewer has no key bindings of its own.
  ⌘F moved nothing: focus stayed on the viewer, and zoom and tool were unchanged. ⌘+ zoomed in (0.909 to 1), so the
  viewer keeps its zoom key. ⌘0, H, N and Escape did nothing visible (zoom 1, tool None). So Edit › Find… (Task 4) has
  the key to itself, and no hidden search box takes the focus.
- **Probe 2 (observed 2026-11-05, keyed run).** A highlight already on the page does not show as selected. Clicking it
  left the viewer's states at focus, `:has-document` and pointer-over, with no selection state. The highlight's `Color`
  read back as none both before and after `HighlightColor` was set to blue, so the new colour did not reach it. The
  limit stands: CoffeePDF can't recolour a highlight on the page, and `Selection()` in Task 6 gets no state for it.
- **Probe 3 (observed 2026-11-05, keyed run).** `StampLabels` has twenty labels, from Approved to Copy. Putting Final
  (the fourth) first and clicking with the Stamp tool placed one stamp whose saved `/Name` is `Final`, which is
  `StampNameForLabel("Final")`. So the first label in `StampLabels` is the one the Stamp tool places, and no stop
  condition fired.
- **Probe 4 (observed 2026-11-05, keyed run).** With ten 30-wide buttons, an indicator and a pinned 60-wide button in an
  `OverflowToolbarPanel`: at 600 wide nothing overflowed, ten were visible, the indicator sat off the row (at 0,-15) and
  the pinned button was at x 430. At 200 wide, seven buttons (3 to 9) overflowed, three were visible, the indicator
  showed at x 102 and the pinned button at x 134, right after it. Back at 600 it returned to the first state.
  `OverflowChanged` fired once for each change (1 at 200, 2 on return). The panel collapses on its own, outside the
  viewer's template, as the plan expects.
- **Probe 5 (observed 2026-11-05, keyed run; probes 5 and 5b, eight runs each, identical but for timings).** Input
  needs a pointer move to the spot before the press: without it, headless clicks and drags were lost intermittently
  (probe 3's stamp failed to place in one of six early runs, and a Text-tool drag made a box in two of six). The probes'
  `Click` and `Drag` now move first, and every later run agreed. The page is also laid out twice (at its own size, then
  fitted to the window), so a click lands only once its bounds stop changing.
  *Shapes.* At the start the states are focus and `:has-document`. Drawing with the Shape tool left `SelectedShapeType`
  at Rectangle while armed. Disarming set it, and the tool, to None, and it stayed None while a shape was selected, so
  `SelectedShapeType` can't identify a selection. Clicking the edge of a drawn rectangle showed `:shape-selected` for the
  second rectangle (the Shape tool with `SelectedShapeType` set to `ShapeType.Text`, which ignored it and drew a plain
  rectangle, a `Square`) and showed nothing for the first rectangle's edge at x 200, y 460, so an edge click can miss.
  *Text.* A click with the Text tool created nothing (no annotation, tool back to None, no state). A drag with the Text
  tool, from page point 250,350 to 420,290, created a `FreeText` annotation of about 170 by 24 points at 250,326 to
  420,350 (not the dragged rectangle). Right after the drag the states were focus, `:has-document` and pointer-over,
  with focus on the viewer and no visible text box. After `KeyTextInput("Probe")`, `:editing-text` and `:shape-selected`
  both appeared, focus moved to a text box, and that text box's `Text` was "Probe", so edit mode was established by
  typing, not by the drag. `:editing-text` stayed after the tool was set to None, and a click on the box's centre kept
  it. A click on empty page ended it: the states cleared, the text box left the tree and focus went back to the viewer.
  Clicking the left edge of that existing box (x 250, mid-height) then showed `:shape-selected` alone, with no
  `:editing-text` and focus on the viewer. A click on an existing box's interior, outside editing, wasn't run.
  *What tells them apart.* While editing, `:editing-text` (with `:shape-selected`) says a text box. A selected existing
  text box and a selected shape both show only `:shape-selected`, and nothing in the states or `SelectedShapeType`
  tells them apart; only the annotation types from `GetAnnotationsAsync` (`FreeText`, `Square`) do.
- **Probe 6 (observed 2026-11-05, keyed run).** The built-in format menus offer six fonts in this order: Helvetica,
  Arial, Times New Roman, Courier, Georgia, Verdana. They offer fifteen sizes: 6, 8, 10, 12, 14, 16, 18, 20, 24, 28, 32,
  36, 48, 60 and 72. The colour swatch panel holds the palette in this order: black, #404040, grey (#808080), #BFBFBF,
  #DEDEDE, #EDEDED, white, then pastels #FF8080, #FFBF80, #FFEB80, #80E080, #80BFFF, #C080FF, #FF80FF, then strong colours
  #CC0000, #CC6600, #CCA300, #009900, #0066CC, #6600CC, #CC00CC. The defaults are: font Helvetica, size 12, highlight
  #FACD5A at half opacity, underline green, strikeout red, squiggly blue, note #FFF09A, draw red at width 2, shape fill
  white and stroke black at width 2, no stamp colour, zoom 0.05 to 5. Setting `SearchQuery` to "fixture" searched by
  itself: one result at index 0 (zero-based), `FindNext` stayed at 0 (one result), and `ClearSearch` gave an empty query,
  no results and index -1. So `SearchCountText` in Task 4 counts from `index + 1`, and shows nothing at -1.
- **The build (observed 2026-11-05).** The window's `MinWidth` is 800, measured headless with Avalonia's headless
  fonts, not the Mac's. The page count keeps the room "of 9999" takes, so the minimum holds for documents of up to
  9,999 pages. With that room kept and the Text tool armed (its three format menus are the widest, and never
  overflow), a sweep of widths kept the format menus clear of the search box down to 781, for a 1-page and a
  1,000-page document alike. But below 793 they ran past the tools' own column, which the panel draws past its edge,
  into the row's margin, so the minimum-width tests now ask for the column too: 793 passes and 792 fails. The first
  measure, 760, didn't keep the count's room, and a 1,000-page document overran it by 21 points (the final review's
  I3). The owner's resizing check on the Mac
  (§7) is what confirms it there.
  After the final review's fix wave, the keyed run passed 176 core tests and 232 app tests, and skipped two probes:
  the core's `PdfCompactionProbe`, which skips by itself, and the app's keyless probe. The keyless run passed 176 core
  tests and 70 app tests, and skipped the same core probe and 163 app tests that need the PdfViewer.
  What the final review found, and the fix wave fixed, each with a test that failed first:
  - C1: the panel arranges a child it moves off the row in an empty rect but leaves it drawn, taking clicks and the
    focus, and read by VoiceOver. Shapes and Stamp kept their split buttons' 32-point minimum height there and sat over
    Highlight's top half, so a click there armed Stamp. The toolbar now makes each overflowed child see-through, takes
    its clicks and focus away and drops it from the accessibility tree, and does the same to More while nothing has
    overflowed.
  - C2: the panel was centred in its column, so it was only ever given its desired width, and once More had shown it
    kept room for More there, so Stamp never came back. The panel now takes its whole column and centres the tools
    itself.
  - I1: the page and zoom boxes compared their text with the viewer's current page or zoom, and a focused box didn't
    follow the viewer, so a scroll, ⌘+ or a fit mode's re-fit while a box had the focus was undone when the focus left.
  - I2: the viewer resets `SelectedShapeType` when its tool is disarmed, so the Shapes button armed a rectangle
    whatever was chosen last. The toolbar remembers the shape.
  - Probe 4's indicator "off the row (at 0,-15)" was half on the row, not off it: a child in an empty rect keeps its
    minimum size, centred on the panel's corner, which is C1.
  What the build changed from the plan because a probe or a test said so:
  - `Selection()` reads only `:editing-text` and `:shape-selected`, not `SelectedShapeType` (probe 5).
  - A colour chosen from a swatch or typed keeps the slot's opacity, as the default highlight is at half opacity
    (probe 6).
  - The page and zoom boxes commit only what was typed, and Return gives focus back to the page, so a blur or Escape
    doesn't turn Fit into a manual zoom.
  - A Stamp label is chosen on a click over it, so the label shown as current can be chosen too, or on Return or
    Space. Moving the selection with the arrow keys doesn't choose (the final review).
  - There are 24 icons, not 26: Colour and Dismiss were unused.
  - Named flyouts (Zoom, Shapes, Stamp, More) are reached through their buttons' `Flyout`, as the compiler makes no
    field for them.
  - The test helpers move the pointer before a click, flush input around typing, wait for a settled page, and re-drag
    when headless input drops a drag (probe 5).
  The limits: a highlight already on the page can't be recoloured from the toolbar (probe 2). A selected text box
  that isn't being edited reads as a shape (probe 5), so it gets the shape's menus; whether they restyle it is for the
  owner's check. Fluent's notice was added on 2026-11-06, once #16 had merged (§8).
- **The owner's checks (2026-11-06, on the test package 1.1.0-toolbar.3).** Checks 1 to 5 passed, with two findings:
  - **Check 1, fixed.** In dark mode, the Colour button's swatch looked darker and muddier than the highlight it
    applies. The swatch drew the highlight's half-opaque colour, so it blended with the dark toolbar, while the
    highlight blends with the white page. Every colour sample now shows the colour as it looks on a white page
    (`ToolbarRules.OnPaper`). That covers the Colour button's swatch, and the Colour menu's swatches at the opacity
    choosing them gives. To check again on 1.1.0-toolbar.4.
  - **Check 5, by design.** Every markup showed its author in Preview but the link. A link isn't a markup annotation
    (ISO 32000 §12.5.6.2), so it has no author, and `MarkupAuthor` writes one only on markup annotations
    (`AnnotationSubtype.IsMarkup`). The saved test file held the author on its highlight, underline, text box,
    rectangle, note, stamp and drawing, and none on its link.
