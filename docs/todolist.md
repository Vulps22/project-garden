---
aliases:
  - "To do"
tags:
  - tracking
---
# To do

One line per piece of work, pointing to the doc that holds everything about it. Notes and design live in the topic
docs, never here; an item is removed once it is done. Milestones are in [[roadmap]].

## Fusion 2.1 migration

- [ ] Restart the Editor and confirm its own compile is clean (ghost `economy/` paths). [[tooling#Compile-checking from the CLI|tooling]]
- [ ] Decide `NetworkTransform` vs `KinematicController` as writers of `isKinematic`. [[networking#Fusion 2.1|networking → Fusion 2.1]]
- [ ] *Detach From Parent* on the `Produce_*` prefabs. [[networking#Fusion 2.1|networking → Fusion 2.1]]
- [ ] *Disable Shared Mode Interpolation* on kinematic-grab `NetworkTransform`s. [[networking#Fusion 2.1|networking → Fusion 2.1]]
- [ ] Move `WorldBridge.Place`'s teleport into the network tick. [[networking#Fusion 2.1|networking → Fusion 2.1]]
- [ ] Re-run the assembly-name availability check; accept the SDK update if offered. [[community-modules#Migration to 4.0 and Fusion 2.1|community-modules]]
- [ ] Missing script in CM's `Networked Script Enable Object Example.prefab`: Vulps's call. [[community-modules]]
- [ ] Upload and test with two clients: the 2.1 build, which also tests the `NetworkMessenger` framing. [[networking#Messaging|networking → Messaging]]

## Before any non-test upload

- [ ] `EconomyManager._startingBalance` is 10. [[economy]]
- [ ] `TutorialManager._rememberProgress` is on (it logs a WARN at startup when off). [[tutorial]]

## Cleanup

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
