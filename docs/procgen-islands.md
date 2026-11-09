---
aliases:
  - "Procgen islands"
tags:
  - arc
  - game
---
# Procgen islands

The step doc for [[roadmap]]'s *Future — islands that generate themselves*: islands placed in the sky by the world
seed, with stalls on them. Vulps's design; the engineering notes are flagged as such.

---

## Phase 1: islands from a seed

**Status (2026-11-01):** played alone in-world: islands placed from the seed, flown between, landed on and climbed.
Code: `GrowAGarden/Islands/`.

- **`WorldSeed`** (on `SceneManager`, message id 1 on its messenger) holds the seed. The first master takes the UTC
  midnight of the moment it joins, as Unix seconds, and sends it to everyone; it sends it again whenever a player
  joins and after a master handover. A new master keeps the seed it was given. A client places nothing until it has
  one, so a player who joins after the next midnight still gets the instance's seed, not today's.
- **`IslandField`** (root object `IslandField`, at the Garden) derives the layout and sends nothing. The sky is an
  unbounded grid of cells; a hash of `(seed, cell)` decides whether a cell holds an island, which prefab from
  `_islands`, its offset inside the cell, its height within `_heightRange` (±25 m) and its yaw. Gardens sit at y=0. Each client loads every island
  that touches its own sector, plus every cell within the player's draw distance of its head ([[detail]]; `_viewRadius` without settings), and destroys the rest; they are
  plain scenery with no NetworkObject. Why the whole sector:
  [[networking#Authority by sector|networking → Authority by sector]].
  **Reordering `_islands` moves every island**, so new islands are appended.
- **Every island prefab carries `SpawnableIsland`**, and `_islands` accepts nothing else. Its `IslandCategory`
  (Outcrop, Island, Landmark) fixes how much room it takes: the field treats every island as its category's full
  box, whatever it contains. Sizes are one table in `SpawnableIsland.MaxSize` (Outcrop 35×60×35, Island 90×100×90,
  Landmark 250×180×250), the prefab draws its box in the Scene view, and `OnValidate` warns when the contents outgrow
  it.
- **Overlap is settled against the seed, not against what has spawned**, so every client agrees whatever order it
  streams cells in. Each cell proposes an island anywhere in the cell. Where two footprints (the circle a category's
  box sweeps at any yaw) meet, the lower-ranked island moves directly away, just far enough to clear; rank is a hash.
  If the move puts it into a third island, or into a Garden's clear zone, it is not placed. Islands may spill into
  neighbouring cells, which is what lets a Landmark exist on a 150 m grid.
- **Gardens are placed in the scene** ([[towns]]). No island is placed within `_gardenClearRadius` of any of them; the
  lattice of Garden sites that islands once cleared around, Garden or not, is gone (2026-11-03).
- **Defaults:** cell 150 m, density 0.9, cell margin 0 (an island may sit anywhere in its cell), garden clear 110 m.
- The hand-placed `Island_A` ring is gone; the field owns the sky.

**Phase 2:** more island prefabs, appended to `_islands`.

## The tablet map

**Status (2026-10-31):** first version built; zoomed-in islands tested in-world, sectors untested.

The tablet itself is [[tablet]]. `MapOverlay` (on `IslandField`) draws the seed's world onto Somnium's own tablet map; `TabletMapBridge` is the one
file that knows Somnium's map layout (`Tablet.XR(Clone)/…/Tablet.XR.MapTab/…/MapView/Viewport/Content`).

- **Somnium's map is UI over a static picture.** `Content` is an `Image` of the mainland (`MapCombinedRGB`); zoom
  scales `Content` itself (×0.29 to ×6.6 seen) and drag moves it. We clear the picture, hide `Landmarks`, `Parcels`
  and `MapPlayer_You`, grow `Content` to ±50 km so scrolling is not clamped, and put our layer inside it, so their
  zoom and drag carry ours. `PlayersViewContainer` (other players) is left alone; it was empty with one player.
- **Drawn in world coordinates** from the field's origin, for whatever the viewport shows, redrawn only when the
  view, the zoom or the player moves. The map centres on the player when opened.
- **Islands** use a top-down sprite baked per prefab by **Grow A Garden → Bake Island Map Sprites** (Editor-only,
  `GrowAGarden/Editor/`), stored on `SpawnableIsland` with the metres it spans; an unbaked island is a box. The
  Garden's sprite is on `MapOverlay`. Re-bake after changing an island.
- **Sectors** are 1 km squares, Sector 0 centred on Garden 0 ([[towns]]); other Gardens fall wherever their town puts
  them. When
  the view spans more than 900 m from its centre the map shows labelled sector tiles (`Sector 0`, `Sector -1,0`)
  instead of islands; region names may replace the numbers later. Labels use Somnium's own map font.
- Zoomed fully out with islands drawn, the first version drew 1,500 islands and resolved a 4 km radius four times a
  second; frame rate collapsed. That is what the sectors are for.

> [!question] Open
> - Map scale is 1 unit per metre, a guess that looked usable.
> - Somnium's "centre on me" button scrolls to its hidden dot, not ours.
> - How other players appear in `PlayersViewContainer`, which needs two clients.

## Buy points everywhere

**Status (2026-11-01):** both uploads built and played alone in-world: Garden and island stalls stock and sell, and
island stalls open by the seed with a seeded rarity. Multiplayer untested. Decided by Vulps 2026-11-01.

Every buy point behaves the same, Garden or island: the master decides its stock by stall id without having it
loaded, a player in its sector simulates it, and it sells once per cycle.

1. **This upload:** sectors as the interest table, authority by sector, sector-plus-distance loading, and the stall
   split into `BuyPoint` and `ShopKeeper`. The Garden's three stalls go through it.
   [[networking#Authority by sector|networking → Authority by sector]], [[shop]].
2. **Island stalls:** an island marks where a stall *could* stand with `StallSocket`s; the world seed picks which
   open. When `IslandField` places an island it hashes the seed and the cell, opens anywhere from none to all of
   its sockets, and puts `IslandField._buyPoint` (the Garden's `BuyPoint` prefab,
   no override, so it rolls) on each. Every client picks the same sockets with nothing sent. A stall's id is its
   socket's place among the island's sockets in hierarchy order, so it does not depend on which others opened;
   reordering sockets renames stalls, moving them does not.
   Each opened stall also gets a rarity from 1 to 100 from the same hash, set as its `_rarityMultiplier`: it
   gates nothing, but a high one makes rare crops far likelier ([[shop]]). `Island_A` has 2 sockets; `Island_B` has 3 (two on the
   plateau, one on the lower shelf).

## The design

**Status (2026-11-04):** Vulps's design; built as Phase 1 and *Buy points everywhere*, except where noted.

**Islands are premade, not generated.** A palette of authored island prefabs, placed by the world
seed. The generator picks *which* island goes *where*, not what an island is made of. In the finished
game the definitions may live on a server, authored either at `IslandCreator.vulps.co.uk` or — more
likely, and better — in a Somnium world built for the purpose (not built). **For now everything stays local
Unity prefabs.**

- **Players spawn at Garden 0** ([[towns]]).
- **The seed is one number** for the instance, the UTC midnight of the moment the first master joins (*Phase 1*).
- **The seed places islands on an unbounded grid of cells**, loaded as players come into range (*Phase 1*).
- **Every island has stall sockets**, and the seed opens anywhere from none to all of them (*Buy points
  everywhere*): `Island_A` has 2, `Island_B` 3.
- **The loop:** fly out, find a stall, decide whether you want what it is offering or whether to keep exploring,
  collect one or two seeds, and come home.
- **Anything that falls out of the world is gone**: the world floor destroys dropped grabbables and returns a falling
  player to the nearest Garden (`WorldFloor`, [[towns]] → *What is one per Garden*).
- **Every player sees the same islands, in the same places, with the same stalls, selling the same items.**
- **Each island stall has a rarity from 1 to 100**, hashed from the seed and the cell and skewed low, which weighs its
  roll toward rare crops ([[shop#What a stall sells|shop → What a stall sells]]). It does not depend on distance.
- **Nothing is ever planted out there.** Gardens are the only places a player can plant or harvest. Exploration
  islands are places you *find* things, not places you farm.
- **Flight** is ours, hand-driven: [[flight]].

---

## Engineering notes

**Status (2026-11-04):** the derivation is built; the radius is analysis, not decided.

### The map is a derivation, and nothing about it goes on the wire

Seed plus coordinate gives island prefab, position, and stall sockets, identically on every client.
"Generating as they come into range" is *instantiation*, not decision. There is no island manifest to
sync, no late-join path, and no repair channel. This is `GetGrowthCompletion()` at world scale and it
is the property to protect above every other — the moment any part of the layout has to be
*announced*, the design stops being free. What the master does decide is each stall's stock, by stall id
([[shop]]).

### ⚠ Radius is bounded twice, and the tighter bound is not the one you expect

**By float precision.** Unity is float32; positional precision degrades as `distance / 2²³`.

| distance from origin | precision |
|---|---|
| 1 km | 0.12 mm |
| 10 km | 1.2 mm |
| 50 km | 6 mm |
| 100 km | 12 mm |

In VR, held objects and hand tracking begin to visibly jitter in the 1–10 km band, and physics solves
degrade before rendering does. Without a floating-origin system — which cannot work here anyway,
since every client would need a different offset while everything networked lives in world space —
**~5 km is a realistic outer bound.**

**By cycle time, which binds much tighter.** `WorldManager._recycleSeconds` is 300. The furthest
*useful* stall is the one you can reach and return from inside one cycle, because anything beyond
that has re-rolled before you arrive. Effective radius is `flightSpeed × 150 s`:

| flight speed | effective radius |
|---|---|
| 10 m/s | 1.5 km |
| 20 m/s | 3 km |
| 50 m/s | 7.5 km |

**So flight speed and cycle length are the two knobs, and the radius falls out of them.** 2 km is a reasonable
starting size, with density tuned to make the trip feel right. If a genuinely larger world is wanted, island stalls
should cycle on a slower clock than the Gardens'. They serve different purposes and there is no reason they share a
heartbeat.

---

## Open questions

**Status (2026-11-04):** open.

1. **Cycle length for island stalls** — shared with the Gardens', or its own slower clock.
2. **Radius and island spacing**, which fall out of flight speed and 1.
