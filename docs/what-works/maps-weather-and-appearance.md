---
title: Maps, weather and appearance
type: product-status
status: active
updated: 2026-11-14
---

# Maps, weather and appearance

Terrain, travel, weather and the game’s appearance.

## Maps, weather and appearance

Find, agent selections and building jumps ease the camera to their destination
in about 0.7 seconds. Zoom steps ease over about a third of a second and keep
the pointed-at tile under the cursor. A new request starts from the shown view;
wrapped travel takes the short route. Open quick cards stay still during the
glide. Middle dragging, keyboard panning and the World Map remain immediate.

Agents and animals glide between the server's reported tiles, with walking
frames every quarter second and a one-pixel bob. Boats and handcarts glide
without a bob. Pause freezes local motion; resume continues it. Long jumps,
world switches and earlier saves snap the drawing to the recorded position. Hit targets, names and the
selected agent's quick card follow the drawing, while the server still decides
actual travel and position. [Windows movement checks](../../playtest/alpha-1.md)
remain pending.

In autumn, a few fallen leaves lie beneath mature broadleaf and fruiting or
picked orchard trees at close and mid zoom. Their warm colours follow the
approved leaf drawing. Conifers, saplings, Roads and water have none; leaves
stay beneath trees and other sprites and disappear outside autumn. They are
a visual detail, with no harvest or effect on work.

Small/Medium worlds have generated land, rivers, lakes, separate ground and
vegetation layers, resource sites and individual trees. Foot travel supports
diagonal steps and slower mountain travel. Agents wade straight across rivers
one or two tiles wide, from bank to bank: a one-tile river at half walking
speed, and a two-tile river at a provisional third of walking speed. Eligible
agents can swim wider rivers and lakes much more slowly, losing warmth and
carrying at most four units. Cold, ill or heavily loaded agents do not start
swimming. Milk restocking, yard water, planting seeds, medicine, repair supplies,
cart materials and food for a child check the loaded trip before collection.
Unreachable milk sources are withheld too. Carts and animals stay on their existing land routes; the sea still
requires a boat. Swimming has its own motion and label on the map. Communal boats
travel between completed Ports over connected water. Swimming and two-tile
wading still need hands-on Windows playtesting.
Peaks are impassable; mountains and peaks cannot hold construction. Town
streets take diagonals where the land allows.

