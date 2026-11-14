---
title: Model requests and identity
type: architecture
status: active
updated: 2026-11-14
---

# Model requests and identity

This page describes model requests, agent identity and provider choices.

## Model inputs, usage and memories

The named `model` and `typesafe` HTTP clients refuse automatic redirects and
limit buffered bodies to the shared 256 KiB response bound. Streaming adapters
also use `ProviderResponseBody` before parsing. Credential-bearing records omit
their fields when printed. Startup removes matching leftover temporary provider
configuration files without reading their contents; busy or linked files are
retained, with only removed/retained counts logged. Credential writers keep
their handle open through atomic publication so cleanup cannot take a live
temporary file between its final write and rename.

The per-world routine helper is Off, Jev or OpenAI Decisions. Decisions uses
`POST https://api.openai.com/v1/decisions`, sharing Jev's bounded actor context
and existing owner-private memory scoring. Its `input` is serialized context;
`questions` and `answers` are arrays keyed by unique names. Action choices are
fixed candidate IDs. Memory score questions identify `source_index`, and scores
on three ordered levels map to the same 0–10,000 importance scale as Jev.
Unknown/duplicate answer names, unusable choice types and malformed scores are
refused. The runtime still admits only legal actions and requested owner sources;
a refusal or invalid action falls back to `safe_idle`. No confidence veto is added.

Decisions uses the installation's default OpenAI key or an explicitly selected
OpenAI credential slot; Jev uses the corresponding TypeSafe default or named key.
Both can select a key saved in API keys. The world saves only the slot ID, helper and model;
missing/deleted credentials produce the existing safe provider failure. A call
reserves one installation usage attempt before HTTP, including action and memory
questions together. Reply bodies are bounded; timeout, cancellation and sanitized
call telemetry use the existing provider boundary. Changing the helper or model
while paused advances its routing revision and cancels pending work. Late replies
cannot alter actions or memory scores. An explicitly selected helper handles
adult and elder routine choices, including those of world-born residents, and
scores only that actor's own memories. Infant, child and adolescent observations
never route to a helper. Personal assignments still handle planning, guidance,
naming, identity, conversations and wills. Birth provenance continues to forbid
paid world-default inheritance for personal requests throughout life; helper-off
routine requests use the saved personal assignment or local rules. The runtime
uses the current society age band for memory eligibility, and observations
already include the life stage in their admission digest. No save fields change.
Before the first explicit helper selection, existing founder installation
routing and its configured Jev model remain effective; known world-born adults
can also use the enabled helper. Applying the displayed default Jev
choice also activates explicit helper routing; repeating it afterward is a no-op.

