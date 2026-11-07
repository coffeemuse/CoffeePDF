#!/usr/bin/env bash
# Starts a test build of CoffeePDF walled off, for Claude's pass before a hand-off (docs/DEVTOOLS.md, the devtools
# spec's §6.3). Its settings, log and Helper folders go in <folder>, the settings giving the markup author Test Reviewer
# and having answered the first launch's Healdan question, so nothing touches the owner's settings, Helper or documents.
#
#   packaging/mac/launch-test-build.sh <folder> [<file.pdf> ...]
#   packaging/mac/launch-test-build.sh --quit <folder>
#
# <folder> must exist and be outside the repository, such as Claude's scratchpad. Each PDF must be a file inside it, so
# a save in the pass changes only the pass's own copy. Each launch writes its settings afresh, so a pass starts from the
# same state.
#
# Environment (optional):
#   APP   the CoffeePDF.app to start: packaging/mac/out/CoffeePDF-devtools.app by default, which
#         COFFEEPDF_DEVTOOLS=1 package.sh builds. --quit takes the same one.
#
# It runs the bundle's binary directly, not through `open`, which would let LaunchServices hand the files to a running
# CoffeePDF with the same bundle identifier. It ends by exec-ing the binary, so the process id it prints is the app's,
# which avdt's attach-to-app takes. It also writes that id to <folder>/coffeepdf.pid. It runs until the app quits, so
# start it in the background (Claude: Bash's run_in_background). The app's output goes to <folder>/app.log.
#
# --quit ends it, but only while that id is still the test build's binary: the file outlives a crash or a ⌘Q, and the
# id may since belong to another process.
set -euo pipefail

die() { printf 'launch-test-build.sh: %s\n' "$*" >&2; exit 1; }

[ "$#" -ge 1 ] || die "usage: launch-test-build.sh <folder> [<file.pdf> ...], or --quit <folder>"
here=$(cd "$(dirname "$0")" && pwd -P)
repo=$(cd "$here/../.." && pwd -P)
. "$here/devtools.sh"
app=${APP:-"$here/out/CoffeePDF-devtools.app"}
[ -x "$app/Contents/MacOS/CoffeePDF" ] ||
  die "$app/Contents/MacOS/CoffeePDF isn't there: build it with COFFEEPDF_DEVTOOLS=1 packaging/mac/package.sh <version>"
binary="$(cd "$app/Contents/MacOS" && pwd -P)/CoffeePDF"

if [ "$1" = --quit ]; then
  [ "$#" -eq 2 ] || die "usage: launch-test-build.sh --quit <folder>"
  pidfile="$2/coffeepdf.pid"
  [ -f "$pidfile" ] || die "$pidfile isn't there, so nothing this script started is running from that folder"
  pid=$(cat "$pidfile")
  if [ "$(ps -p "$pid" -o comm= 2>/dev/null || true)" = "$binary" ]; then
    kill "$pid"
    printf 'Ended CoffeePDF, process %s\n' "$pid"
  else
    printf 'Process %s is no longer %s, so nothing was ended\n' "$pid" "$binary"
  fi
  rm -f "$pidfile"
  exit 0
fi

[ -d "$1" ] || die "$1 isn't a folder: give one that exists, outside the repository"
folder=$(cd "$1" && pwd -P)
case "$folder/" in
  "$repo"/*) die "the folder must be outside the repository, so no PDF or setting lands in it" ;;
esac
if git -C "$folder" rev-parse --is-inside-work-tree > /dev/null 2>&1; then
  die "the folder must be outside any git work tree, so no PDF or setting lands in one"
fi
shift
for file in "$@"; do
  [ -f "$file" ] && [ ! -L "$file" ] || die "$file isn't a file: give the pass's own PDFs, inside $folder"
  case "$(cd "$(dirname "$file")" && pwd -P)/" in
    "$folder"/*) ;;
    *) die "$file must be inside $folder, so a save in the pass changes only the pass's own copy, never a document" ;;
  esac
done
if ! carries_devtools "$binary"; then
  printf "warning: %s doesn't carry the developer tools, so avdt can't reach it (build it with COFFEEPDF_DEVTOOLS=1)\n" \
    "$app" >&2
fi

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
