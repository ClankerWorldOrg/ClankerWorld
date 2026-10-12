---
title: Code structure that prevents the common bugs
type: development-reference
status: active
updated: 2026-10-12
---

# Code structure that prevents the common bugs

The owner asked on October 12 for a structure that stops the same kinds of
bugs coming back. This page records which bugs keep happening, why the current
structure lets them happen, and the structure we are moving to. The refactor is
tracked in [#1814](https://github.com/compoodment/ClankerWorld/issues/1814); follow the rules below in new code now, even
before a step lands.

[How the game works](how-it-works.md) describes the code as it is today. Where
this page and that one differ, that page is the current behavior and this one
is the target.

## What keeps breaking

Between September 29 and October 12, 398 of about 800 merged pull requests
fixed bugs. Classified by root cause, not by symptom:

| Root cause | Fixes | What the structure gets wrong |
| --- | --- | --- |
| The same rule written in several places | 61 | An action's offer and its execution each choose their own goods, target or actor checks, and disagree |
| Game client screens | 51 | Each panel handles late replies, kept edits, focus and text fitting its own way |
| Flaky tests | 50 | Model calls run on background threads, and tests guess with timers when a reply is accepted |
| IDs and text that grow or get cut | 44 | IDs and bounded text are plain strings built by hand, checked by 15 different validators |
| Reaching places | 32 | Over 100 route calls each pick their own start, range, load and swimming rule |
| Other rule edge cases | 32 | Long processes (ballots, lessons, plans) track their state in one-off fields |
| Performance and growing lists | 30 | Choices are rebuilt from world scans every tick; histories are capped one list at a time |
| Everything else | 98 | Saves, pairing, ownership, orders, model replies, content |

Most P0 bugs were a **halted world**: 31 of 37 P0 game fixes. Most of those
were found only when the server encoded and strictly decoded the save after a
tick, far from the code that made the bad state. Halts are rising, from 2 in
the first period to 17 in the last three days, mostly from the ID and text
class and from agents born in the world, whose longer IDs the test worlds never
used.

## Rules for new code

Follow these now. A refactor step that lands turns a rule into a check.

1. **Never build an ID or cut text by hand.** Build IDs only through the
   derived-identity helpers ([Derived identities](derived-identities.md)).
   Don't add another length check, character check or excerpt function; until
   step 5 gives them one home, reuse the one the area already has.
2. **One definition per action.** An action's offer works out its plan once:
   which goods, target, route and quantity. Its execution checks that same plan
   again instead of choosing again. Apply candidate caps only after filtering.
3. **One answer per question.** Use the shared goods query for goods
   ([Finding goods](how-it-works.md#finding-goods)), and one shared helper each
   for life stage, need thresholds and reach. If the helper you need doesn't
   exist, add it in one place and use it everywhere you touch.
4. **Refuse, don't throw.** A choice that is no longer possible gets a refusal
   with a reason the event log can show. Exceptions are for programming errors
   only. Never catch an exception just to treat it as a bad model reply.
5. **Promises live on the stock.** Goods lent to a stall, promised to a
   delivery or held for a trip must not be taken by code that skips a filter.
   Until step 7 marks them on the stock itself, read goods only through the
   shared goods query, never from lots directly.
6. **The server decides what things mean.** The game shows what the server
   sends. It doesn't work out its own thresholds, activity names or event text
   from IDs.
7. **Tests don't guess with timers.** Once step 3 lands, drive model replies
   through its harness and assert the tick a reply is accepted at. Until then,
   reuse an existing shared wait rather than writing a new one, and add no new
   reflection into the runtime's private fields.
8. **Test with hard worlds.** Use long IDs, accented and emoji names, and a
   second generation of agents born in the world.

## Target structure

- **IDs and bounded text are types.** IDs are created and parsed only through
  one bounded factory each, keeping their saved spelling. Text limits and
  excerpts have one implementation that never cuts a character in half. A
  source check blocks new hand-built IDs and literal length checks.
- **Every offered choice is tested.** A test builds hard worlds, applies every
  choice the game offers in the trial copy the server already uses for
  refusals, and checks that nothing throws, the save round-trips strictly, and
  the choice either progresses or is refused with a reason. A short run checks
  every pull request; a long run checks nightly.
- **Model calls run through one scheduler.** The game keeps using background
  threads. Tests use a manual scheduler that shows each call's stage, from
  started to accepted at a tick, and a guard test blocks timer-based waits.
- **One definition per action** in the ordered action tables of
  [#1387](https://github.com/compoodment/ClankerWorld/issues/1387). Each row
  plans once, checks the same plan when the choice is offered, accepted and
  applied, declares the life stages and states it allows, and owns one ID
  codec. Reach is one service that starts from the agent's position and names
  its purpose and load.
- **Each piece of state has one writer.** Systems own their state; shared
  records (agents, buildings, inventory, orders) are changed only through named
  operations, each with a writer check like the existing agent writer scan.
  Each system checks its own state when it changes it; the save check only
  checks links between systems.
- **Orders are typed.** Each kind of order is its own record with only the
  fields it needs, its status follows one transition function, and finished
  orders are kept only as long as the game shows them.
- **Events carry who and what as data**, not as colon-joined detail text the
  game has to split.
- **One copy of the server–game contracts**, compiled into both, with a
  round-trip test that fails on a field one side doesn't map. The server
  decides labels and meanings.
- **One request lifecycle in the game.** Every owner request is tied to the
  world and panel that sent it, restores the controls on every outcome,
  including a lost reply, and never overwrites what the player is typing.

## Order of the steps

The scripted rename ([#1364](https://github.com/compoodment/ClankerWorld/issues/1364)) merges first, so the refactor works on the
final names. [#1814](https://github.com/compoodment/ClankerWorld/issues/1814) tracks the steps, in this order:

1. [#1815](https://github.com/compoodment/ClankerWorld/issues/1815) Test every offered choice against hard worlds and a strict save,
   using the trial copy the server already uses to refuse a failing action
   ([#1756](https://github.com/compoodment/ClankerWorld/issues/1756)).
2. [#1816](https://github.com/compoodment/ClankerWorld/issues/1816) Make the default test worlds hard.
3. [#1817](https://github.com/compoodment/ClankerWorld/issues/1817) Run model calls through a scheduler tests can control.
4. [#1818](https://github.com/compoodment/ClankerWorld/issues/1818) Move the timer-based tests onto it and block new ones.
5. [#1819](https://github.com/compoodment/ClankerWorld/issues/1819) Make IDs and bounded text types, with a source check. This builds
   on the bounded generated IDs from [#1704](https://github.com/compoodment/ClankerWorld/issues/1704).
6. [#1820](https://github.com/compoodment/ClankerWorld/issues/1820) One request lifecycle in the game, and full names that fit
   everywhere. [#1812](https://github.com/compoodment/ClankerWorld/issues/1812) fixes the lost-reply case on its own first.
7. [#1821](https://github.com/compoodment/ClankerWorld/issues/1821) Promises held on the stock in the inventory core.
8. [#1822](https://github.com/compoodment/ClankerWorld/issues/1822) One definition per action, one system at a time, with shared
   life-stage, need and reach rules. This extends the ordered action tables of
   [#1387](https://github.com/compoodment/ClankerWorld/issues/1387).
9. [#1823](https://github.com/compoodment/ClankerWorld/issues/1823) Typed orders with one status machine and a bounded history.
10. [#1824](https://github.com/compoodment/ClankerWorld/issues/1824) Events with typed payloads.
11. [#1825](https://github.com/compoodment/ClankerWorld/issues/1825) One copy of the server–game contracts, with labels from the
    server.
12. [#1826](https://github.com/compoodment/ClankerWorld/issues/1826) One writer per piece of state, with write-time checks, as the
    remaining runtime areas move into systems after [#1387](https://github.com/compoodment/ClankerWorld/issues/1387), continuing
    [#1377](https://github.com/compoodment/ClankerWorld/issues/1377).

The shared goods query ([#1366](https://github.com/compoodment/ClankerWorld/issues/1366)) and the runtime split ([#1377](https://github.com/compoodment/ClankerWorld/issues/1377)) continue as
planned; these steps build on them rather than replacing them. Removing the
old fixture world ([#1708](https://github.com/compoodment/ClankerWorld/issues/1708)) is already in progress.
