"""Tests for check-personal-details.py, the check both hooks run (CLAUDE.md, spec §16).

    python3 -I .githooks/test_check_personal_details.py

They stage files in a throwaway repository and run the check pre-commit runs, with made-up details, never the
account's. CI runs them on Linux. They're about the one exception, THIRD-PARTY-NOTICES.md's two credits (owner,
2026-11-05), and show it lets those through and nothing else.
"""
import importlib.util
import os
import subprocess
import tempfile
import unittest

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("check_personal_details", os.path.join(_here, "check-personal-details.py"))
check = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(check)

# Made-up details. The account name is a first name, as a real one often is, so the credits can begin with it.
FULL, ACCOUNT, HOME = "Jordan Example", "jordan", "/Users/jordan"
DETAILS = (FULL, ACCOUNT, HOME)

# The two credits as the notices hold them, with that first name: FreeType's licence (as PDFium's copy has it, then
# SkiaSharp's, spaced out and across a line break) and ICU's.
CREDITS = (
    "                    Copyright 1996-2002, 2006 by\n"
    "          David Turner, Jordan Wilhelm, and Werner Lemberg\n"
    "  (David Turner,  Jordan Wilhelm,  and Werner Lemberg)\n"
    "  David Turner, Jordan\n"
    "  Wilhelm, and Werner Lemberg\n"
    "Eugene Wilson, Jordan Martin Campbell.\n"
)


class TheNoticesCredits(unittest.TestCase):
    """check_staged, as pre-commit runs it, on files staged in a throwaway repository."""

    def setUp(self):
        # A hook sets GIT_DIR and GIT_INDEX_FILE, which would point git at the real repository.
        self.git_environment = {name: value for name, value in os.environ.items() if name.startswith("GIT_")}
        for name in self.git_environment:
            del os.environ[name]
        self.cwd = os.getcwd()
        self.repository = tempfile.TemporaryDirectory()
        os.chdir(self.repository.name)
        subprocess.run(["git", "init", "-q"], check=True)

    def tearDown(self):
        os.chdir(self.cwd)
        self.repository.cleanup()
        os.environ.update(self.git_environment)

    def problems(self, path, content):
        """What the check says about the file staged with that content."""
        folder = os.path.dirname(path)
        if folder:
            os.makedirs(folder, exist_ok=True)
        with open(path, "wb") as file:
            file.write(content if isinstance(content, bytes) else content.encode("utf-8"))
        subprocess.run(["git", "add", "--", path], check=True)
        problems = check.check_staged(DETAILS)
        for problem in problems:
            self.assertNotIn(ACCOUNT, problem.lower(), "a refusal never prints the detail")
        return problems

    def assertRefused(self, problems, what):
        self.assertEqual(len(problems), 1, problems)
        self.assertIn(what, problems[0])

    def test_the_credits_pass_in_the_notices(self):
        self.assertEqual(self.problems("THIRD-PARTY-NOTICES.md", CREDITS), [])

    def test_the_account_name_anywhere_else_in_the_notices_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", CREDITS + "Copyright 2026 Jordan\n"),
                           "the account name")

    def test_the_account_name_before_another_surname_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", CREDITS + "Jordan Smith\n"), "the account name")

    def test_the_full_name_in_the_notices_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", CREDITS + FULL + "\n"), "the full name")

    def test_the_home_folder_in_the_notices_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", CREDITS + HOME + "/Projects\n"), "the home folder")

    def test_the_account_name_in_utf16_in_the_notices_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", CREDITS.encode() + "Jordan".encode("utf-16-le")),
                           "the account name")

    def test_the_credits_in_another_file_are_refused(self):
        self.assertRefused(self.problems("docs/notes.md", CREDITS), "the account name")

    def test_the_credits_in_a_notices_file_elsewhere_are_refused(self):
        self.assertRefused(self.problems("docs/THIRD-PARTY-NOTICES.md", CREDITS), "the account name")


if __name__ == "__main__":
    unittest.main()
