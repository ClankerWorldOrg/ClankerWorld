# Security policy

ClankerWorld is an early alpha made by a very small team. Thank you for helping
keep players' worlds, keys and computers safe.

## Report a vulnerability privately

**Please don't report security problems in public issues, pull requests,
discussions or comments.** Use GitHub's private reporting instead:

**[Report a vulnerability](https://github.com/ClankerWorldOrg/ClankerWorld/security/advisories/new)**

Please include:

- what an attacker could do, and what they would need first (for example,
  being on the same PC, on the same network, or having a paired device);
- steps to reproduce it, or a proof of concept;
- the version, or commit, you tested.

**Never include real secrets**, even your own: no API keys, pairing codes,
device credentials, private saves, or raw replies from a model service. Use
made-up values. If you think a real key has leaked, revoke it with its provider
straight away.

You'll get a reply in the private report. This is a small alpha project, so
there is no fixed response time yet. Please give us a fair chance to ship a fix
before you disclose anything publicly. If you'd like to be credited, say so in
the report.

## Supported versions

Only the latest published alpha and the `main` branch get security fixes.
Older alpha builds don't; update through the launcher.

## What's in scope

- **The world server:** device pairing and approval, the owner's signed
  requests, the local approval listener, and how it reads requests and saves.
- **The game and the launcher:** how API keys and device keys are stored, how
  the game starts and talks to its world server, and how the launcher checks
  and installs downloaded versions.
- **Saves, logs and problem reports:** anything that leaks keys, pairing codes,
  device credentials or another player's private data.
- **Release packages and this repository's automation:** builds, release files
  and GitHub workflows.

## What's out of scope

- Problems in a model service itself (report those to that provider), and what
  a model chooses to say. The game treats every model reply as untrusted.
- Attacks that need administrator access to the player's PC, or an already
  compromised Windows account.
- Flooding a world server you have exposed to the internet yourself. Run a
  remote world server only on a private network.
- Windows warning about an unknown publisher: builds aren't code-signed yet.
- Vulnerabilities in third-party packages without a way to exploit them through
  ClankerWorld. Please report those to the package's maintainers.

## How the game protects you

- The world server is the only authority over the world. The game, other
  devices and model replies can only ask; the server checks every request.
- The world server the game starts on your PC listens only on your own
  computer. The first device's code is approved on the world server's computer;
  an approved owner device can approve further devices.
- On Windows, API keys are stored protected for your Windows account. Keys never
  go into saves or logs. Problem reports also blank known secrets and text that
  resembles common keys before including log excerpts.
- The launcher checks every download against the published release before
  running it.

## For contributors

Contributors and coding agents follow the security rules in
[CONTRIBUTING.md](CONTRIBUTING.md#priorities): never describe a security
problem in an issue, pull request or comment. Contributors use private reporting
as above. Coding agents tell the owner in chat and create only the placeholder
issue described in CONTRIBUTING.
