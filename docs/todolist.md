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
- [ ] Delete `Produce.ReportPlacementOnce()` once the 2.1 build is tested. [[testing#Debug aids|testing → Debug aids]]
- [ ] Collapse `AnnounceStock`'s three broadcasts to one, after confirming. [[networking#Late-join sync|networking → Late-join sync]]
- [ ] Docstrings that credit `GardenLease` with the departure clock and say 120 s. [[plot-ownership#Departures|plot-ownership → Departures]]
- [ ] Rename `EconomyManager.OnPlayerBalanceChanged` to `PlayerBalanceChanged`. [[conventions#Code|conventions → Code]]
- [ ] Re-check [[shop]]'s purchase flow names against `BuyPoint`/`DispensingEntity`.
- [ ] Scripts still say "plot" for PlantSlot. [[terminology]]
- [ ] 13 materials converted to URP by hand live in untracked asset packages; a fresh clone gets magenta. [[packaging]]

## Crops

- [ ] Growth pivots: a `ScalePivot` per crop prefab. [[crops#Growth pivots|crops → Growth pivots]]
- [ ] An ending for `MultiHarvestPlant` before a crop uses it (#43). [[crops]]
- [ ] Seed ideas: GitHub issues #44, #47. A `PlantableEntity` (planting a sword to grow an auto-harvester) is an idea, not a plan.

## Parked

- [ ] Procgen islands, blocked on flight's speed. [[procgen-islands]], [[flight]]
