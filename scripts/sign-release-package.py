#!/usr/bin/env python3
"""Approve release artifacts with the protected GitHub Actions signing identity."""
import argparse
import hashlib
import json
import os
import pathlib
import subprocess
import tempfile
import zipfile

ISSUER = "https://token.actions.githubusercontent.com"
WORKFLOWS = tuple("https://github.com/ClankerWorldOrg/ClankerWorld/.github/workflows/" + name +
                  ".yml@refs/heads/main" for name in ("nightly", "release"))
MAXIMUM_BYTES = 262144


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def manifest_digest(path):
    if path.suffix != ".zip":
        return hashlib.sha256(b"").hexdigest()
    with zipfile.ZipFile(path) as archive:
        entry = archive.getinfo(path.stem + "/manifest.sha256")
        if entry.file_size > 4 * 1024 * 1024:
            raise ValueError("Package manifest is too large")
        return hashlib.sha256(archive.read(entry)).hexdigest()


def payload(proof):
    return ("clankerworld.release-package.v2\npackage=" + proof["Package"] +
            "\nsha256=" + proof["PackageSha256"].lower() +
            "\nmanifest_sha256=" + proof["ManifestSha256"].lower() + "\n").encode("utf-8")


def signing_identity():
    identity = "https://github.com/" + os.environ.get("GITHUB_WORKFLOW_REF", "")
    if (os.environ.get("GITHUB_ACTIONS") != "true" or
        os.environ.get("GITHUB_REPOSITORY") != "ClankerWorldOrg/ClankerWorld" or
        os.environ.get("GITHUB_REF") != "refs/heads/main" or
        os.environ.get("GITHUB_EVENT_NAME") not in ("schedule", "workflow_dispatch") or
        identity not in WORKFLOWS):
        raise ValueError("Release signing runs only in the protected main release workflow")
    return identity


def verify_bundle(cosign, message, bundle, identity):
    trusted_root = pathlib.Path(__file__).resolve().parents[1] / "tools/release-verifier/trusted-root.json"
    subprocess.run([cosign, "verify-blob", str(message), "--bundle", str(bundle),
                    "--trusted-root", str(trusted_root), "--offline",
                    "--certificate-identity", identity, "--certificate-oidc-issuer", ISSUER], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("sign", "verify"))
    parser.add_argument("package", type=pathlib.Path)
    parser.add_argument("--cosign", default="cosign")
    parser.add_argument("--verifier", type=pathlib.Path, help="Use the bundled launcher verifier for verification")
    args = parser.parse_args()
    sidecar = args.package.with_name(args.package.name + ".sig")
    if args.operation == "sign" and sidecar.exists():
        raise ValueError("This artifact already has an approval")
    proof = dict(Package=args.package.name, PackageSha256=digest(args.package), ManifestSha256=manifest_digest(args.package))
    with tempfile.TemporaryDirectory(prefix="clankerworld-release-signature-") as temporary:
        temporary = pathlib.Path(temporary)
        message, bundle = temporary / "payload", temporary / "bundle.json"
        message.write_bytes(payload(proof))
        if args.operation == "sign":
            identity = signing_identity()
            subprocess.run([args.cosign, "sign-blob", "--yes", "--bundle", str(bundle), str(message)], check=True)
            if bundle.stat().st_size > MAXIMUM_BYTES // 2:
                raise ValueError("Approval bundle is too large")
            verify_bundle(args.cosign, message, bundle, identity)
            proof["Signature"] = bundle.read_text(encoding="utf-8")
            encoded = json.dumps(proof, indent=2) + "\n"
            if len(encoded.encode("utf-8")) > MAXIMUM_BYTES:
                raise ValueError("Release approval is too large")
            with sidecar.open("x", encoding="utf-8") as target:
                target.write(encoded)
        else:
            if sidecar.stat().st_size > MAXIMUM_BYTES:
                raise ValueError("Signature file is too large")
            recorded = json.loads(sidecar.read_text(encoding="utf-8"))
            if any(recorded.get(field) != value for field, value in proof.items()):
                raise ValueError("The signature does not describe this package")
            bundle.write_text(recorded["Signature"], encoding="utf-8")
            if args.verifier:
                subprocess.run([str(args.verifier.resolve()), str(message), str(bundle)], check=True)
                return
            # Exactly the pinned workflows are accepted, never an arbitrary GitHub identity.
            for identity in WORKFLOWS:
                try:
                    verify_bundle(args.cosign, message, bundle, identity)
                    break
                except subprocess.CalledProcessError:
                    if identity == WORKFLOWS[-1]:
                        raise


if __name__ == "__main__":
    main()
