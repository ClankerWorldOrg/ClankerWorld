---
title: Runtime logging
type: architecture
status: active
updated: 2026-11-14
---

# Runtime logging

This page describes runtime logs, problem reports and event descriptions.

## Runtime logging

Unexpected HTTP failures return a plain problem response. `SafeServerErrors`
logs `server_request outcome=failed error_type=... route=...`, using the route
pattern rather than request text and suppressing the
middleware's detailed exception diagnostics. `PrivateStorage` creates private
installation files with owner-only permissions before writing; its folders
also exclude other accounts while preserving an owner's read-only setting.

Changed gameplay loops, provider adapters, persistence boundaries and lifecycle
gates need low-noise structured logs at meaningful outcomes. An operator should
be able to tell what was attempted, accepted, rejected or fell back, and why.
Use stable event names and named fields such as tick, entity/request ID,
provider role/model, legal intention, outcome, latency and bounded usage.
Log transitions and decisions, not render frames or idle polls. The player
Event Log and unread count select known player-facing events and public
milestones through the shared `EventLogPolicy` classification for every accepted
kind. Routine personal storage, stocking and hauling, and automatic source
changes stay in accepted history without becoming Event Log rows. Player order
blockage/cancellation and consequential refusals remain visible; each problem
kind for the same complete actor and target appears at most once per world day,
using the saved calendar offset. Model failures retain their recovery-based
coalescing. The client keeps each problem's original event ID across raw-history
rollover, discards keys from expired calendar days, and resets them with the
observed world or timeline. The unread count uses the same selection. Unknown diagnostics do not
become player text.

Never log API keys, authorization headers, signatures, credential-bearing URLs,
prompts, raw provider request/response bodies, hidden reasoning or unbounded
player/model text. A reply is checked against its offered choices only after
its `model_call` line, so that line names the choice only when it is an offered
one and `<not-offered>` otherwise. Memory/belief/map telemetry uses safe IDs, counts and outcome
metadata, never private prose or map contents. Logs are derived telemetry, never
simulation authority or required save state. Observability tests must prove both
useful signal and absence of representative secrets.

**Report a problem** (`Diagnostics/ProblemReport.cs` in the Godot client, with
no Godot dependency so the launcher can compile the same file) zips `summary.txt`
and the last 4 MB of each log it is given: the current Godot log and the previous
run as `game.previous.log`, plus `server.log` and `server.previous.log`
when the game runs its own local server. It reads only those files, never saves,
settings or key stores. Each line passes through `ProblemReport.Redact`, which
blanks the launch secret, values after names such as `api_key`,
`Authorization`, `password` or `pairing_code`, bearer tokens, passwords in
addresses, common provider key formats and any token of 32 or more characters
mixing upper case, lower case and digits outside file paths. Known secrets,
named credential values and provider key formats are removed even inside paths;
ordinary paths stay readable. Hex tokens of 32 or more characters and unlabelled
six-digit comparison codes are removed as well, including inside paths.
The rule against logging secrets still applies; this pass is a safety net,
not permission. Reports go to `reports/` in the player's
data folder (or `user://reports` without a local server), keeping the five
newest.

`WorldEventFormatter` in the server builds descriptions from one owner snapshot.
The observation sends its derived `Text` on each event. An observer-only window
keeps the newest 2,048 observed events, matching connected clients even after saved
history compacts to 1,024. Each snapshot refreshes potentially visible rows in that
window, including model failures that different clients may coalesce differently.
The window resets on a load or world switch and never enters saved state. Renames
and changed Town or content names therefore update existing rows. The game displays
that text and keeps its event filtering and location controls. Descriptions resolve
complete agent and Town IDs from the owner snapshot;
colons inside those IDs are part of the identity. `WorldEventEntry` stores a
kind-discriminated `WorldEventPayload`: actor, captured position and each kind’s
own subject references and values. Producers construct these records directly.
Their inherited `Kind` getter uses a type lookup built once from the root's
`JsonDerivedType` registrations, keeping the JSON kind and the caller's kind together.
`WorldEventLog` uses the complete actor ID, without trying detail prefixes.
A read-only diagnostic projection keeps the established log spelling. Food yields
and Town membership fields remain separate values. Existing entries use the current saved name, including
deceased profiles. Model-decision and Town telemetry likewise keep complete IDs.
Server descriptions leave accepted payloads unchanged. The typed saved format is
documented in [Saves and replay](../saves-and-replay/validation-weather-and-events.md#typed-world-events).

The owner snapshot projects the saved continuity rule's current on/off state.
Godot uses it to keep an **Add a newcomer** offer in the Event Log while the
rule is on in a started world, even when the transition event has left bounded
history. The link opens the existing Add Agent controls and rechecks the current
snapshot when clicked; it neither places an agent nor asks for a paid model call.

Godot ends Add Agent placement mode on the current world's signed placement
receipt, before refreshing provider status. Failure of that separate read keeps
the placement confirmation and explains how to refresh model settings. A refused
placement leaves the panel open; responses from an obsolete world still cannot
change the current world's interface.

Preparing parenthood notes derive the caregiver household's usable edible
reserve and required amount from the same captured society/inventory checkpoint
as the owner snapshot. Birth and these notes share the lot ownership, carrier,
edibility, usable-vessel and reservation predicates. The reserve remains two
portions per active household member plus four for birth. A parent's continuity
guidance includes the amount only when they belong to the caregiver's current
household; a partner living elsewhere gets the blocker without stock quantities.
These notes add no saved fields, events or birth authority.
