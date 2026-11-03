---
aliases:
  - "To do"
tags:
  - tracking
---
# To do

One line per piece of work, pointing to the doc that holds everything about it. Notes and design live in the topic
docs, never here; an item is removed once it is done. Milestones are in [[roadmap]].

## Before any non-test upload

- [ ] **Tutorial rework, soon:** correct it for 14 Gardens (it assumes the one at Garden 0); learn from SpaceScape's tutorial. [[tutorial]] [[towns]]
- [ ] **Remove `ScreenStandIn`** once screens near you only works: it hides every screen but world spawn's behind a notice. [[popcorn]]
- [ ] **Screens near you only:** ask Dene how Base Reality keeps far video players from playing locally, then close far and sunk screens' media. [[towns]]
- [ ] **Towns:** fly the three towns and claim, plant and sell in an outer Garden; 14 video players; popcorn screen and Moonflower dome there; music switching between screens. [[towns]]
- [ ] `EconomyManager._startingBalance` is 10. [[economy]]
- [ ] `TutorialManager._rememberProgress` is on (it logs a WARN at startup when off). [[tutorial]]

## Cleanup
- [ ] **Event driven, phase 1 (built, untested):** `PlayerTracker` samples position once and raises events; sectors, islands, the Gardens' bubbles (drawing is now Unity's LODGroups, rescaled by Draw Distance), the screen ARS listens to, earshot, the dome's night, the world floor, authority by sector (message id 14) and the stranded check now run on events. Test: move items between sectors with two players, fall out of the world, drop something into the void, walk in and out of a dome. [[principles]] [[networking]]
- [ ] **Event driven, phase 2:** stock and stalls (`BuyPoint` stock lookup every frame and 5 s retry, Almanac refresh). [[principles]]
- [ ] **Event driven, phase 3:** the tablet (lookups, new-tablet checks, `MapOverlay`). [[principles]]
- [ ] **Event driven, phase 4:** a second audit of the rest (flight/climb device scans, `ButtonProbe`, `TripManager` re-melt, `Seed.ReportLooseState`, `SettingsPanel`, `ScreenStandIn`, per-frame state checks without timers). [[principles]]

- [ ] Missing script in CM's `Networked Script Enable Object Example.prefab`: Vulps's call. [[community-modules]]
- [ ] Collapse `AnnounceStock`'s three broadcasts to one, after confirming. [[networking#Late-join sync|networking → Late-join sync]]
- [ ] 13 materials converted to URP by hand live in untracked asset packages; a fresh clone gets magenta. [[packaging]]

## Crops

- [ ] Growth pivots: a `ScalePivot` per crop prefab. [[crops#Growth pivots|crops → Growth pivots]]
- [ ] Roll a plant's stats (`HarvestCount`, value, size), perhaps from the stall's rarity. [[crops]]
- [ ] **Fun seeds:** popcorn, the Moonflower and the Spotlight Tulip work in-world; put each in the stalls when it is ready. [[fun-seeds]]
- [ ] Seed ideas: GitHub issues #44, #47. A `PlantableEntity` (planting a sword to grow an auto-harvester) is an idea, not a plan.

## Tablet

- [ ] The Almanac: try it in-world (silhouettes, collecting from a stall, the record, the stall count). [[almanac]]
- [ ] Developer access: grant and take it from someone in-world. [[dev-tools]]
- [x] Video controls were a sliver because the player was squashed from load; fixed by squashing only the picture (2026-11-02), confirmed in-world. [[popcorn]]
- [x] Developer Market tried in-world and working (2026-11-02). [[dev-tools]]
- [x] AudioProbe read and removed 2026-11-02: world sounds go to Ambience in MainMixer; copying the group onto a runtime source works ([[tablet#Settings|tablet → Settings]]).
- [ ] Remove `TabletProbe` when Vulps says (it now runs only for the developer as master). The tablet is documented in [[tablet]].
- [ ] Restock timer: check it sits under World Spawn without pushing the sidebar out of shape, and agrees between two players and after the master leaves. [[shop]]
- [ ] Notifier: a toast and chime when someone joins; silent within 5 s; Show Notifications off silences it. [[notifier]]
- [ ] Change instance from inside the world: do the apps, the timer and World Settings come back? Send the log either way. [[tablet]]
- [ ] Plant a seed someone else bought (handed over, or from the Developer Market) in your own Plot. [[plot-ownership]]
- [ ] Notify Everyone from Settings → World → Developer: reaches another player and yourself; the keyboard fits the panel. [[dev-tools]]
- [ ] Frame time benchmark: does a PERF line appear every minute, does the first say whether CPU/GPU timing is available, and record a day and a night baseline in the same spot. [[testing]]
- [ ] Grow Wand: touch a seedling's soil, a grown bearing plant, a carrot in the ground; does a second player see it grow? [[dev-tools]]
- [ ] Level of detail: walk out of a Garden's dome (solid hut, no fire), past 300 m (small things gone), past the draw distance; drag Draw Distance and watch islands and towns come and go; does the pinned core ease? [[detail]]

## Changelog

- [ ] A changelog on the hut: it fetches the repo's `latest` GitHub release and shows that release's description. Not designed yet.

## Parked

- [ ] Flight sinks on some launches; cancelling the pull made it rubber-band. Waiting on the Somnium team.
  [[flight#Falling with grace, 2026-11-01|flight → Falling with grace]]

- [ ] Gardens at the island sites (phase 1b). [[procgen-islands#Phase 1b Gardens at the sites|procgen-islands → Phase 1b]]
- [ ] Procgen islands phase 2: more island prefabs. [[procgen-islands]]
