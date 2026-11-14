---
title: Markets and trade
type: persistence-reference
status: active
updated: 2026-11-14
---

# Markets and trade

Saved Markets, stalls, physical trade and paid lanterns.

## Paid Markets and stall trade

Schema 75 adds a required, non-null `Markets` list to each Town. Each Market
binds its hall and fixed 7×4 plaza to the completed starter project, which pays
for exactly two stalls in slots 0 and 4. Each additional stall requires its own
completed project with the exact definition, slot, site and material budget.
Loading rejects unpaid or duplicate slots, altered plans, unmatched buildings
and inconsistent removal history. Approved, partly supplied and completed
Market construction retains the shared Town project's physical load,
reservation, consumption and work records.

Named borrowing records bind actual living adults to a paid stall while they
remain inside the hall-and-plaza area. Stock receipts keep the personal or
household owner recorded at deposit, physical lot, quantity and occupancy
identity; live inventory remains authoritative after inheritance or collection.
Source and destination lot references retain the full native inventory identity
without a narrower receipt length limit. Whole and partial deposits from long
split/move histories remain valid; the receipt hash, owner, quantity, occupancy,
derived destination and exact retained inventory event still have to agree.
Another borrower gains no right to sell those goods. Barter records bind the
exact inventory offer, named seller and buyer, lots, reservations and outcome.
Completed or cancelled trades retain a recent history without requiring spent
goods to remain live. Schema 94 adds `NextStockReceiptSequence`, receipt
`Sequence`, trade `StockReceiptSequence` and `RetiredStockReceiptThrough`.
Sequences do not change when another record retires; required markers and strict
bounds reject missing, duplicate, reordered or unaccounted receipts. The live
receipt suffix above the retirement boundary must be complete. An open offer
always retains its exact source receipt. A closed outcome may refer below the
explicit retired boundary; that historical reference grants no new selling right.

Business exchanges, Market exchanges and Market stock receipts retain the exact
resource kind accepted by inventory and content quantities. Resource kinds have
no separate trade-record length limit and are never truncated. Loading still
rejects empty, untrimmed or control-character names, mismatched lot kinds and
forged offer or receipt bindings. This validation correction keeps the current
schema and does not change saved fields or transfer goods during reload.

Each Market keeps the most recent 32 receipts, 32 closed trades and 32 ended
borrowing records, plus exact live dependencies. Current borrowers keep the
latest matching deposit for each actual on-site lot and historical owner; repeated
collection and deposit cannot retain every obsolete receipt for the same goods.
Open trades retain their original deposit proof. Retained payment receipts keep
their settled offer and trade, and retained settlements keep their actual payment
receipt and both acceptances. These bindings do not recursively retain an
unlimited chain of earlier closed deposit proofs. Live claims block retirement;
other closed offers and their two claims retire together. Paid projects, buildings,
removal records and actual inventory owners, quantities and locations stay intact.

World and inventory events remain in the existing durable archive chain.
Retirement requires no archive lookup to establish live trading authority.
Checkpoint compaction archives event prefixes before installing the new history
head; missing or corrupt referenced archives still fail closed. Restoring an
older checkpoint retains that checkpoint's own bindings and continuing native
retirement decisions. Prepared-tick rejection and pause discard retirement with
the other world changes.

Loading checks owners, locations and the actual physical
occupancy instead of reconstructing authority from a stall's current borrower;
retained active claims of an open offer must bind its exact parties, lots, quantities,
purpose, expiry and exclusive reservation state. Missing or released claims remain
loadable and are cancelled before trade continues. Goods on a stall tile are not capped at load,
because a death or a removed stall can leave more there than a borrower may
deposit. Two standing Markets in one Town cannot share ground. Loading also
keeps a standing plaza clear of unrelated buildings, fields, claims, expansions,
resources, map objects and bridge ends; only its paid stalls and aisle Roads fit.

Leaving, household change, death or removal ends borrowing and cancels open
offers. Removed Market buildings keep their paid history. Actual owners can still collect
physical leftovers after obsolete selling receipts retire; another borrower needs
their own fresh deposit authority. Current-format
restore must preserve intermediate construction, borrowing, stock and open
trades without duplicating goods or approval. `MarketConstructionRuntimeTests`
covers a generated paid Market and additional stall, personal and household
stocking and barter, fresh consent, paused-plan ingredient buying, retrieval,
intermediate restores and malformed state. A buyer added outside all Town land
retains its independent household and no Town membership; open and settled
trades reload strictly, and its live and restored continuations remain
byte-identical after each continuing step.

Planting-stock demand reuses existing fields, inventory and reservations and
adds no saved field. The seed scenario creates its prepared field through
actual tilling, buys one grain seed, retains the supplier's remaining unit,
suppresses another same-kind purchase while the bought unit is held, then
consumes that exact unit through ordinary planting. Intermediate states reload
strictly with the same physical lots and claims. Stock, food consumption,
equipment and phase wakes are controlled test arrangements; title, field
placement and actor positions are retained from actual work and movement.
Earlier alpha checkpoints are refused and preserved
without migration.

Schema 79 adds two supported street-lantern definitions to that same paid
ledger. Their `Site` is a clear one-tile roadside footprint and `Entrance` is
the approved cardinally adjacent Road tile. The immutable definition selects
the style; the difference between those two positions binds its Road edge.
These positions and the exact trial budget are part of the approval scope,
not inferred later from a nearby Road. Completion keeps the bound entrance
and does not generate or extend streets. Live placement and current-format
validation require uncontested Town title and an existing Road, with the same
real delivery, reservation, work and removal receipts as the Hall. No fuel,
lit flag or separate rendering authority is saved: day/night appearance is
derived from the saved world clock. The owner projection supplies definition
tags as derived display data. Current roundtrip and replay retain the edge,
paid materials and partial custody. Earlier alpha saves are refused and
preserved without migration.
