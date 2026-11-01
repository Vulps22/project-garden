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
- [ ] Turn the Garden into the `GardenIsland` prefab. [[terminology]]

## Crops

- [ ] Growth pivots: a `ScalePivot` per crop prefab. [[crops#Growth pivots|crops → Growth pivots]]
- [ ] An ending for `MultiHarvestPlant` before a crop uses it (#43). [[crops]]
- [ ] **Fun seeds, next after the island sockets:** weed ([[weed]]), seeds that use the ARS system, and a seed that spawns a video screen for playing videos. Design for the last two to come from Vulps.
- [ ] Seed ideas: GitHub issues #44, #47. A `PlantableEntity` (planting a sword to grow an auto-harvester) is an idea, not a plan.

## Procgen

- [ ] Buy-point sockets on the islands (upload 2). [[procgen-islands#Buy points everywhere|procgen-islands → Buy points everywhere]]

## Parked

- [ ] Gardens at the island sites (phase 1b). [[procgen-islands#Phase 1b Gardens at the sites|procgen-islands → Phase 1b]]
- [ ] Procgen islands phase 2: more island prefabs. [[procgen-islands]]
