---
title: Derived identities
type: architecture
status: active
updated: 2026-10-13
---

# Derived identities

`DerivedId.MaximumLength` is the shared 128 UTF-16-unit boundary for IDs
assembled from other IDs. `Create` preserves the complete short spelling and
hashes the complete longer value with SHA-256. A generator passes its full
parts before hashing; it does not cut an ancestor, owner, lot or recipient
reference to make the result fit.

`Hash` and `HashBase64Url` preserve generators that already used a digest,
including their existing digest lengths and prefixes. Their prefix and digest
must fit the shared boundary. `Numbered` hashes the complete long scope and
counter, then retains the counter as a suffix. `MatchesNumbered` checks that
counter and recomputes the exact handle from the original scope.

The record's separate references keep their complete values. Names are display
text. Standalone IDs, mod content IDs, action payloads, transaction keys and
version digests retain their own contracts. For example, a repair candidate
contains the exact lot ID so the executor can parse its target; it is not an
opaque receipt that can be replaced by a hash.

## Shared identity and text types

`Identity.Create` uses `DerivedId` for generated handles. `Identity.Parse`
accepts an existing bounded handle without trimming, normalizing or hashing it.
The JSON converter writes the same string and dictionary-key spelling; there
is no implicit conversion from a string into an identity. Complete saved
references use `IdentityRules.Reference`, with the caller's existing bound and
canonical-whitespace requirements. References are never shortened to fit a
handle. Belief handles retain the stricter telemetry-safe character contract.

`BoundedText.Create` and `BoundedText.Excerpt` share one cut in UTF-16 units.
They keep both units of a supplementary Unicode character together. Excerpts
choose their existing normalization and suffix explicitly: unchanged text,
collapsed whitespace, or controls replaced by spaces; an ellipsis is optional
and counts toward the limit. Names and source text keep their original values.

`IdentityTextBoundaryTests` scans production source for hand-built IDs in
identity-valued assignments, returns and arguments, and for literal length
comparisons and relational length patterns. Existing expressions are recorded
in `tests/ClankerWorld.Tests/Architecture/identity-text-uses.json`. Each
allowance belongs to its exact file and expression; it cannot fund a new use.
The check requires obsolete allowances to be removed and compares the file
with the checkout's first parent to prevent additions. CI fetches that parent.
Route new handles through the factories and text/reference checks through the
shared rules. The remaining allowances track work outside the first typed
storage boundaries of [#1819](https://github.com/ClankerWorldOrg/ClankerWorld/issues/1819).

## Executable generator inventory

The native inventory uses three actual accepted births. Its milk trace performs
50 ordinary kernel barters with both parties consenting, exclusive holds,
whole-lot milk and partial wood payments. Milk retains its identity; each new
payment lot preserves its source. Each barter saves and strictly reloads the
world, then recalls a memory keyed by its new payment ID. This is a
controlled-stock kernel trace. The admitted Store and Market purchase workflows
have separate native regression coverage.

The following matrices list the production generators they exercise. Pure
factory rows invoke the production method, rather than copying its formula.
They save the generated ID as a real social-memory key in the native world,
strictly reload to identical checkpoint bytes, retrieve that memory for its
actual owner and validate the resulting cognition observation. Controlled
factory-input records do not represent model-admitted civic or owner orders.

| Inventory | Production generators and variants |
| --- | --- |
| `DerivedInventoryIdTests` | Partial transfer, containment, take, relocation, handcart load/unload, barter holds and settlement, milk and payment splits |
| `PaidMilkDerivedIdTests` | Fifty successive ordinary milk barters, both consents, payment quantities, stable milk identity, completed reservations, complete payment source links and recall after every barter |
| `DerivedSocietyIdTests` | Native child and request IDs, protected parentage/care/membership records, birth food holds, domestic-person and frozen birth-family handles, household creation/join/rejoin, consumption holds and memory summaries |
| `DerivedWorkIdTests` | Native personal and borrowed tool repair, partial tool/material splits, gifts to the household, completed material reservations and owner-order completion receipts; actual paid placement of a 128-character building instance |
| `DerivedCivicIdTests` | Council proposals, project requests/projects/buildings/deliveries and delivery/deposit suffixes, market/stall/stock/offer handles, land grants/claims/cases/revisions/property/transfers/payments and hearing-generated rights/sources, law adoption/amendment/repeal/record handles including typed estate amendments, government and judge rounds, nominations/admissions, non-land grants, hearing notices/law evidence/party handles, marriage/surname/writing handles, every numbered Town record family and all five nonviolent file-ID kinds |
| `DerivedPrivateFactoryTests` | Knowledge/recipe/cognition aliases, food/boat/guardian arrivals, wills, automatic buildings and replacements, construction/production/expansion/tool-request/cart/cargo/repair/talk/shelter/read receipts, civic/evidence/action tokens, ornaments and salvage choices/operations; medical, food/material/tree harvests, orchard holds, tool use/repair/gifts, field planting/tool/crop/seed/replant handles, equipment and indexed vessel units; guardian/dependent/replacement care, membership collision suffixes, domestic-family splits, estates/storage variants and final words; parenthood/partnership/solo-household/departure/personal-transfer handles, childhood/lesson/project/trust memories, roads, trade and writing; conduct/seen/inspection/relay/remedy handles and work receipts; conversation/turn/wrap-up/claim/context and rename memories; animal choices/products/births/herds/saddle/milk holds, boat choices |

The civic matrix preserves a named saved Town with a 128-character ID and a
120-character name, a 128-character household name and an 80-character project
name. The private matrix retains actual native people and stock, with controlled
128-character cart, tool and orchard-seed lot IDs. Animal factory rows use a
controlled 128-character animal reference; they do not add a live animal or
claim an admitted animal workflow. Numbered rows also exercise
the maximum supported counter. Long complete source-reference regressions
remain separate from the generated-ID boundary.

The legacy automatic-building row supplies a controlled identifier in the old
content-instance grammar. Its native-descendant row retains the actual native
person. Rename handles use the production memory contract: private permanent
contents, current source tick and the owner as subject.

## Generated agent IDs

A child born in the world gets `DerivedId.Create("{worldId}:agent:{request}")`
from its accepted birth request. Saves made before the
[#1364](https://github.com/compoodment/ClankerWorld/issues/1364) rename spelled
it `:inhabitant:`; those saves are refused by schema, so no producer or reader
accepts the old spelling. The same rename changed the text of these
generators: `direct-barter:` (direct barter offers), `shop-trade:` (shop
trades), `partnership:` and `care-relationship:` (relationship records) and
`trust-memory:` (trust memories).

## Matching generated IDs

Producers and strict validators must use the same factory and complete inputs.
Do not recognize a hashed ID by searching its old concatenated prefix. The
birth-food, production-input, market/payment, inheritance, orchard and civic
bindings recompute their exact generated handle. A provenance field identifies
the original source even when the new opaque ID no longer embeds that source.

The named work, Society and Town factories expose their existing shapes to the
inventory. They introduce no second bound or hashing policy. When adding a
derived generator, route it through `DerivedId`, add its actual factory or
transition to the native inventory, and check any saved producer/consumer
recomputation together.

See [Saves and replay](saves-and-replay.md#bounded-derived-identities) for the
schema boundary and older-save behavior.
