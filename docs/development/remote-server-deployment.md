---
title: Remote world server deployment
type: development-reference
status: active
updated: 2026-10-18
---

# Remote world server deployment

Players setting up a fresh Linux host should use the
[Linux hosting guide](../hosting-linux.md). This page remains the operator's
checklist for preserving an existing installation during a manual update.

Use this checklist for an owner-authorized update of a world server that runs
on another machine, such as the development VPS. It describes a manual
operation; there is no staged deploy command yet. Building or merging a PR
does not authorize updating the running remote world server.

The operator records each step as passed, failed or not performed, with the
source commit, artifact hashes and bounded outcomes. Keep that record private
when it identifies an installation. Never include keys, pairing codes, save
contents, private thoughts or raw model replies.

## Prepare the update

1. Confirm the destination machine, service, OS account and authorized maintenance
   window. Record the running commit and the proposed commit. Inspect the actual
   service configuration privately; the checked-in
   [systemd template](../../deploy/clankerworld-server.service) is a starting
   point, not evidence of what is running. Updating binaries does not authorize
   replacing existing state or pairing authority.
2. Build from a clean checkout at the proposed commit. Run the applicable
   [build and test checks](build-and-test.md#which-checks-to-run), including
   locked restore, formatting, Release tests, Godot checks and Windows export
   when distributing a matching client. Publish the server from that same
   checkout with `dotnet publish src/ClankerWorld.Server/ClankerWorld.Server.csproj --configuration Release --runtime linux-x64 --self-contained -p:RestoreLockedMode=true --output /path/to/staged-server`.
   Record the publish directory, game version and SHA-256 hashes of the exact artifacts.
3. Check save, generator and signed-action compatibility against the current
   server. During alpha an older save may be refused, but must be preserved.
   Obtain the owner's explicit choice before a fresh-world reset. Never
   discover an incompatible save by replacing the running application first.
   See [Saves and replay](saves-and-replay.md) and
   [Matching client and server builds](releasing.md#matching-client-and-server-builds).
4. Stage the verified binaries in a separate versioned directory. Keep the
   existing binaries and service configuration available for rollback. Check
   free space for the complete private backup and staged release, and that the
   intended service account can read the release and write its state paths.
   Stop if a path is ambiguous or the rollback copy is incomplete.

## Locate and preserve installation state

Resolve absolute paths from the actual service environment and configuration,
including relative paths against its working directory. Keep state outside the
versioned binary directories. The relevant configuration keys are:

| Configuration key | What to preserve |
| --- | --- |
| `ClankerWorld:World:SavePath` | Active world checkpoint and its adjacent `.history`, `.manual`, `.autosave.json` and `.worlds` storage |
| `ClankerWorld:Pairing:AuthorityPath` | Durable owner-device authority and revocations |
| `ClankerWorld:Pairing:ApprovalSecretPath` | Account-private approval header file (defaults to the authority path with `.approval-header` appended) |
| `ClankerWorld:Providers:ConfigurationPath` | Installation model assignments and protected credentials |
| `ClankerWorld:Providers:UsagePath` | Installation-lifetime paid-attempt accounting |
| `ClankerWorld:Assets:CatalogPath` | Approved asset catalog and the assets it refers to |

When `ClankerWorld:Providers:UsagePath` is unset, the server uses `provider-usage.json`
beside the provider configuration file. An older process or different working
directory may have used another location. Identify all possible old and new
meter locations before switching; a missing file is not proof that no calls
were spent. Back up private service configuration and environment files too.
See [Checkpoints, history and backups](saves-and-replay.md#checkpoints-history-and-backups)
for the complete backup set.

1. Pause through the approved client's signed control path. Confirm the pause
   was durably acknowledged and record the world identity and tick. A client
   disconnect alone is not a durable checkpoint acknowledgement.
2. Stop the service, confirm its process has exited, and prevent an automatic
   restart during the update. Copy the whole resolved backup set while no
   writer is active. Copying a running world is not a coherent backup.
3. Verify the backup can be read by the intended recovery account, has
   restrictive permissions and contains every referenced history segment,
   catalog world and checkpoint. Check hashes privately, without printing
   file contents. Keep the untouched originals until verification succeeds.

## Move accounting and credentials safely

Record spent attempts, the configured limit and pending reservations before
changing the meter location. Preserve the entire meter, including pending
reservations; do not turn an interrupted request into unused allowance. On
startup, the current server records interrupted reservations as abandoned while
keeping their spent attempts.

- If both locations contain accounting, stop. Do not pick the smaller, newer
  or more convenient file, concatenate them, or reset either one. Reconcile
  the histories in separately reviewed work before allowing paid calls.
- If a meter cannot be read, paid calls stay blocked. Preserve the file and
  use the recovery message; deleting it would start accounting at zero.
- A binary rollback must keep the latest valid accounting. Never restore an
  older, lower meter from the world backup to make the old binary start.
- Keep credentials in installation storage, outside world saves. Check that
  protected credentials can be read by the intended OS account using the
  supported credential setup. A copied encrypted file may depend on its
  original account or OS. If it cannot be read, leave the model unavailable
  and let the owner set it up again; do not export plaintext keys.

## Switch and verify while paused

1. Point the service at the staged release and the verified state paths, then
   start it. Record the running process and exact binary hashes, and compare
   them with the staged artifacts. A responsive port alone is insufficient.
2. Check state ownership and permissions. Confirm that no fallback state or
   fresh meter appeared inside the new release directory. Check the bounded
   startup results for checkpoint, history, authority, credentials and meter
   loading. An unsupported or damaged save must remain intact.
3. Reconnect with the existing approved Windows device. Verify the signed
   handshake and required action formats, the same world identity, the saved
   paused tick and model assignments. Inspect and reload a disposable current
   checkpoint to check persistence; do not overwrite the active playtest save
   just to collect evidence.
4. Confirm time and paid attempts remain unchanged through paused observation
   refreshes. Verify missing-key, unreadable-meter and exhausted-limit cases
   using disposable synthetic state with a fake provider. Do not probe a real
   paid model merely to establish readiness.
5. Report the update and any failed checks. Only a separately explicit owner
   Resume starts time and model calls. Keep client and server revisions in the
   record; deployment, export and actual playtesting are different checks.

## Roll back or stop

If readiness fails, keep the world paused and stop the new service. Preserve
the failure evidence and all state it wrote before changing binaries again.
Restore the previous binaries and service configuration. Use the latest state
only if the previous binary can read it; otherwise use the matching paused
pre-update world/history backup with the owner's authorization. Preserve the
newer state separately. Accounting always stays at its latest valid spent
total, even when world state goes back.

If the old binary cannot safely read the latest accounting or credentials,
leave paid calls blocked and stop for repair. A rollback is not permission to
delete state, re-pair silently, reduce spent usage or start a new world.
Repeat the paused verification above before considering the rollback usable.

## Rehearse before a live update

Use disposable synthetic installation state, an isolated service and a fake
provider. Exercise failure before service stop, after backup, while moving
accounting, before readiness and after the first new-format save. Check that
each failure preserves the original files, leaves a usable recovery path and
never lowers spent attempts or starts unattended paid work. Record what the
rehearsal actually checked; this checklist is not a completed rehearsal.

A future command can automate plan, stage, apply, verify and rollback after the
local Windows package works. Its remaining contract is tracked in
[#267](https://github.com/ClankerWorldOrg/ClankerWorld/issues/267); no such command
is implied by this guide.

## Installing this build on a remote server

The [server-only package](build-and-test.md#linux-server-package) includes
the self-contained Linux x64 server and a fresh-install command. Its
[systemd unit](../../deploy/clankerworld-server.service) runs
`/opt/clankerworld-server/ClankerWorld.Server` as `clanker:clanker`, without
an installed .NET runtime. For a manually staged development build, publish
with `--runtime linux-x64 --self-contained -p:RestoreLockedMode=true` and
provision that account before installing the unit. Existing installations
follow the paused update procedure above, not the fresh installer.
`StateDirectory=clankerworld-server` creates `/var/lib/clankerworld-server`;
`StateDirectoryMode=0700` makes it private to the service account. World state,
device authority, the approval header and model configuration live there. The optional
environment file is `/etc/clankerworld-server/provider.env`.

Install the reviewed unit, reload systemd and start it:

```bash
sudo install -m 0644 deploy/clankerworld-server.service /etc/systemd/system/clankerworld-server.service
sudo systemctl daemon-reload
sudo systemctl enable --now clankerworld-server.service
```

Pair the first device through the local approval listener and configure its
models through the game. For an existing installation, use the paused update
and backup procedure above; a binary or service rename does not itself require
re-pairing, deleting state or starting a new world.

The template sets these environment variables:

- `ClankerWorld__World__Mode`
- `ClankerWorld__Hosting__Kind`
- `ClankerWorld__Clock__Run`
- `ClankerWorld__Clock__ClientPresenceTimeoutSeconds`
- `ClankerWorld__Http__Port`
- `ClankerWorld__Pairing__ApprovalPort`
- `ClankerWorld__Pairing__ApprovalSecretPath`
- `ClankerWorld__Pairing__AuthorityPath`
- `ClankerWorld__World__SavePath`
- `ClankerWorld__Providers__ConfigurationPath`
- `ClankerWorld__Providers__UsagePath`
- `ClankerWorld__Assets__CatalogPath`

The server also reads these settings, which a unit sets only when needed:

- `ClankerWorld:Clock:StrictTickFailures`
- `ClankerWorld:LocalServer:LaunchSecretPath`
- `ClankerWorld:Pairing:ServerAuthorityId`
- `ClankerWorld:Providers:AnthropicModel`
- `ClankerWorld:Providers:DecisionProvider`
- `ClankerWorld:Providers:JevModel`
- `ClankerWorld:Providers:Model`
- `ClankerWorld:Providers:OllamaCloudModel`
- `ClankerWorld:Providers:OpenAiModel`
- `ClankerWorld:Providers:UsagePath`
- `ClankerWorld:World:Seed`

`Program.cs` reads these keys; environment overrides replace `:` with `__`.
`ClankerWorld:World:Mode` accepts only `game`. Both HTTP listeners bind to
loopback. The ordinary port defaults to 5188; the approval listener is disabled
when `ClankerWorld:Pairing:ApprovalPort` is unset or zero. Enabled ports must
be distinct and between 1 and 65535. Client presence defaults to five seconds
and accepts two to sixty seconds. `ClankerWorld:Clock:Run` enables the clock service, while
signed presence and pause still decide whether it advances.

Forward only the ordinary listener through the deployment's authenticated HTTPS
proxy. It serves the discovery-only `GET /api/v1/handshake`, build-only
`GET /api/v1/status`, pairing routes and signed `/api/v1/owner/*` routes.
The approval listener, never forwarded, serves `POST /api/v1/approval/pairings`,
`POST /api/v1/approval/pairings/{pairingId}/approve` and
`POST /api/v1/approval/devices/{deviceId}/revoke`. Only a local server, which
the game starts, also serves `POST /api/v1/approval/shutdown` and
`GET /api/v1/approval/launch-check`, and needs
`ClankerWorld:LocalServer:LaunchSecretPath`. All approval routes check the local
listener and loopback host name. A remote Unix server also requires its private
`X-ClankerWorld-Approval-Secret` header. In local-server mode the other routes need
the existing `X-ClankerWorld-Launch-Secret` header, and the launch check answers a
challenge without it. [Device pairing](device-pairing.md#local-server-started-by-the-game)
owns those checks and the client bootstrap flow. Route handlers live in
`src/ClankerWorld.Server/Endpoints/OwnerEndpoints.Pairing.cs`.

Model-decision logs name the agent with `agent=`. The server's events include
`server_shutdown`, `approval_listener`, `model_decision`,
`model_decision_progress`, `model_call` and `model_conversation_call`.
`pairing_local_recovery` records local pairing and recovery;
`local_server outcome=shutdown_requested` records the game's shutdown request.

## Local approval and recovery

On a package-installed host, `sudo clankerworld-server approve` prompts for
the joining device's code and approves its unique pending pairing through
the same account-protected listener. It does not print a code or proof. The
lower-level commands below remain available to operators.

The remote server creates `/var/lib/clankerworld-server/approval-header` on its
first start, readable and writable only by `clanker`. This is the owner-approved
secret-file design (October 18). Operators run local approval commands as that
account; do not print the file or put its contents in arguments, environment
variables or logs. Existing deployments must also use a private state directory
and preserve this file across updates. If a file is damaged or has shared
permissions, startup refuses it and leaves it untouched for operator recovery.
Keep a private backup with the installation state.

Prepare an owner-only JSON request file readable by `clanker`, containing the
comparison code shown by the joining device as the `pairingCode` field. After
checking that the codes match, approve its pairing ID:

```bash
sudo -u clanker curl --fail-with-body --silent --show-error \
  --header @/var/lib/clankerworld-server/approval-header \
  --json @/path/to/private-approval.json \
  http://127.0.0.1:5189/api/v1/approval/pairings/PAIRING_ID/approve
```

For recovery revocation, prepare an owner-only request file containing `{}` and
run the same command with `--json @/path/to/private-revocation.json` and
`http://127.0.0.1:5189/api/v1/approval/devices/DEVICE_ID/revoke`. Recovery pairing
creation uses `/api/v1/approval/pairings` with the device's `publicKeySpkiBase64`
field, then follows the same comparison-code approval and signed activation.
Keep request files private and remove them after use. Never forward this
listener. The normal signed owner routes and the game's local launch flow do
not use this header file.
