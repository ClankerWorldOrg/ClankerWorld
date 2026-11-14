---
title: Wills and estates
type: architecture
status: active
updated: 2026-11-14
---

# Wills and estates

This page describes wills, estates and final words.

### Wills

An estate with frozen lots and an agent with a personal model gets exactly one
post-death request (`WorldRuntime.Wills`). Its `CognitionWillContext`
offers the frozen lots as `item:1`… keys (at most 24, in lot-ID order) and up to
sixteen living people as `will:heir:{id}` keys, family and household first, plus
`will:town:{id}` for the archived Town when it has a Warehouse of its own. The
candidates are `will:household` and, when anyone may inherit, `will:heirs`.
The stable `will:household` key chooses default inheritance. Its description
includes any supported Town share effective at death; candidate descriptions
are bound into the request digest. The historical profile labels this outcome
as default inheritance, since a Town law can change the household's share.
The model's reply (`CognitionWillChoice`) is untrusted: the response must match
the request, epochs and digest; the runtime maps only offered keys back to
heirs and lots; and `SocietyRules.ResolveWill` checks that every heir is a
living person other than the deceased or an offered Town, that there are one
to three distinct heirs, and that every listed lot is still frozen in this
estate. Anything else resolves to default inheritance.

`ResolveWill` stores the exact division as `WillBequests` (lot, heir,
quantity). "items" gives each listed lot whole to its heir. Every other lot,
and every lot under "equal", is divided equally: each heir gets the same whole
number of units, and the units left over go one at a time to the heirs in the
order the will names them, continuing from where the previous lot's leftovers
stopped. Lots are taken in lot-ID order, so each lot's parts always sum to its
frozen quantity. A storage pot, water jug or handcart counts as one unit, and its
contents always go with it to the same heir; only top-level lots are offered to the
model, with a vessel's contents described beside it.

Settlement runs once, when the escrow expires and no will is pending. A
living person heir, including a child, owns their part without automatically
carrying it. Ground lots keep their tile, stored lots retain their recorded
storage, and goods held by a living carrier remain in that carrier's custody.
Goods carried by the deceased are dropped at their last tile. A Town heir's
part goes to its Warehouse as Town stock while the Warehouse has
room and stores that kind; the runtime passes each Town's Warehouse, free room
and refused kinds (food, and handcarts, which stay on the ground) as
`SocietyTownStore`. Food refusal and Warehouse validation use the inventory's
food classifier, including eggs, milk, cooked eggs, milk porridge and rich
meals. The refusal set is built from actual stock only when an estate is due.
Whatever the will cannot deliver (a share for an heir who
has since died, food, a handcart or goods beyond the room) follows the
household default: an equal split between the living household
beneficiaries, with the first in ID order taking leftovers, or the deceased
resident's recorded Town when none remain. In a default estate, that Town's
Warehouse receives eligible goods while space remains. Undeliverable shares
from an accepted will and overflow keep their original location and Town
ownership, with separate lot IDs for Warehouse and ground portions.
A vessel and its contents move as one family and keep their
lot IDs: the Town takes a family only when the Warehouse accepts every kind in
it and has room for all of it, otherwise the family follows the household
default, where vessels rotate between the living beneficiaries. A quantity-one
map, field record or book keeps its lot ID.

`TownEstateDefaultRule` is a typed Resident-duty law proposed and amended by
the ordinary Council process, with 0%, 25%, 50%, 75% and 100% choices offered
to personal models. Free-form law text cannot create or amend that effect.
When settlement is due, `DefaultEstateDivisionsForDueEstates` derives the rule
from the deceased resident's saved Town and the version in force at death,
strictly after that version's adoption. Later amendment or repeal cannot
retarget the estate. Accepted wills always retain priority. Without one, the
Town share is rounded down per lot and bounded by eligible Warehouse capacity;
the remainder follows the household path. A whole vessel family is eligible
for the law's Town share only at 100%, with room for all its contents.
No living person's ownership changes when the law passes.

Final words are optional with either outcome. `CognitionWillChoice.NormalizeFinalWords`
preserves complete printable Unicode characters, turns control, invisible
formatting, private-use and malformed UTF-16 characters into spaces, collapses
spaces, and refuses text over 80 UTF-16 units or containing markup characters
(`< > [ ] { }` and backticks). The HTTP provider parser drops unusable words
before admission; a directly supplied typed reply with invalid words is refused.
An admitted reply keeps its words even when its division falls back. At settlement
each living person who receives goods gets the private memory
`final-words:{estate}:{heir}` ("Name's final words were: '…'"). New keys over
128 characters use `final-words:sha256:` followed by a hash of that complete
key, so native-born heirs can retrieve the words in later personal decisions.
Short keys stay unchanged. The saved owner and deceased subject keep their
complete identities; text, privacy, kind and source tick stay unchanged.
The owner sees
the words on the historical profile through
`OwnerWorldFinalWill`; events, logs and telemetry carry only IDs, the split and heir
counts, never the words. See [saves and replay](../saves-and-replay.md) for
pending-will restore behavior.
