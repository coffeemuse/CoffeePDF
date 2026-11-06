#!/bin/bash
# Writes Icons.axaml, the toolbar's icons (toolbar spec §3, §4): Fluent UI System Icons' regular 20-point shapes at a
# pinned version (MIT, © Microsoft Corporation; THIRD-PARTY-NOTICES.md), and the two CoffeePDF draws itself. Run it
# after changing the list below, and commit Icons.axaml. Needs curl and python3.
set -euo pipefail

version=1.1.344
here=$(cd "$(dirname "$0")" && pwd)
out="$here/Icons.axaml"

# The key in the dictionary, then Fluent's name for the icon.
icons=(
  "Icon.PanelLeft panel_left"
  "Icon.Highlight highlight"
  "Icon.Underline text_underline"
  "Icon.Strikethrough text_strikethrough"
  "Icon.Draw pen"
  "Icon.Text text_t"
  "Icon.Shapes shapes"
  "Icon.Note note"
  "Icon.Link link"
  "Icon.Search search"
  "Icon.Print print"
  "Icon.More more_horizontal"
  "Icon.ChevronDown chevron_down"
  "Icon.ChevronUp chevron_up"
  "Icon.Line line_thickness"
  "Icon.TextFormat text_font"
  "Icon.Bold text_bold"
  "Icon.Italic text_italic"
  "Icon.AlignLeft text_align_left"
  "Icon.AlignCentre text_align_center"
  "Icon.AlignRight text_align_right"
  "Icon.Options options"
)

# Fluent's underline letter, with a wave of a point's thickness in place of its bar.
squiggly="M 6 3.5 a .5 .5 0 0 0 -1 0 v 6.45 A 5 5 0 0 0 10 15 c 2.77 0 5 -2.26 5 -5.05 V 3.5 a .5 .5 0 0 0 -1 0 v 6.45 C 14 12.2 12.2 14 10 14 s -4 -1.8 -4 -4.05 z M 5 16.9 L 6.5 15.9 L 8 16.9 L 9.5 15.9 L 11 16.9 L 12.5 15.9 L 14 16.9 L 15 16.23 L 15 17.43 L 14 18.1 L 12.5 17.1 L 11 18.1 L 9.5 17.1 L 8 18.1 L 6.5 17.1 L 5 18.1 Z"
# A rubber stamp: a ring for the knob, the neck, the block, and the line it prints; even-odd, so the outlines are hollow.
stamp="F0 M 7 5 A 3 3 0 1 0 13 5 A 3 3 0 1 0 7 5 Z M 8 5 A 2 2 0 1 0 12 5 A 2 2 0 1 0 8 5 Z M 8.5 8 H 9.5 V 11 H 8.5 Z M 10.5 8 H 11.5 V 11 H 10.5 Z M 4 11 H 16 V 15 H 4 Z M 5 12 V 14 H 15 V 12 Z M 3 16.5 H 17 V 17.5 H 3 Z"

# Fluent's single path, spaced out: an SVG path may run numbers and an arc's flags together ("a1 1 0 011 1"), which
# Avalonia's path parser can misread.
path_of() {
  python3 -I -c '
import re, sys
paths = re.findall(r"<path[^>]*\sd=\"([^\"]+)\"", sys.stdin.read())
if len(paths) != 1:
    sys.exit(f"expected one path, found {len(paths)}")
d = paths[0]
commands = set("MmLlHhVvCcSsQqTtAaZz")
number = re.compile(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
out, i, command, argument = [], 0, "", 0
while i < len(d):
    c = d[i]
    if c.isspace() or c == ",":
        i += 1
    elif c in commands:
        command, argument = c, 0
        out.append(c)
        i += 1
    elif command in "Aa" and argument % 7 in (3, 4):
        out.append(c)  # an arc flag is one digit, 0 or 1
        argument += 1
        i += 1
    else:
        m = number.match(d, i)
        if not m:
            sys.exit(f"cannot read the path at {i}: {d[i:i+12]}")
        out.append(m.group())
        argument += 1
        i = m.end()
print(" ".join(out))'
}

{
  echo "<!-- Written by make-icons.sh from Fluent UI System Icons $version (MIT, © Microsoft Corporation; THIRD-PARTY-NOTICES.md), with Icon.Squiggly and Icon.Stamp drawn there. Don't edit it by hand. -->"
  echo '<ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'
  for entry in "${icons[@]}"; do
    key=${entry% *}
    name=${entry#* }
    d=$(curl -fsSL "https://cdn.jsdelivr.net/npm/@fluentui/svg-icons@$version/icons/${name}_20_regular.svg" | path_of)
    printf '  <StreamGeometry x:Key="%s">%s</StreamGeometry>\n' "$key" "$d"
  done
  printf '  <StreamGeometry x:Key="Icon.Squiggly">%s</StreamGeometry>\n' "$squiggly"
  printf '  <StreamGeometry x:Key="Icon.Stamp">%s</StreamGeometry>\n' "$stamp"
  echo '</ResourceDictionary>'
} > "$out.tmp"
mv "$out.tmp" "$out"
echo "Wrote $out"
