---
title: Agents and their models
type: product-status
status: active
updated: 2026-11-14
---

# Agents and their models

Agents, their models, memories and conversations.

See [orders and goods](orders-and-goods.md) and [orders for work](orders-for-work.md) for instructions.

## Agents and their models

Each starting agent has a provider, model and key assignment. Stored keys can
be shared or selected independently. An agent's Profile lets you change these
choices and rename the person. For OpenAI, Ollama Cloud and Anthropic, the model
is picked from the game's own short list, newest at the top, or typed by name.
Anthropic offers Claude Opus 5.5, Claude Sonnet 5.5 and Claude Haiku 5.5, and a
new Anthropic agent starts on Claude Haiku 5.5. Each of these agents also has a
**Thinking** setting, chosen with its model: **Model default**, **Low**,
**Medium** or **High**. A model that refuses the setting fails its calls until
it is set back to Model default. The server checks the chosen key with the
provider and greys out listed models it can't use. That check, the listed model
names, the Claude requests and the thinking setting have been tested against
recorded sample replies, not yet against live provider accounts. A paused world's routine
helper can be Off, Jev or OpenAI Decisions, with its own model and key pickers.
**Settings → Game → API keys** saves named TypeSafe keys for Jev alongside
OpenAI, Ollama Cloud and Anthropic keys. Jev starts on **jev-1.14.0**; an
explicitly saved **jev-1.13.0** selection is retained. Unused keys can be
deleted from API keys; a key used by the active helper is protected from deletion. Decisions
uses a saved OpenAI key and handles the same routine choices and scoring of each
agent's private memories as Jev. Both helpers serve adults and elders, including people
born in the world. Children keep their own model; conversations, identity,
wills and planning use the agent's personal model throughout life. Switching
cancels pending work and preserves memories. **Test helper · 1 paid call**
checks the selected helper and saved key immediately; applying a helper only
selects it. Settings explain why choices with planning options still use the
personal model. Helper checks use the same signed, metered and bounded path as
personal-model tests, with sample replies covered automatically.
The Decisions integration is checked against the published API contract and
sample replies; the live-account Windows check is still [pending](../../playtest/alpha-1.md).
The saved TypeSafe-key path is covered by automated checks; its live-account
Windows check is also [pending](../../playtest/alpha-1.md#agents-models-and-conversations).
When a personal model names a new agent, it gets a stable first-letter hint to
encourage varied names. Each chosen first name is unique across the world,
including deceased agents. Different surnames, capitalization or spacing do
not make a taken first name available. The game asks once more after a
collision, without showing anyone else's name. If the second answer is
unavailable or also taken, the person keeps the placeholder until the player
changes it. Temporary names never reserve a first name, even after automatic
naming ends.

Player renames follow the same rule. An agent may keep their own first name
while changing the rest of their name. The Profile explains that the name is taken and
keeps the name field open with your attempt in it until you change it, close
it or choose another agent. This makes no model request. Renaming keeps the
same person and leaves past spoken lines as they were. Each actual player rename
adds a private, permanent memory with the world day and accepted name. A
married surname change records it for both spouses whose names change; other
agents are not told. Memories marks it Permanent. The model and Memories list
keep the latest permanent records as ordinary experiences accumulate, and earlier
rename history remains saved. Refusal and an unchanged-name retry add nothing.
This has automated runtime, signed HTTP, save/replay and native UI checks;
[Windows checks](../../playtest/alpha-1.md) remain pending.

A newborn starts with a temporary name. A chosen child's name must use one
biological parent's surname and an unused first name. The player can name the
child in Profile; automatic naming waits until the child is old enough for
ordinary personal-model calls. Later parent renames do not change a child's
existing name. Removing the last chosen biological-parent surname available
to a minor child is refused before the parent's name, events or rename memory
change. Removing one parent's surname still works when another biological
parent supplies a chosen surname.

A child's personal model is recorded at birth from the parents' explicit
personal assignments. When those differ, the parent who began the family plan
is the disclosed tie-break. Infants make no personal-model calls. After
infancy, the child uses the saved model only when its original key is available
and normal call checks allow it; if the key is missing, built-in choices keep
the child safe and the game does not substitute another paid model. The child
card shows the selected model or that it needs setup. Children whose parents
have no explicit model remain unconfigured; world defaults are not inherited.
You can later choose a different personal model or explicitly clear it;
that setting is saved, and a cleared child continues with built-in choices.
This setup supports long valid world seeds and recovers saved newborns whose
provider setup was interrupted. Once the child becomes an adult, the world's
selected routine helper can handle routine choices and score that adult's own
memories. Turning the helper off restores the personal model or built-in
choices; it does not inherit a paid world default.

Personal models choose from legal actions. Each request gives the agent's
name, life stage, personality, aspiration, household, fullness, and warmth and
illness where known. It also gives their latest private thought, a few relevant
memories and some places they know. Jev's routine choices also see fullness,
warmth and illness. Needs are sent as exact numbers. Describing them in plain
words on a stated scale, such as "hungry (starving, hungry, fine, full;
starving is worst, full is best)", is built for personal models and Jev but
switched off: compared with real models, it did worse than numbers with one of
the two models tried ([#672](https://github.com/compoodment/ClankerWorld/issues/672)).
Newly placed adults can choose their own
personality and aspiration in their first personal-model reply. The choice is
saved and shown on their profile. A missing or invalid choice keeps "undecided"
and "find a purpose" without an extra call; routine replies cannot overwrite it.
At the middle of life, becoming a parent or elder, or losing a partner or
parent, an agent gets one separate chance to reconsider its personality and
aspiration using its selected personal model. Each kind of moment is offered
once, for up to five extra model requests in a life. The agent may keep its
current choices. Missing or invalid replies, a pause, disconnection or a reload
while waiting keep the current identity and do not automatically retry the
opportunity. Accepted changes and their reasons appear on the profile and
survive saving. These moments never change names or choose a physical action.
Children born in the world start with their own identity choice pending.
Their first personal-model decision after infancy, at day 3, includes their
biological parents' chosen personalities and aspirations, alongside their
household and Town. The child chooses its own personality and aspiration;
the game does not mix or assign parental traits. Missing, invalid, partial or
refused replies keep the choice pending for the next ordinary decision, with
no separate identity call. Saving keeps both pending and completed choices.
Parents' private thoughts and memories stay private. The household
and Town are sent by their recorded names. Requests contain that agent's
recorded context and learned information, not a complete view of nearby people,
relationships, belongings or activities. Better pacing
or thought quality from these changes has not been proven in live play.

One slow model can wait while other agents and the world continue. The waiting
agent keeps its already chosen physical task, including ongoing project work,
lessons and trips. Built-in rules meet urgent food or warmth needs and provide
practical care for nearby dependants. New civic, family, ownership and work
commitments wait for a personal-model choice; a delay starts no new agreement
and adds no failure line to the Event Log. A legal
choice is accepted even when the model reports low confidence. A failed or
unusable reply uses the safe fallback and finishes that attempt without another
paid repair request. The agent card shows whether the model is ready, waiting,
canceled, missing a key, out of allowed calls, unable to give a usable reply,
timed out or unavailable. It shows the last accepted model choice separately.
A small circling gold spark appears after two seconds of waiting and clears
on reply or pause. It remains separate from the conversation badge. The Event
Log names the agent and failure reason once while the same problem continues;
a successful reply allows a later failure to appear again. Waiting, successful
replies and canceled requests do not add rows.
Those facts survive a refresh and save/reload. The paired Windows/model-wait
check remains in [the playtest list](../../playtest/alpha-1.md).

**Settings → Game → Model calls**, also reachable from the Main Menu, shows the
installation's model-call count and its optional limit. One count and limit
cover every world and every call attempt, including failed, retried and
abandoned ones. The count never resets by itself, and loading an older save
does not lower it. Provider-reported tokens appear only as information. When
a call brings the count to 80% of the limit, the server adds one Event Log warning.
It belongs to the world active when the server records it; switching worlds while
it waits can change which world receives it. A changed limit sets a new 80%
mark that only later calls can cross. Reaching the limit pauses
the world until the limit is raised or more calls are allowed and the world is
resumed. After that, time keeps running under the new allowance, even if an
earlier limit notice was delayed. Per-world limits and cost estimates are not offered. The Windows
check is in [the playtest list](../../playtest/alpha-1.md).

Thoughts, memories, beliefs and explored map facts belong to the individual
agent. The player can inspect mistakes and where a belief came from. Jev can
rank existing memories during a normal call. Once per world day, ordinary
experiences and beliefs at least three world days old with importance below
25% move into each agent's saved private archive, which ordinary recall skips.
Unscored records use zero importance, so this also works with the routine helper
off. Life, relationship, skill and commitment records stay active; open work
and conversations postpone archiving. Only requested or preparing parenthood plans
postpone it; completed, postponed and cancelled plans keep their saved history without
protecting otherwise eligible old records. These age and importance limits are
provisional. Original evidence and corrections survive saves. With the routine helper on,
an existing call can choose a shorter extract of four to twelve old private
records. Its saved summary keeps exact source links and can be recalled alongside
active memories, even after switching the helper off. Sources retain who said
something, hearsay and uncertainty; no other agent receives those private records. Agents can also start a nearby
public conversation: each speaker uses their own assigned planning model,
accepted speech is saved with the people who could hear it, and only those
listeners receive a hearsay memory, including descendants with long generated
identities. A conversation can have up to six public
turns and one wrap-up; both people must accept the same wrap-up before its
structured effect applies. These conversation and daily-call limits are still
provisional and need a Windows playtest. Private thoughts are never shared as
conversation history. Every unfinished conversation remains visible to the game,
including after a reconnect or reload; recent closed history shows up to 16
conversations. Other automatic experience capture remains unfinished. Summary timing and
usefulness still need a Windows playtest.

Partnered adults can propose marriage through that conversation's wrap-up.
Each partner separately accepts or declines with their selected personal model.
Mutual marriage consent starts a dedicated surname conversation, which can
continue remotely and has at most four alternating valid turns. Only the
partners' original surnames are allowed; agreement ends it early, otherwise a
disclosed seeded draw resolves it after four turns. Failed calls, pauses and
cancellations consume no surname turn. Consent, surname progress and the final
result survive saves and ordinary conversation-history compaction. Profiles
show the spouse and shared surname, and later player surname changes update
both spouses together while keeping the original conversation unchanged. A
taken first name leaves both names unchanged. Either partner can end the
partnership and marriage alone, and a partner's death ends the marriage. Both
names stay unchanged. Former partners may form a new partnership and marry
again through fresh consent and surname conversations. Profiles keep ended
marriages as history; a later surname change affects only a current spouse.
Unfinished surname sessions close when their marriage ends, and save/reload
keeps the ending and original consent.
