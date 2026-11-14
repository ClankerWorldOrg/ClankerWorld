---
title: Model selection and usage
type: architecture
status: active
updated: 2026-11-14
---

# Model selection and usage

This page describes model selection, testing and usage accounting.

The owner's model picker shows the game's own list for each provider,
`ProviderModelCatalog.Curated`: newest generation first and, within a generation,
larger models first. Add new models there in their place. When a key is known,
the server checks it against the provider's model-list route (OpenAI
`/v1/models`; Ollama Cloud `/api/tags`, then `/v1/models`; Anthropic
`/v1/models` through its official client, with its own key header) using
the saved key, a named key slot, or a key pasted for that check only, which is
not stored. Keys never return to the client. Listed models the key's route
doesn't include are marked unavailable; names are compared without Ollama's
`:cloud`, `-cloud` or `:latest` endings. If the key can't be checked, the whole
list stays usable and the reason is shown. OpenAI list and personal-model test errors
share bounded categories for authentication, permissions, model access, credits,
spending or usage limits, rate limits and service failures, following
[OpenAI's error guide](https://developers.openai.com/api/docs/guides/error-codes).
Only known error codes and types are classified; remote messages never enter
results or logs. Unreadable or oversized error bodies retain the HTTP category.
A new agent starts on the provider's
default model when the key can use it. If the key can't use a new agent's
starting model, the picker selects nothing and asks the owner to choose, so no
other model, possibly a costlier one, is chosen for them. An existing or
hand-picked model the key can't use stays shown, greyed, with the same request.
Starting a model lookup resets the typed display of an automatic new-agent
choice, including after a provider switch. The new list then checks that
default's availability. Choosing or typing a model marks it as the owner's
choice, so a later list refresh preserves it.
Checks are cached per key for ten minutes, time out after eight seconds and are
not model calls, so they do not count toward the usage cap below.

**Test model** is a separate owner action in Add Agent and an agent's Model
panel. It sends one request through the same personal decision adapter used in
play, at the chosen thinking level: chat completions with the required JSON
response format for OpenAI and Ollama Cloud, and the Messages API for
Anthropic. It sends no temperature and uses the shared 16,000-token output limit. The server durably reserves a
paid-call allowance before sending; every result after that point, including a
timeout or rejected format, counts as one attempt. The check never tries an
alternate request format, changes provider settings, or saves a pasted key.
The response contains only a bounded result such as ready, missing key, usage
limit, authentication or access failure, exhausted allowance, rate limit,
unsupported format, timed out, unavailable, or unusable reply. It does
not include provider error text or response bodies.

**Test helper** reuses this signed, metered endpoint for Jev's System One or
OpenAI Decisions adapter, with the selected model and saved provider-specific
key. The challenge must advertise `clankerworld.owner-helper-setup-check.v1`
before the client sends the action; older hosts get the existing update message
without a paid request. Helpers reject personal-model thinking options.
The synthetic check offers only `safe_idle` and requires that offered choice;
it does not run or change an agent. Settings distinguish selection from a
successful connection check and explain that ordinary routing requires every
offered candidate to be routine, with no guidance, naming or identity request.
Editing the helper, model or key clears its test result. A replaced form cannot
publish a late success or failure in its label or global feedback.

An installation-local usage file reserves every model attempt before HTTP work.
Concurrent requests share its optional lifetime attempt cap. Failure, retry and
abandonment keep their spent allowance; only known token counts are added.
Claude and chat-completions decisions and model checks keep reported counts
even when the game answer's JSON cannot be read. Missing usage adds no estimated
tokens. Invalid negative counts are discarded so an unusable reply still finishes
as a failed attempt.
Deterministic choices consume no attempt. Reaching the cap persists a pause;
changing allowance and resuming are separate owner actions.
Start World and Resume share a check that refuses unreadable accounting or an
exhausted allowance before changing the world.

The client keeps an edited limit separate from the displayed usage status
until a limit action succeeds or the registration changes. Both Settings
reads and automatic pause reads accept only the latest usage operation for
the current registration. A successful limit action invalidates earlier
reads; changing registration clears the old installation's usage status,
accounting error and limit draft.

The reservation that brings the total to 80% of the cap, rounded up
(`ProviderUsageStore.WarningMark`), raises `WarningReached` once at that
installation-wide crossing. No additional durable warning marker is saved:
totals only grow, one per reservation, and a changed cap sets a new mark that
only later reservations can cross. In game-world mode the server acquires the
world mutation gate, then appends a player-facing `model_call_warning` event
(`used:<count>:limit:<cap>`) to the world active at that time. A concurrent world
switch can change which world receives it.

A successful save keeps the warning event with that world. A failed save leaves
the event in memory for the next successful world save and logs
`event_log_unsaved`. A restart or loading an older save restores its checkpoint,
which may predate the warning; the installation counter never rewinds and does
not reissue past crossings. The count and cap stay in the usage file, and no
checkpoint schema changed. Telemetry logs `provider_usage_warning` with its
outcome.

`ProviderUsageWorldEffects` applies both the cap's pause and the warning under
the world mutation gate. A pause also rechecks the installation's current
allowance after acquiring the runtime gate. If more calls are allowed or the
limit was removed while it waited, it completes without pausing or saving and
logs `superseded`; otherwise it pauses and saves. Warnings still append and
save their crossed mark. Starting a conversation
call reserves before the provider's first await; canceling a provider call can
also invoke accounting callbacks synchronously. Both can happen on the thread
holding the runtime gate. That gate is not reentrant, so the runtime marks
provider startup and cancellation under it
(`IsInvokingProviderUnderGateOnThisThread`). On that thread the pause or warning
moves to another task that waits for the world. Outside the runtime gate it
runs at once, so a model decision's late reply cannot be admitted before the
pause. `provider_usage_limit_reached` logs `paused`, `paused_unsaved`,
`superseded` or `failed`.

Unreadable or inconsistent accounting leaves the server reachable with paid work
blocked. Preserve the damaged file; changing the cap cannot bypass it. Writes
use unique private temporary files flushed before replacement. Failed reservation
writes do not increment the in-memory total or dispatch a model call. This does
not promise directory-fsync power-loss durability or provider-invoice parity.
