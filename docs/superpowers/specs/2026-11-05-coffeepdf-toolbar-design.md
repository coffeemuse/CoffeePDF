# CoffeePDF's own toolbar

- **Date:** 2026-11-05
- **Status:** agreed with the owner section by section in a brainstorming session on 2026-11-05, and the owner approved
  this written spec the same day. The implementation plan is `docs/superpowers/plans/2026-11-05-coffeepdf-toolbar.md`.
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

  - **Colour:** a row of swatches, No Colour (for a fill), and a typed hex colour. Shapes and text boxes have a tab for
    each colour they carry.
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
    `PageCount`, `ZoomLevel`, `ZoomMode`, `SearchResultCount`, `CurrentSearchResultIndex`, `SidebarMode`,
    `CanEditAnnotations` and `HasDocument`, and the format properties of §3's table. It reads the viewer's
    `:shape-selected` and `:editing-text` states, which the viewer documents for host styles, to show the format menus
    for a selection.
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
- **Overflow.** The tools sit in the viewer's `OverflowToolbarPanel`, which is public (§10). The format menus are marked
  `IsPinned`, so they stay while tools collapse. The "more" menu is rebuilt from `OverflowedChildren` whenever
  `OverflowChanged` fires, and its items act on the buttons they stand for.
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
  `PageCount`. Anything else puts back the value shown before.
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
  - `THIRD-PARTY-NOTICES.md` at the repository's root gets the MIT notice of Fluent UI System Icons (© Microsoft
    Corporation), and `packaging/mac/package.sh` copies the file into `Contents/Resources/`. The notices for what v1.0
    already bundles are a change of their own, and whichever lands first creates the file. The two icons CoffeePDF draws
    are its own and need no notice.
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
