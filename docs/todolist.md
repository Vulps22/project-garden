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
- [ ] **Screens near you only:** ask Dene how Base Reality keeps far video players from playing locally, then close far and sunk screens' media. [[towns]]
- [ ] **Towns:** fly the three towns and claim, plant and sell in an outer Garden; 14 video players; popcorn screen and Moonflower dome there; music switching between screens. [[towns]]
- [ ] `EconomyManager._startingBalance` is 10. [[economy]]
- [ ] `TutorialManager._rememberProgress` is on (it logs a WARN at startup when off). [[tutorial]]

## Cleanup

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

## Parked

- [ ] Flight sinks on some launches; cancelling the pull made it rubber-band. Waiting on the Somnium team.
  [[flight#Falling with grace, 2026-11-01|flight → Falling with grace]]

- [ ] Gardens at the island sites (phase 1b). [[procgen-islands#Phase 1b Gardens at the sites|procgen-islands → Phase 1b]]
- [ ] Procgen islands phase 2: more island prefabs. [[procgen-islands]]
