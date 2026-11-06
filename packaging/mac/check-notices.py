"""Checks THIRD-PARTY-NOTICES.md against what package.sh's publish put in the app (spec §12), so a package can't carry
a component, or a version of one, whose notice nobody added.

    python3 -I check-notices.py <deps.json> <THIRD-PARTY-NOTICES.md> <publish folder>

<deps.json> is the publish's own, which lists the .NET runtime pack and every package the app carries, with their
versions. For each one, exactly one section of the notices must name it, by its heading or in backticks in its
**Version:** line. That line must begin with the version the publish used, and so must the section's row in the table
at the top. Each native library in the publish folder must be named, in backticks, in a section. The notices' ```text
blocks, which hold other projects' texts, are left out, so nothing in them counts.

It prints each problem and exits 1 if there's any, or if it can't read its files: a check that can't finish passes
nothing.
"""
import json
import os
import re
import sys

BLOCK = re.compile(r"^```text\n.*?^```$", re.MULTILINE | re.DOTALL)
HEADING = re.compile(r"^## (.+?)[ \t]*$", re.MULTILINE)
# A **Version:** bullet runs over its indented lines, up to the next bullet or a blank line.
VERSION = re.compile(r"^- \*\*Version:\*\*[ \t]+(.*?)(?=^- |^[ \t]*$|\Z)", re.MULTILINE | re.DOTALL)
ROW = re.compile(r"^\| \[([^\]]+)\]\(#[^)]*\) \| ([^|]+?) \|", re.MULTILINE)


def sections(notices):
    """The table's rows as {component: version}, the sections as {heading: body}, and the headings that repeat."""
    text = BLOCK.sub("", notices)
    parts = HEADING.split(text)
    rows = dict(ROW.findall(parts[0]))
    found, repeated = {}, []
    for heading, body in zip(parts[1::2], parts[2::2]):
        if heading in found:
            repeated.append(heading)
        found[heading] = body
    return rows, found, repeated


def version_line(body):
    """The section's **Version:** text, or None."""
    match = VERSION.search(body)
    return " ".join(match.group(1).split()) if match else None


def version_given(body):
    """The version the section's **Version:** line begins with, such as 10.0.11 from "10.0.11, the …", or None."""
    line = version_line(body)
    return re.split(r"[\s,:;]", line, maxsplit=1)[0].rstrip(".") if line else None


def components(deps):
    """(name, version) for each runtime pack and package the publish carries files from."""
    target = deps["targets"][deps["runtimeTarget"]["name"]]
    carried = []
    for key, library in deps["libraries"].items():
        if library["type"] == "project":
            continue
        assets = target.get(key, {})
        if not any(assets.get(kind) for kind in ("runtime", "native", "resources")):
            continue  # a package with no files of its own in the app
        name, version = key.rsplit("/", 1)
        if library["type"] == "runtimepack":
            name = name[len("runtimepack."):] if name.startswith("runtimepack.") else name
        carried.append((name, version))
    return sorted(carried)


def problems(deps, notices, libraries):
    rows, found, repeated = sections(notices)
    said = ["the heading \"## %s\" comes more than once" % heading for heading in repeated]
    for name, version in components(deps):
        named = [heading for heading, body in found.items()
                 if heading == name or "`%s`" % name in (version_line(body) or "")]
        if not named:
            said.append("the app carries %s %s, which no section names, by its heading or in its **Version:** line: add "
                        "its section, with the notice copied from that version, and its row in the table" % (name, version))
            continue
        if len(named) > 1:
            said.append("%s is named by more than one section (%s)" % (name, ", ".join(named)))
            continue
        heading = named[0]
        given = version_given(found[heading])
        if given != version:
            said.append("the app carries %s %s, but the section \"%s\" gives %s: update it, with the notice copied from "
                        "%s, and its row" % (name, version, heading, given or "no **Version:**", version))
        if rows.get(heading) != version:
            said.append("the app carries %s %s, but the table's row for \"%s\" gives %s"
                        % (name, version, heading, rows.get(heading, "nothing (there's no row)")))
    for library in libraries:
        if not any("`%s`" % library in body for body in found.values()):
            said.append("the publish has %s, which no section names: add its component's section" % library)
    return said


def main(arguments):
    if len(arguments) != 3:
        print("usage: check-notices.py <deps.json> <THIRD-PARTY-NOTICES.md> <publish folder>", file=sys.stderr)
        return 2
    deps_path, notices_path, publish = arguments
    try:
        with open(deps_path, encoding="utf-8") as file:
            deps = json.load(file)
        with open(notices_path, encoding="utf-8") as file:
            notices = file.read()
        libraries = sorted(name for name in os.listdir(publish) if name.endswith(".dylib"))
        said = problems(deps, notices, libraries)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print("THIRD-PARTY-NOTICES.md couldn't be checked (%s), so it doesn't pass" % type(error).__name__, file=sys.stderr)
        return 1
    for problem in said:
        print("THIRD-PARTY-NOTICES.md: %s." % problem, file=sys.stderr)
    return 1 if said else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
