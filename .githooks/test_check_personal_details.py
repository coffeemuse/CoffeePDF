"""Tests for check-personal-details.py, the check both hooks run (CLAUDE.md, spec §16).

    python3 -I .githooks/test_check_personal_details.py

They stage files in a throwaway repository and run the check pre-commit runs, with made-up details, never the
account's. CI runs them on Linux. They're about the one exception: inside THIRD-PARTY-NOTICES.md's ```text blocks, which
hold other projects' notices word for word, the account name as a plain word doesn't count. They show it lets that
through and nothing else.
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

# Made-up details. The account name is also a name a notice might credit.
FULL, ACCOUNT, HOME = "Jordan Example", "jordan", "/Users/jordan"
DETAILS = (FULL, ACCOUNT, HOME)


def block(*lines):
    """A notice as THIRD-PARTY-NOTICES.md holds one: in a ```text block."""
    return "```text\n" + "".join(line + "\n" for line in lines) + "```\n"


# CoffeePDF's own prose, and a copied notice crediting someone who shares the account name, across a line break too.
PROSE = "## Some component\n\n- **Version:** 1.2.3.\n\n"
NOTICE = block("Copyright 2001 by Ada Lovelace, Jordan Someone, and", "Grace Hopper. Thanks to Jordan", "Another.")


class TheNoticesException(unittest.TestCase):
    """check_staged, as pre-commit runs it, on files staged in a throwaway repository."""

    def setUp(self):
        # A hook sets GIT_DIR and GIT_INDEX_FILE, which would point git at the real repository. Each change is undone
        # by a cleanup, which runs even when a later step here fails.
        for name, value in [(name, value) for name, value in os.environ.items() if name.startswith("GIT_")]:
            del os.environ[name]
            self.addCleanup(os.environ.__setitem__, name, value)
        repository = tempfile.TemporaryDirectory()
        self.addCleanup(repository.cleanup)
        self.addCleanup(os.chdir, os.getcwd())  # cleanups run last first, so this comes back out before the removal
        os.chdir(repository.name)
        subprocess.run(["git", "init", "-q"], check=True)

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

    def test_the_account_name_in_a_notice_passes(self):
        self.assertEqual(self.problems("THIRD-PARTY-NOTICES.md", PROSE + NOTICE + PROSE + NOTICE), [])

    def test_the_account_name_in_the_prose_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", PROSE + "Checked by Jordan.\n\n" + NOTICE),
                           "the account name")

    def test_the_account_name_after_a_notice_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", NOTICE + "Jordan\n" + NOTICE), "the account name")

    def test_a_notice_that_is_not_closed_is_checked(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", PROSE + "```text\nCopyright Jordan Someone\n"),
                           "the account name")

    def test_a_block_that_is_not_text_is_checked(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", "```sh\ncd /tmp # Jordan\n```\n"), "the account name")

    def test_the_full_name_in_a_notice_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", PROSE + block("Copyright " + FULL)), "the full name")

    def test_the_home_folder_in_a_notice_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", PROSE + block(HOME + "/Projects")), "the home folder")

    def test_the_home_folder_before_a_name_in_a_notice_is_refused(self):
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", PROSE + block(HOME + " Someone/notes")),
                           "the home folder")

    def test_the_account_name_in_utf16_in_a_notice_is_refused(self):
        content = ("```text\n" + "Copyright ").encode() + "Jordan".encode("utf-16-le") + "\n```\n".encode()
        self.assertRefused(self.problems("THIRD-PARTY-NOTICES.md", content), "the account name")

    def test_a_notice_in_another_file_is_refused(self):
        self.assertRefused(self.problems("docs/notes.md", PROSE + NOTICE), "the account name")

    def test_a_notice_in_a_notices_file_elsewhere_is_refused(self):
        self.assertRefused(self.problems("docs/THIRD-PARTY-NOTICES.md", PROSE + NOTICE), "the account name")


if __name__ == "__main__":
    unittest.main()