The Decisions picker starts with `gpt-6-luna` and permits typed model names.
Its non-billable availability check uses OpenAI's model list with the selected
key; that list confirms model visibility, not access to the Decisions endpoint.
Jev offers `jev-1.14.0` without an availability probe. Its default lives once in
`ClankerWorld.Shared/RoutineHelperDefaults.cs`, compiled into simulation and game,
and used by the server through the simulation assembly. Explicit saved model
names, including `jev-1.13.0`, are retained.
TypeSafe's [model notes](https://docs.typesafe.ai/models) and
[API reference](https://docs.typesafe.ai/api), checked October 16, 2026, list both
versions on `POST /v1/systemone` with the same state/questions and typed
choice/score answers; this update requires no adapter format change.


A personal-model request selects one legal candidate, not a free-form dialogue
turn. It now includes bounded actor-owned self context: name, life stage,
personality, aspiration, household, available warmth/illness and the latest
private thought. Absent fields remain unknown. Need scales are explained;
`fullness_basis_points` measures fullness (0 starving, 10,000 full).
Self context is included in the queued-observation digest. Nearby relationships,
carried inventory and current activity are not provided. During a world-born
child's pending first identity choice, self context also contains at most two
biological parents' recorded names and chosen personalities/aspirations as
`family_background`. An unchosen parental identity remains unknown; a deceased
parent's last recorded identity may supply the background. Private thoughts,
memories and unrelated identities are excluded. Household and Town keep their
existing recorded-name fields. This bounded snapshot is part of the ordinary
personal-model request and its digest, in both numeric and word-based need
formats; it creates no separate request or inherited traits.
Household IDs longer than the self-context limit use stable SHA-256 aliases
in ordinary decisions and post-death will requests. Short IDs stay unchanged.
Saved membership, property and other household references retain the complete
household identity, including one founded by a native-born adult.

Native births use the existing `IdentityChoicePending` marker and the
`undecided`/`find a purpose` placeholders. Infants still receive no cognition.
At the first ordinary decision after infancy (currently day 3), both chosen
identity fields must be usable before the opportunity completes. A missing,
invalid, partial, failed or cancelled reply leaves it pending for the next
ordinary decision; the existing idle and action cadences still apply. Accepted
identity completes retained queued observations and removes their initial
family background. Later ordinary replies cannot overwrite the chosen fields;
the existing bounded life-moment opportunities remain separate.

Jev's routine request
sends the same three needs as flat fields after `fullness_basis_points`:
`warmth_basis_points` and `illness_basis_points`, `null` when unknown, with
their scales explained in its instructions.

`ModelNeedWords` holds the agreed alternative from
[#646](https://github.com/ClankerWorldOrg/ClankerWorld/issues/646): each need as a
word followed by its whole scale, worst to best, with no exact value, such as
`"fullness": "hungry (starving, hungry, fine, full; starving is worst, full is best)"`.
Fullness is *full* from 70%, *fine* from 40%, *hungry* from 20% and *starving*
below that, matching the comfortable and urgent food references. Warmth is
*warm* from 60%, *chilly* from 35% and *freezing* below that. Illness is *well*
below 25%, *unwell* below 50%, *ill* below 75% and *very ill* from there: each
step is where illness slows work further (the owner's choice on
[#672](https://github.com/ClankerWorldOrg/ClankerWorld/issues/672)). In words, the
personal request sends `fullness`, `self.warmth` and `self.illness` in place of
the three `_basis_points` fields, in the same positions, and drops the numeric
scale sentences; Jev's routine request sends `fullness`, `warmth` and `illness`
the same way. Unknown warmth or illness stays `null`. Nothing else in either request changes, and
the observation, its digest, admission and the simulation keep the exact values.

Both adapters take a `needFormat` setting. `ModelNeedWords.DefaultFormat` is
`Numbers`, so play sends numbers. Words were to become the default only if they
did no worse than numbers with GLM 5.3 Flash and GPT 6 Luna; they did worse
with GLM 5.3 Flash, and on October 2 the owner chose to keep numbers.
[Need wording comparison](../need-wording-comparison.md) describes the comparison
harness and its results.

Newly placed adults get one opportunity to choose personality and aspiration
in the existing first personal-model action reply. Optional `chosen_personality`
and `chosen_aspiration` strings are trimmed, limited to 256 characters and
refused if they contain control characters. An accepted personal reply consumes
the opportunity even if either field is missing or invalid; the placeholder
stays without an additional model attempt. Routine replies cannot overwrite it.
The pending opportunity is checkpointed, so pause/reload discards late replies
and preserves an unconsumed choice. It selects the personal planner rather than
Jev's routine router. Choice events contain only the agent ID; chosen text stays
in that agent's saved state and later self context, not runtime logs. World-born
children use the ordinary first-choice path described above and require both
usable identity fields before completing it.

The runtime records five named identity opportunities: midlife (half the
configured maximum life, day 30 by default), parenthood, loss of a partner,
loss of a biological parent, and becoming an elder. Each kind is recorded
once per agent. After the initial identity choice, and outside urgent needs
or another model turn, a separate request gives only the current identity and
the named moment. It requires that agent's personal planner, never a world
default or Jev. The existing installation usage meter reserves the call.
The request has a 15-second timeout; accepted optional fields are limited to
256 characters each and contain no control characters. Invalid fields refuse
the entire change. An absent, declined, failed or interrupted reply consumes
the opportunity without retry. With no selected personal model it keeps the
identity without a call.

Death revokes unanswered relationship proposals. Only an accepted partnership
ends by death, giving the survivor the partner-loss opportunity and identifying
them as a partner in the deceased's will context.

Life-moment replies are admitted at the live tick commit boundary, rechecking
the actor, current identity, run epoch and selected provider. Pause and presence
loss cancel outstanding requests. A saved in-flight opportunity becomes
interrupted when play resumes; death finalizes it in the deceased archive.
The moment record saves accepted fields and timing. Events contain only agent
IDs and the moment kind; runtime logs record only that an agent changed its
identity. Accepted text appears in the owner profile and later self context.
The offline bounded test reaches all five moments, observes exactly five
requests, and confirms that more ticks and a reload add none.

Request text uses the game's own words (*agent*, *Town*, *House*), not the older
*inhabitant* and *settlement*, including plurals. Both adapters omit the
clock and run/decision counters; admission uses them on the server. Households
and Towns use their recorded names. Candidate destinations use a readable name
where one is available, while the legal option IDs remain unchanged. Jev stays
narrow: choosing a legal action and scoring existing memories, without persona.
Retrieval ranks which memories to include before sending the request. Its Jev
importance scores are not sent to the personal model: those scores select the
context, rather than adding facts about the remembered event. Belief provenance
and belief confidence remain explicit.
When an unnamed agent is asked to choose a full name, the personal-model
request includes a soft first-letter hint derived from that agent's stable ID.
The hint stays the same if the request is retried, is computed per agent, and
does not reveal anyone else's name. If a current reply supplies a valid name
whose first token is already chosen by another agent, the scheduler queues one
extra metered personal-model request. That request marks `name_retry` and says
the first name is taken, without including anyone else's name. First names use
NFC normalization, collapsed Unicode whitespace and `OrdinalIgnoreCase`;
different middle names or surnames do not avoid a collision. Deceased agents
with chosen names count too. A second duplicate, a missing or invalid name, or
an unusable retry reply leaves the placeholder for the player to rename. The
retry marker uses the existing saved cognition queue trigger list, so it
survives pause and restore. Separately, required saved `HasChosenName` records
whether the display name was chosen. `NeedsName` schedules automatic naming;
closing that opportunity preserves a placeholder's unchosen status. Neither
open nor closed placeholders reserve first names, and recent-event compaction
cannot turn one into a chosen name. The name check
is separate from action admission: a valid name from a current legal-choice,
low-confidence or rejected-action reply is kept, while malformed replies and
stale replies cannot name the agent.

Player renames reuse `AgentNameRules`, including NFC normalization,
collapsed Unicode whitespace and `OrdinalIgnoreCase` comparison. The runtime
checks the first names of all other agents with `HasChosenName`, living
or deceased, under the same world gate that commits the rename. Keeping one's
own first name is allowed. A taken first name returns `name_taken` from
the signed owner endpoint; the client translates only that refusal into a
name-specific explanation. The Profile's open name field keeps unsubmitted
edits through ordinary refreshes, regardless of keyboard focus. Submitted and
refused attempts also stay in the field (`RefusedAgentRename`). Closing the
editor, renaming successfully, or showing another agent or world drops the
attempt; its name labels always follow the server's snapshot. An unchanged chosen
name is a no-op; deliberately choosing the exact displayed placeholder makes
it a chosen name and reserves its first token. No name check rewrites saved
dialogue or identity references, and player choices still supersede late model
naming replies.

A changed player rename and its permanent memory publish in the same Society
operation. `RenameFromPlayer` records the accepted normalized name and current
world day as a private self-subject experience. A stable, bounded ID combines
the saved world-event ordinal and an actor hash, distinguishing several renames at
one tick and preserving their order across reload. Married surname changes
record a memory only for each actually changed spouse, inside the existing
all-or-nothing operation. Long descendant IDs keep their full saved ownership
while the model excerpt uses the existing bounded subject alias. Automatic naming and marriage surname choices do not
create player-rename memories. A refused or unchanged-name request creates no
memory. Deceased rename history remains inspectable. Undoing a founder before
the world starts removes that person and their setup history.

Native births omit a chosen name and retain a placeholder with `NeedsName`.
Infants remain excluded from personal-model dispatch. Once ordinary eligibility
allows a naming request, bounded `self.allowed_child_surnames` lists only the
chosen biological parents' surnames; an empty list means none is available.
Player renames and explicit `CommitBirth` names use the same admission rule
for a child: the final name token must match one biological parent's surname.
An explicit birth name is checked before food or child records change. The
rule includes deceased biological parents and does not substitute caregivers.
An invalid surname follows the existing unusable-name outcome; only a taken
first name earns the extra paid retry. Parent renames do not retroactively
invalidate a child's name or saved state.
`RenameAgent` refuses removal of a chosen parent's surname when the
proposed checkpoint would leave any minor biological child with no allowed
surname. The check runs before commit, so player refusal publishes no name,
event or permanent memory. Other chosen biological parents, including deceased
parents, can still supply a surname. Initial unchosen parents and saved child
names keep their existing treatment; no new saved-state invariant is added.

The response must select a legal candidate. Any finite confidence from 0 to 1
is accepted; confidence does not veto the choice or a valid chosen name.
The personal prompt asks for no probability map. Both parsers accept its
absence; a supplied valid map is retained in the existing response format,
but does not select the action. A current reply that names a candidate never offered to it, or has
invalid response values such as confidence outside 0–1, also completes with
safe idle instead of repeatedly spending calls on the same decision. A choice
that was offered but is no longer legal stays stale; request, provider and run
identity checks still reject late replies without applying fallback.
A reply for a different request cannot cancel the agent's current pending choice
or replace its last accepted intention.
No adapter can turn provider prose directly into a world mutation.
Jev has a separate, smaller routine payload; it is not a persona/dialogue adapter.

Each queued decision makes one model attempt. Malformed replies, timeouts and
transport failures do not trigger a repair call or an unstructured alternate
request. Every billable attempt is reserved in the durable usage store before
HTTP; an absent key or exhausted limit sends no request. Future world decisions
still follow the normal reevaluation rules. The separate, explicitly agreed
duplicate-name request in [#456](https://github.com/ClankerWorldOrg/ClankerWorld/issues/456)
is not a repair of an action reply.

The owner agent card reports a bounded model status: ready, waiting, canceled,
missing key, limit reached, unusable reply, timed out or unavailable. The latest
attempt and last accepted model choice are saved separately, so safe-idle
fallback does not erase the previous accepted choice. Pause/disconnect records
cancellation; refreshed views and current-format reload preserve it. Exception
messages, provider bodies and keys do not enter these fields.

OpenAI and Ollama Cloud personal requests send JSON-object response mode and
`max_completion_tokens: 16000`, without temperature. Personal decisions, wills,
conversations and Test model share this output ceiling with Anthropic; it allows
room for thinking as well as the bounded reply. Providers that cannot support
the ceiling refuse through the existing failure path, without another paid
attempt. The candidate list and routine-helper role are unchanged.

Model-written names, identity, thoughts, observer replies, civic prose and public
speech remove invisible formatting before their existing length and control
checks. `BoundedText` shares the invisible-character classification used by final
words. First-name and surname comparisons remove the same characters, including
in already saved names. Reported provider model names pass the shared 256-unit
printable-text boundary before usage is saved or shown; unusable reported names
are discarded without losing token counts.

**Claude models.** An agent whose provider is Anthropic gets the same prompts
and the same reply validation. `AnthropicModelClient` in the server sends them to
Anthropic's Messages API through Anthropic's official C# client
(`IModelClient` is the seam in the simulation). The game's instructions
are the system prompt, marked for prompt caching: calls that repeat them cost
less once they are long enough for the model to cache, and a shorter prompt is
simply not cached. The observation is the one user message. The Messages API
requires an output limit, so the client allows 16,000 tokens, room for the
model's thinking as well as the short JSON reply; an older model typed by name
with a smaller output limit refuses the request. There is no JSON response mode in these
requests: the prompt's "Return JSON only" asks for JSON, and one surrounding
Markdown code fence is removed before the game reads the text.
- Only text blocks come back. Thinking blocks are dropped and never logged or
  saved.
- A refusal, or a reply cut off at the limit, is an unusable reply. An HTTP
  error keeps its status, so a 400 is an unsupported request, as on the
  chat-completions path.
- The client never retries, because a retry is another paid call. It sends
  only the owner's key: it never falls back to the server's own Anthropic token
  or login profile.
- A refused or cut-off reply still records its reported token counts as a
  failed call, for decisions, conversations and Test model checks. Decision
  parsing failures also retain the usage from the completed provider reply.
- Usage counts every input token the model read, cached ones included, and the
  output tokens, which include thinking.
- The short list is `claude-opus-5-5`, `claude-sonnet-5-5` and the default
  `claude-haiku-5-5`. Conversations use the same client and the same strict
  turn validation. Claude is not a routine helper.

**Thinking.** Each agent model assignment can carry a thinking level: `low`,
`medium` or `high` (`ModelThinking`), or none for the model's own default.
OpenAI and Ollama Cloud receive it as `reasoning_effort`, and Anthropic as
`output_config.effort`. With no level nothing is sent, so those requests are
unchanged. The game offers only the levels every model provider accepts:
`none`, `xhigh` and `max` are refused by some current models, so there is no
Off. A model that refuses the setting fails the call like any other
unsupported request, and Test model then suggests Model default. The level is
set per agent with its model; world defaults have none. A personal choice's
routine and planning rows share it, and a child born in the world starts with
the model default.
