---
title: Device pairing
type: development-reference
status: active
updated: 2026-10-18
---

# Device pairing

Pairing gives one approved device permission to inspect and control the private
world. The private Tailnet encrypts the connection and limits who can reach the
server; being on that network alone does not grant world access. For the player
steps, see [Playing](../playing.md#connect-your-device).

The September 2026 identifier reset requires a new Windows registration and
device key. The previous registration is not imported; pair the matching new
client and server through the normal flow. Keep old authority in a private
rollback backup. This is the approved pre-release exception described in
[Saves and replay](saves-and-replay.md).

## Pairing flow

1. A Windows client creates a non-exportable current-user CNG P-256 signing
   key locally. Its non-secret pairing registration metadata lives under the
   app user's storage, while the private key remains in the Windows key store.
   The server receives only the public key and its fingerprint.
2. The unpaired client requests a pairing record. The server returns an opaque
   pairing ID, a short human-verification code, and a short expiry. The code is
   a comparison value, not a credential. Pairing and challenge state is bounded;
   a pairing also expires after a bounded number of incorrect visible-code
   attempts instead of becoming a durable brute-force target.
3. A server-local bootstrap administrator approves the matching pairing ID and
   code through a separate loopback-only listener. There is no bootstrap
   password or reusable approval secret in the client, a URL, or deployment
   configuration. A remote Unix server generates an owner-only approval header file;
   its account reads that file for local approval and recovery. The local server,
   which the game starts on the player's own PC, instead needs a
   launch secret ([local server](#local-server-started-by-the-game)). Once an owner device exists, it may approve or revoke further
   devices through signed owner-device-management requests. It may also request
   the signed registry of paired-device lifecycle records. The registry exposes
   only public device IDs, public-key fingerprints, lifecycle state and approval
   origin; it
   never exposes private keys, comparison codes, or reusable challenges. The
   same separate server-local listener supports recovery revocation when every
   paired Windows device has been lost or revoked.
4. The device proves possession of its private key before activation. The
   server records its public-key fingerprint, fixed owner scope, and durable
   activation/revocation state. Private keys and raw approval codes never enter
   world saves, snapshots, event projections, or logs.
5. Each read or control request carries a short-lived server challenge and a
   signature over canonical request data. The server consumes the challenge
   once before asking the runtime to commit. Challenges are process-local:
   restarting the server invalidates both pending and consumed challenges, so an
   old nonce cannot be replayed after a crash. Pending challenges are capped per
   device and overall; a new challenge expires the oldest unanswered ones to make
   room instead of being refused. Pairing and revocation remain
   durable; idle reads do not rewrite the authority file. It
   derives the durable issuer from the active device; the runtime mints durable
   IDs, event ordering, and submission sequence. A client request ID and
   idempotency key are signed ingress values, not ownership claims.

The initial server-local approval route is deliberately separate from normal
network requests. For this private deployment, the owner can relay the short
comparison code through the already trusted direct-control channel; that
channel approves a pending pairing only, never supplies the device key.

## Client and server updates

An approved device can reconnect even when a particular action needs a newer
server. Authenticated challenge responses advertise supported action payloads;
New World preview and creation require `clankerworld.owner-world-creation.v3`.
Autosave configuration requires `clankerworld.owner-autosave-configuration.v2`,
which signs the world whose settings the player opened. The client checks each
fresh challenge before signing or posting these actions.
An absent or different format produces an update message while keeping the
existing pairing. Older servers without this advertisement need an update too.
Routine helper changes require `clankerworld.owner-routine-helper.v1`, signing
the observed world ID, helper, model and optional saved OpenAI or TypeSafe key-slot ID. The
server holds the world-selection gate through validation and persistence, and
refuses a changed world or an unpaused world.
Named-key deletion checks the active world's routine helper together with agent
assignments. It holds the same world-selection gate as helper changes, so an
active helper's key cannot disappear during a switch. Refusal preserves the key
file and the world's helper settings.
A request that names Anthropic or a thinking level requires
`clankerworld.owner-model-thinking.v1`: model settings, model lists, saved keys,
Test model and founder or agent placement. Their payloads gain a final
`thinking=` line only when a level is chosen, so other requests are unchanged.
Other signed actions, including reconnect, keep their existing contracts.

The advertisement is compatibility information, not permission to weaken a
proof. The server still reconstructs the exact action payload and verifies its
binding, device signature and one-use challenge. The client never retries with
an older format. Actual challenge/access failures retain their access messages.
See [Releasing](releasing.md#matching-client-and-server-builds) for distribution order.

Owner pairing and signed control traffic require HTTPS. Literal loopback IPs
are the sole plaintext exception, for an explicit local-development server.

The headless server persists paired-device authority separately from the world
runtime. The authority file contains public keys, fingerprints, state, and
anti-replay hashes—not device private keys or raw comparison codes. It also
holds the server authority ID that pairing replies and signed proofs name. Unless
`ClankerWorld:Pairing:ServerAuthorityId` sets one, a new server picks a random
ID and keeps the file's ID after that. Existing servers retain their stored ID,
including IDs made from a computer name by older builds, so paired devices stay
paired. The runtime file contains world state and never becomes credential storage. Both files are
written atomically and use owner-only filesystem permissions on Unix servers.

## Server-origin binding

An owner key is not a portable bearer credential for arbitrary URLs. Both a
pending pairing and an activated local registration are bound to one canonical
server origin. The client permits remote HTTPS origins, with literal loopback
HTTP only for explicit local development; paths, queries, fragments, and
userinfo are not accepted as part of an origin.

While pairing is pending, polling and activation remain at the origin that
issued the pairing record. The client verifies the expected server authority,
device ID, and public-key fingerprint before it saves an activated
registration. Once paired, an edited URL or command-line value cannot silently
retarget the registered owner key. Changing servers is an explicit local
operation: forget the registration and pair to the new server. The persisted
authority and world identities provide a second binding beyond the transport
origin.

Replacing or forgetting local registration also discards the held observation,
event cursor and terrain cache, and cancels an old in-flight refresh. This applies
to ordinary activation and recovery of an already-active pairing. The new server
starts at cursor zero, so a younger world can be entered without restarting the
client. Tick/event regression and terrain identity checks still apply within the
new observation timeline.

Aging-override and Jev actions additionally bind the selected world ID into their
version-2 signed payloads. The server advertises both formats on challenge
issuance; the client refuses to submit them to a server without that support.
World-bound actions use one `OwnerWorldRequestFilter<TAction>`: it validates
and authenticates the action, then holds the world-selection mutation gate
through the target-world check, synchronous handler and durable save. This
covers instructions, order cancellation, building management, life pace, Jev,
routine helpers, developer edits, autosaves and recovery cleanup. Each route
keeps its existing refusal response; runtime operations retain their own checks
for callers outside HTTP. Deletion may deliberately target an inactive world's
saved files, so its catalog and save-identity checks remain separate. For life pace
and Jev, a stale target returns 409 without changing the newly selected world,
and a request without a target returns 400. Building management retains its
rejected result. The pairing authority and world-save schema are unchanged.

Successful activation and recovery of an already-active pairing both return
to the Main Menu. Recovery saves the matching registration without opening
the world; Continue performs the usual signed reconnect.

## Recovering after another device loads a world

Private servers advertise `owner-observation-timeline.v1`. Each reconnect
baseline includes a `Timeline` with the live runtime's instance ID and a
monotonic generation, captured under the same gate as its snapshot and events.
A successful manual load or world switch advances the generation, including a
load of the same world at the same time. A new server runtime has a new instance
ID. Ordinary ticks, pauses, saves and history compaction keep the timeline.
These values are transient observer metadata, not saved world identity.

When another device changes the timeline, the client keeps its held view while
requesting a fresh baseline with cursor zero and no terrain or map-layer cache
hints. Only a validated baseline replaces the view and clears its old events,
selection and inspection state. Responses from an earlier request context or
timeline cannot replace the new view. The usual tick, cursor and event-order
checks still apply within one timeline. Hosts without the capability retain
the earlier reconnect contract; signed reconnect payloads are unchanged.

The game requires `owner-event-presentation.v2` on the signed owner handshake
because Event Log descriptions and typed event fields come from the host.
The host advertises this version without the preceding text-only version.
A host without it is refused
with an update explanation, preserving the last accepted view and paired device
registration. Update a remote host alongside the game.

## Building ownership changes

Building removal and reassignment payloads require the observed simulation world
ID as well as the expected Town and household ownership. The server verifies that
world ID under the runtime mutation gate before changing a building. A request
formed for a previously selected world is refused without changing the active
world or its saved checkpoint, even when both worlds use the same building IDs.
A removal confirmation retains the world and owners shown when it opened;
observation refreshes cannot silently authorize a different ownership change.

## Response-loss recovery

The game's `OwnerRequest<T>` runner owns each action's deadline and control
cleanup. Its context binds ordinary actions to the observation generation and
the sending panel's visibility and selection. A late answer or refusal from a
closed panel or an earlier world cannot update the new panel. Pause waits for
the existing action; duplicate ordinary actions still do not queue. Load,
create, select and startup recovery deliberately replace the timeline and keep
their registration binding. Every outcome restores the controls, while the
pending-submission store below decides whether an uncertain action can be retried.

A same-world observation refresh captures a `PanelDraft` for visible controls.
It restores editable text and its caret/selection, choice identities and scroll
positions after updating facts. A timeline change instead resets the old world.
Name captions use `FittedLabel`, including wrapped headings, with the complete
caption on hover; the client source guard rejects new plain name labels.

Instructions and order cancellations are server-idempotent, but a client can
still lose the response after the server commits one. The Godot client's retry
protocol also retains native map-authoring batches. Before sending
any of these requests, the client may atomically retain one non-secret pending
record. It is bound to the authority identity, device ID, public-key fingerprint,
and canonical server
origin, and preserves the exact instruction or cancellation idempotency key, or
authoring batch ID.

New map batches use `clankerworld.owner-authoring.v2`: the canonical signature
covers the world ID, expected latest event ID, run epoch, observer timeline,
batch ID and every operation field. The server holds the active-world selection lock through
validation and saving. Legacy v1 batches have no world precondition and are
refused with a plain reason. The client keeps all v2 fields through a lost
reply and never rebuilds the batch against a newer observation.

Instructions additionally retain the observed simulation world ID (not the
installation's pairing identity). Unqueued instructions use the signed
`clankerworld.owner-instruction.v2` payload. Queued instructions use
`clankerworld.owner-instruction.v3`, which appends `queue=true` to the canonical
payload. Both require the world ID. The server checks it under the same mutation
gate as selection and manual loading, through the instruction's durable save.
The client also binds each newly retained instruction, cancellation or
authoring batch to the confirmed observer timeline and world. Retry is disabled
while a fresh baseline is pending or a different timeline is observed, even
when the saved world ID is the same. The exact request and its idempotency key
remain on disk; the client never retargets them. After a load, switch or server
restart, check the world before explicitly forgetting a retained request and
issuing a new one. An older local record without a timeline is not silently
bound to a server that advertises this capability.

Client and server must both use this instruction payload. Older requests without
a simulation world ID are refused rather than guessed into the current world.
Unscoped local retry records remain on disk but cannot be sent; explicitly
forget them after checking the original world. Instruction payload compatibility
does not migrate old saves. Saved orders and cancellation receipts use the
current world format in [Saves and replay](saves-and-replay.md).

`POST /api/v1/owner/orders/cancel` signs
`clankerworld.owner-order-cancel.v1` with the world ID, idempotency key, target
agent ID and exact order ID. The server binds it to the active owner device and
world before committing, and saves the original receipt for idempotent retries.
The retained client cancellation has the same pairing, origin, world and
observer-timeline boundaries as an instruction. A fresh challenge and signature
are required for each retry.

Pending cancellations preserve the complete native target ID, including the
longer IDs of adults born in the world. The client retains the 128-character
bounds for the caller's idempotency key, order ID and world ID, and refuses
blank or control-character identities. Persistence, reload and retry keep the
same target; they never shorten or replace it.

After a successful reconnect confirms the same world and observer timeline,
the client automatically retries that exact record once. It requires the
original pairing, device key and origin as well as a valid, matching timeline;
legacy observations without a timeline require an explicit player retry.
Recovery runs after the observation refresh finishes, through the existing
owner-action gate. An owner action's own refresh cannot recursively retry it.
A second lost reply keeps the record and offers manual recovery instead of
resending on every observation pulse.

The ordinary **Your last request** panel offers **Check and send again** and
**Discard it**, without Developer mode. On the original confirmed world,
Check and send again retries the exact request. After a load, switch or server
restart, it instead discards the old local retry and asks the player to inspect
the world before issuing a new request. An instruction for the same world's
existing agent becomes an editable draft with its original text, kind and
queue choice; only a new Send creates a new key. A different world gets no
retargeted instruction or automatic submission. Cancellations and authoring
must be reviewed and issued again through their normal controls. Discarding
never undoes a request the server already accepted. An unreadable or
connection-mismatched record also offers explicit discard; its contents are
not exposed or automatically submitted.

Each exact retry obtains a new one-use challenge and signature. For an
instruction or order cancellation, it submits the same logical request so the
server returns the original receipt rather than creating a duplicate. The
native server returns success for an identical map batch recorded in retained
Developer edit history; an older unrecorded batch is refused by its expected
event and run epoch rather than applied twice. World instruction keys ignore surrounding whitespace consistently. An exact instruction
retry at the server still returns its accepted receipt after the recipient dies,
including after saving and restarting the server. The client's stricter timeline
guard above decides whether its retained record can be sent. A new instruction to a deceased agent or
a different request reusing that key remains rejected. This is not a
general offline queue: only one request is retained, it cannot cross a pairing
or origin boundary, and it can be explicitly forgotten. The record never
contains a private key, signature, challenge, comparison code, or bearer
credential.

A definitive HTTP client refusal clears that request's local retry record and
restores the owner controls, while keeping the refusal visible. HTTP 408,
server errors, lost responses and unreadable receipts keep the exact record
because the server may have committed the request. A late refusal from a previous
world context cannot clear a retained request in the current world.

## Current scope

The device registry records which paired device approved a new pairing, or that
approval happened on the server's own computer. The origin survives approval,
activation, revocation and restart; **Paired-device management** in F12 shows the server's
description. Older authority records without that information display
**Approval not recorded**. This is installation authority metadata, outside
world saves and replay.

Every paired device has the sole `owner` scope: world observation,
pause/resume, instruction submission, order cancellation and device management.
Paused map editing uses the same owner scope. There are no viewer, operator, or multiplayer roles yet. A revoked
device immediately loses access. The revocation is written to the authority
file before it takes effect, so a failed write leaves the device active and
reports an error instead of losing the revocation at the next restart.
Revoking also cancels any pending or approved pairing for the same key.

One key belongs to at most one active device: approving or activating a second
pairing for a key that is already active is refused. A revoked key cannot start
a pairing or have one approved on the public routes. The approval listener on
the server's own computer can still deliberately start and approve its pairing,
which gives it a new device ID beside the revoked record. Authority state retains non-secret public-key
fingerprints and device IDs; world-side control events retain a non-secret
server-derived issuer string, never a private key or comparison code.

All world-changing requests remain requests. Authentication permits the server
to validate and enqueue them; only the authoritative runtime commits the
result. Unpaired devices receive neither full-world observations nor control
results. The unauthenticated HTTP surface therefore exposes only protocol
discovery, not world observation or a second owner interface. Legacy static
diagnostic assets are not a supported game client.

## Required evidence

Current pairing evidence must cover expiry, bounded failed-code attempts,
invalid proof, replayed challenge, revocation, unpaired read/write denial,
owner-only observation, server-derived issuer/tick/sequence, idempotent control
submission, paired-origin binding, signed registry/approval/revocation,
response-loss retry, and authority/runtime
recovery across restart. The Windows client must prove it can create/store a
device key, complete a paired reconnect, and reject a server response that
lacks the negotiated owner capability. The export path must be verified
separately from a real Windows 11 owner test; the latter is not implied by a
Linux CI export.

## Pairing volume and local recovery

Public pairing creation has a shared eight-attempt, one-minute budget, including
invalid keys. It is attached to the route itself, so every path spelling routing
accepts for it counts. A shared budget avoids trusting proxy-supplied source addresses; a
429 response includes Retry-After: 60. Signed challenges, reconnect and pause do
not consume it. This is bounded ingress, not a claim that the Tailnet endpoint
is DoS-proof.

Every route caps its request body before anything reads it
(`RequestBodyLimits`): 16 KiB for pairing routes, 1 MiB for signed
`/api/v1/owner/*` actions, whose largest normal requests are about 2 KB, and
64 KiB for everything else, including challenge requests and the server-local
routes. A larger declared body gets 413 before anything reads it; a body
without a declared length stops being read at the same size. Refusals based on
the declared length log `request_body_limit` at most once a minute, with the route
kind and limit only.

If unauthenticated requests fill the eight pending slots, the existing separate
loopback approval listener also accepts POST /api/v1/approval/pairings with the same
public-key-only body as public pairing creation. It bypasses the public creation
budget and, only when necessary, expires the oldest **unapproved** pending request
to admit the operator's new request. It never expires an approved pairing or
revokes an active device; if all slots are approved, it refuses. Invalid keys and
already active keys must not displace a pending request. Approval and signed
activation still follow their existing checks. The ordinary forwarded listener
returns 404 for this recovery endpoint. Never forward the approval listener.

Every `/api/v1/approval/*` route also needs the request's host name to be
`127.0.0.1`, `[::1]` or `localhost`, with or without a port. Address the
approval listener that way, as the game does; any other name gets the same 404
and one `approval_listener outcome=refused reason=host_name` line at most once a
minute. The public listener has no such check, because Tailscale Serve reaches
it under its own name.

On a remote Unix server, approval and recovery also require the server account's
`X-ClankerWorld-Approval-Secret` header (the owner's choice, October 18). Startup
generates a private header file at `ClankerWorld:Pairing:ApprovalSecretPath`, or
beside the authority file with `.approval-header` appended. It uses owner-only
permissions from creation, keeps the same value across restarts and refuses
shared permissions, symbolic links and damaged files without replacing them.
The containing directory must not be writable by other accounts. Keep this
installation file out of saves, logs and reports. Missing or incorrect proof
receives the same 404; it never authorizes shutdown or the local-game launch check.
Proof refusals log only `approval_listener outcome=refused reason=proof`, at most
once a minute with a skipped count, never the supplied or expected value.
The [deployment guide](remote-server-deployment.md#local-approval-and-recovery)
shows commands that read the header from disk without putting its value in process
arguments. The game-started local server keeps its launch-secret flow below.

This is an operator recovery path, not automatic queue eviction on behalf of an
untrusted remote client. The game's own server also uses it for the game's first
pairing ([Local server started by the game](#local-server-started-by-the-game)). Keep the
returned code and proof local/private. Tests cover capacity recovery and signed
owner availability. Bodies without a declared length and the host-name check are
tested on a real Kestrel server, not only through TestServer.

## Local server started by the game

For the local Windows package ([#468](https://github.com/ClankerWorldOrg/ClankerWorld/issues/468)),
the game starts its own world server, the local server, on the player's PC. Every program on that PC can
reach a loopback listener, so loopback alone cannot prove the request came from
the game. Before starting it, the game writes a fresh random value of at
least 32 characters to a file in the player's own data folder and starts the
server with:

- `ClankerWorld:Pairing:ApprovalPort`, the separate approval listener, and
- `ClankerWorld:LocalServer:LaunchSecretPath`, the path of that file. The path
  is not secret; the value is never passed in arguments, logged or saved by the
  server. A missing, short or unreadable file stops startup, and the setting is
  refused without an approval port.

The game passes on the player's environment without the provider key variables
a server started on its own saves on first start (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`,
`OLLAMA_API_KEY`, `TYPESAFE_API_KEY`) or any `ClankerWorld__` setting override,
so its local server takes keys only from **Settings → Game → API keys**. A server
started on its own still reads both.
The child process uses `Production` for both `ASPNETCORE_ENVIRONMENT` and
`DOTNET_ENVIRONMENT`, and does not inherit `ANTHROPIC_BASE_URL` or
`ANTHROPIC_AUTH_TOKEN`.

On such a local server, every `/api/v1/approval/*` request except the launch
check below must also carry the value in the `X-ClankerWorld-Launch-Secret`
header, compared in constant time. Without it the routes return 404, exactly as
on the forwarded listener.
The game uses it to start its own pairing with `POST /api/v1/approval/pairings` and
approve it with the comparison code, so the player sees no code to copy and the
public route's shared budget and pending slots do not hold it up. Status and
activation stay on the pinned public origin. Pairing with any other server uses
the public route.

The game does this itself when the `package.json` in its folder names a world
server that is there, and no `--server-url` argument is given (`LocalServer`). It uses the fixed origin
`http://127.0.0.1:5188/`, because a paired origin is pinned, and the approval
port 5189. It stores this pairing in `user://local-owner-device-registration.json`,
apart from a server pairing. The game holds an exclusive per-installation
`server/startup.lock` handle from the port check through launch and readiness.
An overlapping game refuses startup
before changing the secret or logs. The empty lock file stays in place; process
exit releases its handle, so a crash leaves no stale lock. A failed launch
restores the prior secret, and cancellation finishes its own starting child's
stop attempt before releasing the handle. An explicitly refused shutdown keeps
the server and its paused world available for retry. A running server of the exact same version and
source revision, started by an earlier launch, is reused only after it answers
the side-effect-free launch check on the approval listener. The game sends
`GET /api/v1/approval/launch-check` with a fresh random challenge in the
`X-ClankerWorld-Launch-Check` header, and the server returns an HMAC-SHA256 of
it keyed by the secret (`LocalServerLaunchCheck`). The game compares that with
the answer for the secret already in its file, in constant time. The secret
itself never goes over the connection for this check, and the game sends it on
other approval requests only to a server that has answered; a child of its own
that has not answered is stopped directly instead of over HTTP. A clean stop, one
the server acknowledged or of the game's own child, deletes the secret file, so
only a server still running keeps one for the next launch. Anything else on
the port is refused with a message and never stopped. Discovery or proof requests
that time out are retried within the existing startup window, retaining their
two-second request deadlines. After a discovery timeout, an exclusive bind to
the loopback port checks whether it is free: Windows may still be retrying a
connection to an empty port when the request deadline expires. A free port lets
the game launch its server; a failed bind keeps the existing listener protected.
An explicit build mismatch, invalid response or
secret refusal ends the attempt immediately. An unresponsive existing server is
reported as timed out, without starting a second process or replacing its secret.
Caller cancellation also leaves that server and secret intact. The server's output goes to
`logs/server.log` in the player's data folder, and each start first moves the
previous one to `logs/server.previous.log` so a crash can still be reported.
Saves, the owner authority and the provider files sit in separate folders
there, never in the game folder.

`POST /api/v1/approval/shutdown` exists only on a local server. It
pauses the world, cancels model work, saves pending newborn model bindings in
provider storage and writes its checkpoint before returning 202. The response
completion callback requests process exit, so server teardown follows the
acknowledgement. If either write fails, it returns 503 and
keeps the server alive and paused so the game can retry. Pending startup recovery
returns 409 without rewriting the refused checkpoint. Whenever a world
server stops, it waits for the tick loop to finish and writes a final checkpoint
(`server_shutdown` in the log). Ctrl+C or a service stop also pauses a local
server's world; a failed final write is logged and sets a nonzero process exit status.
A world halted for inspection is left untouched. A server
started without these settings behaves as before and cannot be stopped over
HTTP.

## Bounded owner actions

Starting pairing, polling its status and activating the device each have a
15-second deadline, or the HTTP client's shorter configured timeout. The
deadline includes reading the response body after successful headers. A stalled
body releases the connection controls so the player can try again; caller
cancellation also stops the request. A lost activation reply can still be
recovered through the existing active-pairing status check.

Signed actions have a 15-second deadline over the complete challenge/sign/send/read
operation, or the HTTP client's shorter configured timeout. Load World allows up
to 60 seconds because listing checks every saved checkpoint; leaving that screen
still cancels the request. Cancellation covers both response bodies even after
successful headers. Reconnect retains its shorter four-second deadline.
A timeout does not prove that the server rejected an action;
instructions, order cancellations and authoring retain their exact retry record
until a receipt is accepted.

Pause/resume waits for the active owner action to release the client gate rather
than being dropped as a duplicate click. Ordinary duplicate actions still do not
queue. Confirming Quit to Menu can retry an unconfirmed pause, and leaving still
requires an accepted pause receipt, even if an observation already says paused.
A paused observation alone cannot prove a failed checkpoint write recovered.
A later failed refresh does not revoke an accepted receipt. This does not change Main Menu Quit Game.
