#!/usr/bin/env python3
"""Sign/verify release artifacts offline with an owner-held P-256 key and OpenSSL."""
import argparse
import base64
import hashlib
import json
import pathlib
import subprocess
import tempfile
import zipfile


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
    return ("clankerworld.release-package.v1\npackage=" + proof["Package"] +
            "\nsha256=" + proof["PackageSha256"].lower() +
            "\nmanifest_sha256=" + proof["ManifestSha256"].lower() + "\n").encode("utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("sign", "verify"))
    parser.add_argument("package", type=pathlib.Path)
    parser.add_argument("key", type=pathlib.Path, help="Private key for sign; public key for verify. Never upload the private key.")
    args = parser.parse_args()
    sidecar = args.package.with_name(args.package.name + ".sig")
    proof = dict(Package=args.package.name, PackageSha256=digest(args.package), ManifestSha256=manifest_digest(args.package))
    with tempfile.TemporaryDirectory(prefix="clankerworld-release-signature-") as temporary:
        temporary = pathlib.Path(temporary)
        message = temporary / "payload"
        signature = temporary / "signature"
        message.write_bytes(payload(proof))
        if args.operation == "sign":
            # No key material is read by Python or printed by this command.
            subprocess.run(["openssl", "dgst", "-sha256", "-sign", str(args.key), "-out", str(signature), str(message)], check=True)
            proof["Signature"] = base64.b64encode(signature.read_bytes()).decode("ascii")
            # Never silently replace an existing owner's approval.
            with sidecar.open("x", encoding="utf-8") as target:
                json.dump(proof, target, indent=2)
                target.write("\n")
        else:
            if sidecar.stat().st_size > 4096:
                raise ValueError("Signature file is too large")
            recorded = json.loads(sidecar.read_text(encoding="utf-8"))
            if any(recorded.get(field) != value for field, value in proof.items()):
                raise ValueError("The signature does not describe this package")
            signature.write_bytes(base64.b64decode(recorded["Signature"], validate=True))
            subprocess.run(["openssl", "dgst", "-sha256", "-verify", str(args.key), "-signature", str(signature), str(message)], check=True)


if __name__ == "__main__":
    main()
