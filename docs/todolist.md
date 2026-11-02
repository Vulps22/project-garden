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

- [ ] `EconomyManager._startingBalance` is 10. [[economy]]
- [ ] `TutorialManager._rememberProgress` is on (it logs a WARN at startup when off). [[tutorial]]

## Cleanup

- [ ] Missing script in CM's `Networked Script Enable Object Example.prefab`: Vulps's call. [[community-modules]]
- [ ] Collapse `AnnounceStock`'s three broadcasts to one, after confirming. [[networking#Late-join sync|networking → Late-join sync]]
- [ ] 13 materials converted to URP by hand live in untracked asset packages; a fresh clone gets magenta. [[packaging]]

## Crops

- [ ] Growth pivots: a `ScalePivot` per crop prefab. [[crops#Growth pivots|crops → Growth pivots]]
- [ ] Roll a plant's stats (`HarvestCount`, value, size), perhaps from the stall's rarity. [[crops]]
- [ ] **Fun seeds:** popcorn (the video screen) built 2026-11-02 and working in-world; not in the shops until it is finished. Then the ARS seeds. [[fun-seeds]]
- [ ] Seed ideas: GitHub issues #44, #47. A `PlantableEntity` (planting a sword to grow an auto-harvester) is an idea, not a plan.

## Tablet

- [ ] The Almanac: design the first version, then build it. [[almanac]]
- [x] Video controls were a sliver because the player was squashed from load; fixed by squashing only the picture (2026-11-02), confirmed in-world. [[fun-seeds]]
- [x] Developer Market tried in-world and working (2026-11-02). [[dev-tools]]
- [x] AudioProbe read and removed 2026-11-02: world sounds go to Ambience in MainMixer; copying the group onto a runtime source works ([[tablet#Settings|tablet → Settings]]).
- [ ] Remove `TabletProbe` when Vulps says (it now runs only for the developer as master). The tablet is documented in [[tablet]].
- [ ] A seed restock timer beside Somnium's sidebar buttons, as a quick action under World Spawn ([[tablet#The sidebar|tablet → The sidebar]]).
  [[procgen-islands#The tablet map|procgen-islands → The tablet map]]

## Parked

- [ ] Flight sinks on some launches; cancelling the pull made it rubber-band. Waiting on the Somnium team.
  [[flight#Falling with grace, 2026-11-01|flight → Falling with grace]]

- [ ] Gardens at the island sites (phase 1b). [[procgen-islands#Phase 1b Gardens at the sites|procgen-islands → Phase 1b]]
- [ ] Procgen islands phase 2: more island prefabs. [[procgen-islands]]
