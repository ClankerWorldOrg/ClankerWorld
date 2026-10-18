"""Checks workflow authorization and fail-closed artifact publication without OIDC."""
import hashlib
import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest import mock

spec = importlib.util.spec_from_file_location("release_signing", pathlib.Path(__file__).with_name("sign-release-package.py"))
signing = importlib.util.module_from_spec(spec)
spec.loader.exec_module(signing)


class ReleaseSigningTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.package = pathlib.Path(self.temporary.name) / "ClankerWorld-0.1.0-alpha.2-windows-x64.zip"
        with zipfile.ZipFile(self.package, "w") as archive:
            archive.writestr(self.package.stem + "/manifest.sha256", "# exact build manifest\n")
        self.context = dict(GITHUB_ACTIONS="true", GITHUB_REPOSITORY="ClankerWorldOrg/ClankerWorld",
                            GITHUB_REF="refs/heads/main", GITHUB_EVENT_NAME="workflow_dispatch",
                            GITHUB_WORKFLOW_REF="ClankerWorldOrg/ClankerWorld/.github/workflows/nightly.yml@refs/heads/main")

    def call(self, operation):
        with mock.patch.object(sys, "argv", ["sign-release-package.py", operation, str(self.package)]):
            signing.main()

    def fake_cosign(self, arguments, check):
        self.assertTrue(check)
        bundle = pathlib.Path(arguments[arguments.index("--bundle") + 1])
        if arguments[1] == "sign-blob":
            digest = hashlib.sha256(pathlib.Path(arguments[-1]).read_bytes()).hexdigest()
            bundle.write_text(json.dumps(dict(payload=digest)))
        elif arguments[1] == "verify-blob":
            self.assertIn("--offline", arguments)
            self.assertIn("--trusted-root", arguments)
            self.assertEqual(arguments[arguments.index("--certificate-oidc-issuer") + 1], signing.ISSUER)
            self.assertIn(arguments[arguments.index("--certificate-identity") + 1], signing.WORKFLOWS)
            self.assertEqual(json.loads(bundle.read_text())["payload"], hashlib.sha256(pathlib.Path(arguments[2]).read_bytes()).hexdigest())
        else:
            self.fail("unexpected verifier operation")

    def test_only_protected_main_release_context_can_sign(self):
        for field, value in (("GITHUB_ACTIONS", "false"), ("GITHUB_REPOSITORY", "fork/ClankerWorld"),
                             ("GITHUB_REF", "refs/pull/1/merge"), ("GITHUB_EVENT_NAME", "pull_request"),
                             ("GITHUB_WORKFLOW_REF", "ClankerWorldOrg/ClankerWorld/.github/workflows/ci.yml@refs/heads/main")):
            with self.subTest(field=field), mock.patch.dict(os.environ, {**self.context, field: value}), mock.patch.object(signing.subprocess, "run") as command:
                with self.assertRaises(ValueError): self.call("sign")
                command.assert_not_called()
                self.assertFalse(pathlib.Path(str(self.package) + ".sig").exists())

    def test_approval_is_verified_before_writing_and_cannot_be_replaced(self):
        with mock.patch.dict(os.environ, self.context), mock.patch.object(signing.subprocess, "run", side_effect=self.fake_cosign) as command:
            self.call("sign")
            self.assertEqual([call.args[0][1] for call in command.call_args_list], ["sign-blob", "verify-blob"])
            self.call("verify")
            before = pathlib.Path(str(self.package) + ".sig").read_bytes()
            with self.assertRaises(ValueError): self.call("sign")
            self.assertEqual(before, pathlib.Path(str(self.package) + ".sig").read_bytes())
            with self.package.open("ab") as target: target.write(b"changed")
            with self.assertRaises(ValueError): self.call("verify")

    def test_failed_signing_or_verification_leaves_no_approval(self):
        for failed_operation in ("sign-blob", "verify-blob"):
            def fail(arguments, check):
                if arguments[1] == failed_operation: raise subprocess.CalledProcessError(1, arguments)
                self.fake_cosign(arguments, check)
            with self.subTest(operation=failed_operation), mock.patch.dict(os.environ, self.context), mock.patch.object(signing.subprocess, "run", side_effect=fail):
                with self.assertRaises(subprocess.CalledProcessError): self.call("sign")
                self.assertFalse(pathlib.Path(str(self.package) + ".sig").exists())


if __name__ == "__main__":
    unittest.main()
