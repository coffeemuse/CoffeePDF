"""Keeps the owner's personal details out of CoffeePDF's history (CLAUDE.md, spec §16).

Both hooks call it, through python3 -I:

    check-personal-details.py staged          what pre-commit runs, on the files staged for the commit
    check-personal-details.py message FILE    what commit-msg runs, on the commit message in FILE

Any problem refuses the commit, and so does anything that stops the check from running: a staged file git can't read
back is refused, not skipped. The details are read here, at commit time, so they're written nowhere, and what's printed
says which kind was found and in which file, never the detail itself.
"""
import os
import re
import subprocess
import sys
import unicodedata

try:
    import pwd
except ImportError:  # Windows has no passwd database; `id` is tried first anyway
    pwd = None


def run(*command):
    """The command's output, or an empty string if there's no such command or it fails. A command that hangs raises, and
    so refuses the commit: a name that couldn't be read is a name that wasn't checked."""
    try:
        result = subprocess.run(command, capture_output=True, timeout=30)
    except OSError:
        return ""
    return result.stdout.decode("utf-8", "replace").strip() if result.returncode == 0 else ""


def own_details():
    """The account's full name, account name and home folder, as (full, account, home)."""
    account = run("id", "-un")
    full = run("/usr/bin/id", "-F")
    if not full and pwd is not None and account:
        try:
            full = pwd.getpwnam(account).pw_gecos.split(",")[0].strip()
        except KeyError:
            pass
    home = os.environ.get("HOME") or os.path.expanduser("~")
    return full, account, home


def variants(text):
    """The text as it is, in lower, upper and capitalised case, and in both Unicode normal forms (macOS file names are
    decomposed)."""
    found = set()
    for form in ("NFC", "NFD"):
        normal = unicodedata.normalize(form, text)
        found.update((normal, normal.lower(), normal.upper(), normal.capitalize(), normal.title()))
    return found


def forms(text):
    """Every way the text can sit in a file: UTF-8, UTF-16 either way round and Latin-1 (which is how PDFium writes
    an author that fits in it), each also as lower- and upper-case hex, which is how a PDF can hold a string."""
    found = set()
    for variant in variants(text):
        for encoding in ("utf-8", "utf-16-be", "utf-16-le", "latin-1"):
            try:
                raw = variant.encode(encoding)
            except UnicodeEncodeError:
                continue
            found.update((raw, raw.hex().encode(), raw.hex().upper().encode()))
    return found


def details_in(data, details):
    """Which details the bytes hold, as phrases such as "the full name"."""
    full, account, home = details
    found = []
    for what, value in (("the full name", full), ("the home folder", home)):
        if len(value) > 1 and any(form in data for form in forms(value)):
            found.append(what)
    if len(account) > 1:
        # A short or common account name is a word in ordinary text, so its plain form only counts as a whole word. Its
        # other forms are plain substrings, but only from three characters, so a short name can't refuse everything.
        word = rb"(?i)(?<![A-Za-z0-9])" + re.escape(account.encode("utf-8")) + rb"(?![A-Za-z0-9])"
        plain = {variant.encode("utf-8") for variant in variants(account)}
        if re.search(word, data) or (len(account) >= 3 and any(form in data for form in forms(account) - plain)):
            found.append("the account name")
    return " and ".join(found)


def check_staged(details):
    """Refuses a PDF, by its name or by its content, and any staged file or file name holding a detail."""
    listing = subprocess.run(
        ["git", "diff", "--cached", "--name-only", "-z", "--diff-filter=ACMRT"], capture_output=True, check=True
    )
    problems = []
    for path in filter(None, listing.stdout.split(b"\0")):  # -z: names come raw, whatever characters they hold
        name = path.decode("utf-8", "replace")
        held = details_in(path, details)
        if held:
            problems.append("a staged file's name contains %s (not shown). Rename it, and stage it again." % held)
            continue
        if path.lower().endswith(b".pdf"):
            problems.append("%s is a PDF. PDFs aren't committed to CoffeePDF, since a saved one carries its markup author." % name)
            continue
        try:
            # ":0:" is the index's copy, however the name looks.
            blob = subprocess.run(["git", "show", "--no-textconv", b":0:" + path], capture_output=True, check=True).stdout
        except subprocess.CalledProcessError:
            problems.append("%s can't be read from the index, so it can't be checked. Unstage it." % name)
            continue
        if blob.startswith(b"%PDF-"):
            problems.append("%s is a PDF, whatever its name. PDFs aren't committed to CoffeePDF, since a saved one carries its markup author." % name)
            continue
        held = details_in(blob, details)
        if held:
            problems.append("%s contains %s (not shown). Take it out, and stage the file again." % (name, held))
    return problems


def check_message(path, details):
    """Refuses a commit message holding a detail."""
    with open(path, "rb") as message:
        held = details_in(message.read(), details)
    return ["the commit message contains %s (not shown). Take it out, and commit again." % held] if held else []


def main(arguments):
    staged = arguments == ["staged"]
    if not staged and not (len(arguments) == 2 and arguments[0] == "message"):
        print("usage: check-personal-details.py staged | message FILE", file=sys.stderr)
        return 2
    hook = "pre-commit" if staged else "commit-msg"
    try:
        details = own_details()
        problems = check_staged(details) if staged else check_message(arguments[1], details)
    except Exception as error:  # the check couldn't finish, and an unfinished check passes nothing
        print("%s: the personal-details check couldn't finish (%s), so the commit is refused." % (hook, type(error).__name__), file=sys.stderr)
        return 1
    for problem in problems:
        print("%s: %s" % (hook, problem), file=sys.stderr)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
