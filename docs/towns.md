---
aliases:
  - "Towns"
tags:
  - game
  - mechanic
---
# Towns

Four Plots in a game for ten or more players at once left most of them nothing to do. The world now holds
**14 Gardens, 56 Plots**, in three towns. Decided by Vulps 2026-11-03, replacing [[procgen-islands#Phase 1b: Gardens at the sites|Phase 1b]]
(Gardens appearing as players explore).

## The design

**Status (2026-11-03):** decided by Vulps; built, compiles clean, untested in-world.

- **Gardens are placed in the scene, not spawned, and not rolled by the seed.** Players will not fly huge distances,
  so the world needs Gardens near where they are, and it needs them laid out on purpose. Procgen drives only the
  islands you explore.
- **A neighbourhood feel** (Vulps): Gardens cluster into **towns**, with open sky and islands between towns.
- **Every Garden is identical** (Vulps): its own deed hut, screen and dome, besides its four Plots, sell point, three
  stalls and scoreboard. A town is only a layout.
- **40–60 Plots is plenty** for the foreseeable future. Plots only have to cover the players in one instance at once:
  a Plot is freed 180 s after its owner leaves, and worlds do not persist across instances.

- **Not a lattice, not rolled.** Islands clear only around Gardens that exist; a lattice of possible Garden sites
  kept most of the sky empty for Gardens that were never there.

## The layout

**Status (2026-11-03):** built, untested in-world. Every position is a starting point, to be moved after a fly-around.

| Town | Where | Gardens | Plots |
|---|---|---|---|
| `Town_Capital` | around Garden 0, where everyone arrives (2.1, 0.5, 7.28) | 6: Garden 0 and five on a ring 120 m out (E, NE, NW, W, SW) | 24 |
| `Town_East` | 700 m east, 300 m north of Garden 0 | 4: one and three on the ring (E, NE, NW) | 16 |
| `Town_West` | 650 m west, 450 m south | 4, as East | 16 |

- **120 m between Gardens in a town**: a dome's radius is 45 m, so two domes never touch (that needs 90 m), and a
  neighbour is a short hop.
- **Towns are 700–800 m apart**, inside the default 1.2 km draw distance, so every town is in sight of the next and
  islands fill the sky between them ([[detail]]).
- **A town is a plain group** (`Town_*` in the scene) with no component: nothing in the game asks which town it is in.

## How it is built

**Status (2026-11-03):** built, compiles clean, untested in-world.

```
Town_Capital
├── GardenIsland          GardenIsland.prefab: Garden; 4 Plots, SellPoint, 3 BuyPoints, scoreboard, Dome (ShowStage)
│   └── GardenCentre      GardenCentre.prefab: DeedHut, the screen, GardenReturnPoint
├── GardenIsland (1)
└── …
```

- **`Garden`** (`Islands/`, on the `GardenIsland` prefab's root) marks a Garden and gives its Plots and its return
  point. `Garden.All` is every Garden in the scene, the same on every client, so nothing is sent;
  `Garden.Nearest(point)` is the Garden nearest a point.
- **`GardenCentre.prefab`** (`Prefabs_world/`), inside `GardenIsland`, is the Garden's hut, screen and return point: the
  `DeedHut` prefab, Community Modules' `ARS Somnium Video Player` with our `ScreenReveal` and `ScreenManager`, and
  `GardenReturnPoint`. Its screen's settings are in [[popcorn#The screen|popcorn → The screen]].
- **Islands keep clear of real Gardens only.** `IslandField` clears `_gardenClearRadius` (110 m) around every `Garden`
  in the scene. The map marks every Garden.

### What is one per Garden

- **The hut covers its own Garden's four Plots.** `PlotFireToggle`, `PlotTeammateRoster` and `PlotClaimSignage` take
  `Garden.PlotsFor(this)` when their `_plots` is empty (now always). Claiming was never tied to a Plot list:
  `DeedClaimZone` takes any deed into any hut, and the deed belongs to its Plot, so a deed carried into another
  Garden's hut still claims its own Plot.
- **The screen**: popcorn planted in a Garden builds that Garden's screen ([[popcorn]]).
- **The music**: each client listens to the screen nearest it, and the grooves dance within 75 m of it
  ([[light-show]]).
- **The dome**: a Moonflower raises its own Garden's dome, and a Spotlight Tulip pans about it ([[moonflower]],
  [[spotlight-tulip]]).
- **Falling out of the world** puts you back at the return point of the Garden nearest where you fell (`WorldFloor`,
  told by `PlayerTracker` when your head crosses the floor). Grabbables that fall are caught by a trigger volume under
  the floor (100 km wide, 1 km deep, on the Ignore Raycast layer) and the master destroys them.
- **Stall ids** name a Garden's stalls by where the Garden stands ([[shop#Stall ids|shop → Stall ids]]).

### What stays one

- **The scoreboard's top ten.** The economy still sends the list on one board's bridge (Garden 0's), and every board
  shows what arrives (`BalanceDisplayManager.Shown`).
- **The spawn and the tutorial** are Garden 0's: everyone arrives there.

> [!info] Networking
> Nothing new is sent. Every Garden, Plot, sell point and scoreboard is a scene object, registered by `SceneNetworking`
> at load like the first Garden's: 85 scene `NetworkObject`s, 6 per Garden, all with distinct sort keys. Which
> Garden, screen or dome a thing belongs to is derived from where it stands, the same on every client.

> [!warning] Untested
> Everything here compiles and was checked in the Editor, not in-world. Worth watching: **14 Somnium video players in
> one world**, a first (each also starts with no link); 14 Gardens' Plots and sell points with real players; claiming
> at an outer hut; popcorn and a Moonflower dome in an outer Garden; the music following a player between Gardens;
> the stalls stocking in all 14.

## Not built

- **Screens playing only near you.** Somnium syncs a screen's link to every client, so a video playing in another town
  still decodes on yours, and a screen that sinks when its popcorn goes keeps playing underneath. Closing media on far
  or sunk screens locally is the plan, but Pause and Stop are synced and not to be touched: Vulps is asking Dene how
  Base Reality does it (2026-11-03).

- **Spawning at another Garden.** Everyone arrives at Garden 0; choosing where to arrive may follow.
