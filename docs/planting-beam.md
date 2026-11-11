---
aliases:
  - "Planting beam"
tags:
  - interaction
  - mechanic
---
# Planting beam

A beam down from a held seed to the plant slot it will go into. Vulps's design, 2026-11-11.

**Status (2026-11-11):** decided (Vulps), not built.

## Why

A VR player whose play-space floor is not level with the world's floor finds it very hard to plant: the seed has
to reach the slot's trigger, and the floor they feel is not the floor they see.

## How it works

- **Over a dirt block, a beam:** while a held seed is above a dirt block, a beam comes down from the middle of the
  seed and locks onto the nearest plant slot. Letting go of the seed plants it in that slot.
- **Not over dirt, no beam:** the path down the middle of the Plot does not trigger it, and letting go there is an
  ordinary drop.
- **2D players:** the slot is the one they are looking at, and K plants the selected seed there
  ([[2d-players#Interacting]]).

The plant goes through the same `PlantSlot.RequestPlant` as today ([[crops]]).