In newly generated worlds, sand forms deserts and stretches of ocean beach
only; rivers and lakes keep grass banks, and trees never grow on sand. Forests show small groves on darker forest floor, where every tile holds
a tree, among scattered trees on forest grass. These are the first version of
the September 29 terrain direction: their numbers are provisional until
computment reviews generated maps, and they have not been checked by hand in
the Windows game yet
([#461](https://github.com/compoodment/ClankerWorld/issues/461)).

Mountains form a few large massifs instead of many small patches: one or two
on a Small world and two to four on a Medium world. Each is a long range with a
crest of impassable peaks inside a rim of mountain that agents can walk around,
and small leftover patches are flattened. A band of hills rings each massif and
is wider around larger ones. Hills are drawn as soft foothill shading over the
ground and cost the same to walk and build on as grass. Peaks never cut off any
land, rivers start at the foot of a massif, and the starting clearing always
has stone within 32 tiles on foot. Iron, gold and diamonds are found in the massifs. The counts, sizes and
hill widths are provisional until computment reviews generated maps
([#683](https://github.com/compoodment/ClankerWorld/issues/683)). Worlds
generated before this change no longer load; they are refused and their saves
are kept.

Bridges are a basic version. Where agents often wade across the same river
crossing, one or two tiles wide (six crossings by at least two agents within
two world days), a bridge appears across it. It is drawn on the map, named on
the tile card and hover readout, and walked at dry-ground speed. When a Town grows, a new side street or a street
running on past a door crosses a river up to two tiles wide on a new bridge.
Households can plan new buildings, whose streets may need a bridge; the
starting layout keeps its streets on dry land. No bridge is added where one already joins the same river
banks and agents can walk along both banks to reach it. Peaks that block either
bank can leave room for another needed crossing. A wider stretch between narrow
crossings does not cause an extra bridge when both banks still connect to the
existing one. When a new Town's first building completes, a Road links it to
the nearest Town along a cheapest legal route, reusing existing Roads and
bridging legal narrow rivers. An already completed House incorporated while
founding triggers the same link. No legal route leaves the Town unconnected;
it does not select a farther Town. Inter-Town Roads look like Town streets
and survive save/load without enlarging either Town's land. Roads and bridges
have not been checked in hands-on Windows play.

Resources can deplete or regrow. Each tile holds at most one tree. Wood trees
have sapling, mature and stump stages. Felling one for wood also gives the agent
a tree seed. An adult carrying a tree seed may replant a stump, or plant a new
broadleaf or conifer on grass or forest ground outside the Town, and the Event
Log says so. Trees are never planted on sand, water, rock, snow, buildings or
Roads or bridge entrances; a refused planting keeps the seed. A sapling takes 3 days to grow. Trees
do not spread on their own. Tree-planting tasks support quantities, a chosen
species and an exact tile outside Town borders. Adults fetch a real permitted
seed if needed, walk to the site and count only completed saplings. See the
[task examples](../playing/orders.md#orders).

Orchard fruit trees bear fruit only in autumn. They are growing (leaves only)
the rest of the year and drop any fruit left when autumn ends. A picked tree
fruits again after 3 days while autumn lasts, and each picking gives 4 fruit.
An adult with an orchard seed can plant a new orchard tree on open grass or
forest ground outside the Town. Picking fruit also gives an orchard seed for
replanting. All tree numbers are provisional, to tune
in playtests ([#462](https://github.com/compoodment/ClankerWorld/issues/462)).
A Windows check of the tree stages at different zooms is still to do.

Weather varies by region and affects local survival and crops. Each 32×32-tile
region keeps its weather for a spell of a quarter of a day to a full day. A
storm lasts at most three-quarters of a day, and that region then gets at least
half a day without another. Rain nearby makes rain a little more likely. These
values are a prototype for playtesting ([#204](https://github.com/compoodment/ClankerWorld/issues/204)).
Recent rain gives a modest soil-moisture estimate. The map draws the approved
pixel-streak weather: straight rain with small landing bursts, stepped storm
streaks in eastward gusts, and snowflake crosses blown sideways. Cooler tints
keep the map visible; cloud haze and soft flashes remain optional, and the map
darkens gently at night. Drifting visual edges do not mean weather fronts
actually move between regions yet.

Grass and tree canopies use the approved seasonal colours: brighter in spring,
base colours in summer, warmer in autumn and desaturated in winter. Existing
snow follows the world state; changing season adds no snow by itself.

The Event Log names buildings and recipes when construction or crafting starts
or finishes, including older recorded work.

Observed snowfall now leaves white cover on open ground in that region,
keeping grass tips and trodden Roads visible. Cover builds over a game hour
and melts over two hours after snow stops. Walking agents and animals leave
footprints that fade within an hour. Pause freezes both; reload, rewind and
world changes clear this local display history. Roofs share the same cover:
gable, hipped and conical slopes keep the approved shaded-side snow and sunny
ridge-to-eave dusting. Flues, outlines, grass shadows and open yards stay clear;
roofs crossing a region edge use each side's cover. Water, trees and permanent
snow retain their own artwork. Roof appearance at close/mid zoom still needs
[the Windows check](../../playtest/alpha-1.md). Snowy walking
still needs [the Windows check](../../playtest/alpha-1.md).

Rain, storms and snow fade in or out over 1.2 seconds when regional weather
changes; pause freezes the transition. Loading a world shows its current
weather immediately.

The map uses the pixel art approved in the October 1 art review: every ground
type, with mountains, peaks and hills drawn from the world's elevation as one
landform spanning many tiles, and snow that thins into frost at its edges;
water; Roads and plank bridges, with streets running up to each bridge; trees
and natural sites; barrel, saguaro and prickly-pear cacti on about one in five
desert cactus-cover tiles; farm fields as tilled soil with each crop shown at
its stage; every current building; item icons; and the interface icons.
At close zoom, physical handcarts use the approved empty and loaded pictures
in eight directions, including the two loaded pulled poses. Medium zoom uses
their approved 16px versions
([#914](https://github.com/compoodment/ClankerWorld/issues/914)).
Agents face the way they last moved and show walking steps, and carrying,
working, talking or hurt poses chosen from what the game already knows about
them. Mountain relief is drawn nearest the middle of the view first, and for
the ring just outside it ahead of time, so panning rarely shows the earlier
per-tile mountain art; where it does, as just after a world loads, the relief
fades in over a fifth of a second. Natural sites show their approved picked,
harvested and depleted drawings from what the game already tracks about each
site. The interface uses wooden
frames and parchment panels, with pixel fonts:
Fusion Pixel for body text and Timber capitals for headings. The Main Menu shows
the ClankerWorld logo over an animated pixel-art valley with snowy mountains
that follows the Light or Dark theme. Mineral/clay sites exist; some
materials still lack a complete production chain.
