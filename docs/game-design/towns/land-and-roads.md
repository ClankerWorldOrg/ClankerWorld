---
title: Land and roads
type: game-design
status: active
updated: 2026-11-14
---

# Land and roads

Land use rights, Roads, bridges and outstanding building questions.

### Town land and household use rights

The **Town holds formal title** to land it claims; households hold inspectable
**use rights** for homes, farms and businesses rather than household land title.
A plot is a connected group of land tiles and can include empty land. The Town
border alone does not silently take over another claim or transfer a building,
its stock or a household's private goods. Physical occupation or model text
cannot rewrite these records. Every Town has a Council from founding; its
initial small Council consists of all adult residents. Land approvals use the
ordinary Council majority procedure from the start. Every Town also starts with
a vacant mayor’s office for land and non-land cases. Residents can later change
its duties or remove it through the protected government-change process.
Its term and elections are agreed in
[The mayor's office and elections](laws-and-enforcement.md#the-mayors-office-and-elections);
[Land hearings and rulings](laws-and-enforcement.md#land-hearings-and-rulings) settles adjudication.

**Agreed by the owner on October 2, 2026
([#426](https://github.com/compoodment/ClankerWorld/issues/426#issuecomment-5948873459)):**
a Town may extend its title through an explicit Council-approved claim to
adjoining unclaimed land. Use the legitimate current Council's ordinary
[proposal and majority procedure](councils-and-elections.md#council-proposals-and-election-ballots)
for the named land claim. Proposing a claim or expanding the Town border
alone grants no title.

Existing titles, household use rights and pending disputes remain intact.
Approval cannot overwrite another title or settle a dispute without the
agreed lawful hearing. A new Town title does not transfer private buildings,
crops, goods or household stock, or automatically grant a household use right.

**Also agreed by the owner on October 2, 2026
([#426](https://github.com/compoodment/ClankerWorld/issues/426#issuecomment-5956742184)):**

- **Initial household use rights cover building footprints only.** Each
  household receives the footprints of the buildings assigned to it, with no
  surrounding empty land included. The shared Warehouse remains Town property.
- **Expansion onto extra tiles requires a household land request.** The
  legitimate current Council approves it through the ordinary
  [proposal and majority procedure](councils-and-elections.md#council-proposals-and-election-ballots).
  The all-adult Council in a small Town uses that same majority rule.
- **Legitimate building reassignment moves its footprint use right with the
  building.** Record the receiving household's footprint right as part of the
  reassignment, preserving the ownership and dispute safeguards. Town title
  remains unchanged.

The October 2 owner answers on
[#426](https://github.com/compoodment/ClankerWorld/issues/426#issuecomment-5956742184)
settle title growth through Council-approved claims to adjoining unclaimed
land. Claims use ordinary Council proposals: an adult names
one connected plot, councillors learn its notice and vote, and the server
rechecks the plot before recording title. Existing claims, household rights,
structures and private goods are preserved. A border change alone still gives
no title. Household requests for expansion onto additional tiles open
ordinary Council proposals for nonconflicting land, and every current adult in
the beneficiary household must separately accept before a right is granted.
Expansion checks the resulting rights at construction start and completion.
New fields cannot occupy land another household holds or has requested.
Ordinary land requests cannot cover another household's field, and grant
completion checks that the field is still clear of that conflict. A challenge
to a recorded use right still follows the dispute procedure.
Pending disputes supply no grant; hearings and transfers follow the case
procedure below.

Competing requests are visible as **pending disputes**. While one is pending,
conflicting formal rights transfers pause; residents are not evicted, goods
are not seized and ordinary physical movement is not stopped by the dispute
record. Resolution needs an adjudicator authorized by Town law, with the
authority, evidence and exact change recorded. With no such law or adjudicator,
the dispute remains pending; there is no automatic first-claimer winner.
Later October 1 owner answers on
[#426](https://github.com/compoodment/ClankerWorld/issues/426#issuecomment-5924016895)
settle the following land rules: a plot is a connected group of tiles and may
include empty land; ordinary household permission continues unless disputed,
with an optional agreed end date; households may transfer permission by
agreement without mayor approval for every transfer. The elected mayor makes
the final decision on disputes and what happens when an agreed period ends.
These rules do not imply automatic confiscation or unilateral household
transfers. Evidence, hearings, outcomes and conflicts of interest follow the
agreed [case procedure](laws-and-enforcement.md#land-hearings-and-rulings). Disputed land is striped
on the map, and inspecting a tile lists each household's claim. See
[what works today](../../what-works.md) for current availability.

**Neighbouring Towns, agreed on October 8
([#1240](https://github.com/compoodment/ClankerWorld/issues/1240)):** Town
titles never overlap. The first valid title to a tile wins: a claim cannot
include land another Town holds, and a Town's border stops at another Town's
title. Each Town's territorial laws therefore apply only on its own title.
Later design adds land transfers or treaties that both Towns' Councils
approve. How a finding reaches another Town is in [Nonviolent law
enforcement](laws-and-enforcement.md#nonviolent-law-enforcement).

### How Roads and bridges appear

The baseline has **one Road type**, with no extra categories required yet.
Roads appear immediately with Town buildings and automatically link Towns
**when a legal land route exists**. If geography blocks a legal route, the
Towns remain unconnected by Road; boats or later transport may still connect
their travelers. Roads are not generated by footsteps or a separate building
project. Agents and player do not paint Road tiles. Roads speed up travel.
Roads connect to adjacent building entrances and must not occupy a building's
footprint. New Road tiles and new bridge entrances also avoid land any
household holds or has requested. Existing Roads and bridges remain usable,
including where a later land claim overlaps them. The player's September 29
sketch indicates a connected spine with
short branches as a useful layout direction, not a mandatory fixed street map.

**Agreed after the September 30 road review:**

- **Street first.** A Town grows along a main road that winds with the land,
  bending in 45° steps around water, trees and slopes. Side streets branch
  off it at uneven spacing, angles and lengths. The result should look grown,
  not gridded; an evenly spaced fishbone was rejected as too uniform.
- **Diagonal Roads.** A street, or a Road towards another Town, takes the
  diagonal where the land allows instead of stepping in right angles. Agents
  walk diagonally, so a zigzag Road would be slower than the grass beside it.
  Diagonal Road steps follow the same clear-corner rule as diagonal walking.
- **Every building faces a Road.** A building has one entrance, on the side
  facing its Road, and its sprite shows the door on that side; doors are not
  limited to the bottom edge. A short doorstep path joins the door to the Road.
- **Roads run on past the last building** on them, by about three tiles, so
  there is visible free frontage for the next building (tuned in playtests).
- **Roads grow with the Town.** Each new building extends the Road network:
  its street continues past it, and when a street's free frontage runs out a
  new side street branches off. New buildings prefer free Road frontage inside
  the border. Town Roads stay inside the border, which grows with the Town.
- **Look:** packed dirt with worn, feathered edges and pebbles, drawn as Road
  pieces joined only along the Road, with diagonal pieces, rounded bends and
  ends. On sand and snow it may take a darker edge to stay visible.

**Bridge placement responds to traffic** at narrow river crossings, even
without a planned Road. A Road generation pass also builds a bridge immediately
if its legal route encounters a bridgeable river. A river is **bridgeable up to
two tiles wide**; wider water is not bridged. Roads and bridges **cost no
materials**. Either case excludes a redundant nearby bridge over the same
crossing/river, not a necessary bridge over a different nearby stream. Bridge
spacing compares the actual connected banks, with **no fixed radius**, so a
needed bridge over a separate nearby stream is never blocked. For
traffic-created bridges, the initial playtest threshold is **six completed
crossings by at least two distinct agents within two world-days** at the same
legal narrow crossing. Agents may wade rivers up to two tiles wide ([The
world](../world.md#river-crossings-and-visible-forests-and-mountains)), so traffic
can bridge two-tile crossings too. Only actual traversal counts, not route
previews, failed attempts or waiting. Keep bounded crossing evidence across
saves; the threshold can be tuned after playtesting. Generated-Road bridges
need not wait for this traffic. [What works
today](../../what-works.md#maps-weather-and-appearance) says what normal play
shows so far, and [How it
works](../../development/how-it-works.md#roads-and-bridges) says how the same
connected banks are compared.

**Roads and bridges remain permanently** once built. They do not decay or
disappear automatically when traffic stops, a building is removed or a Town is
abandoned. There is one Road type; no temporary-versus-permanent class is
needed. The inter-Town timing, layout and rendering are agreed below.
Diagonal movement must not pass through blocked corners. Playable foot movement
uses the strict two-clear-shoulder rule and a 141% diagonal route cost. Town
streets also take diagonal steps where the land allows, and their Road pieces
join diagonally on the map.

**Agreed on October 8 ([#1239](https://github.com/compoodment/ClankerWorld/issues/1239)):**
a Road to another Town appears as soon as a new Town's first building is
complete. It links the new Town to the nearest Town only, not to every Town,
along the cheapest legal land route, reusing existing Roads where it can. It
looks like a Town street. This settles the inter-Town route timing, layout and look.

### Still to decide

Further structure effects; exact configurations; changes to claim boundaries;
and business economics.
