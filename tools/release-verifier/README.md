# Release approval verifier

This small program uses the official `sigstore/sigstore-go` library to verify
release approvals offline. Its executable is bundled with the Windows launcher;
players do not install Go, Cosign or any signing tools.

The executable accepts only a payload file and a bundle file. It pins the GitHub
Actions issuer and this repository's main release workflows. It requires an
artifact signature, certificate transparency, a verified transparency-log entry
and a signed verification time. It exposes no identity, trust-root or unsafe
verification switches.

`trusted-root.json` was obtained from
<https://github.com/sigstore/root-signing/blob/main/targets/trusted_root.json>
on October 18, 2026. SHA-256:
`6494e21ea73fa7ee769f85f57d5a3e6a08725eae1e38c755fc3517c9e6bc0b66`.
Update it through review when Sigstore rotates its public trust material.
These are public verification certificates and keys, not a signing secret.

The tests include Cosign 3.1.3's public Windows binary bundle from
<https://github.com/sigstore/cosign/releases/tag/v3.1.3>. The binary's SHA-256 is
pinned independently in the test. They check real certificate/log verification,
tampered artifact hashes, missing log evidence, the exact workflow identity
policy and refusal of certificates without certificate-transparency evidence.
No live account, private key or network is used by the tests.

`scripts/build-release-verifier.sh` pins Go 1.27.2 and verifies its download.
The module and checksum files pin dependencies. The build also copies the Go
license and licenses/notices for every compiled dependency beside the executable.
