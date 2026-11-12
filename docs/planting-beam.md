---
aliases:
  - "Planting beam"
tags:
  - interaction
  - mechanic
---
# Planting beam

A beam down from a held seed to the plant slot it will go into. Vulps's design, 2026-11-11.

**Status (2026-11-11):** built to Vulps's design; compiles clean, untested in-world.

## Why

A VR player whose play-space floor is not level with the world's floor finds it very hard to plant: the seed has
to reach the slot's trigger, and the floor they feel is not the floor they see.

## How it works

- **Over a dirt block, a beam:** while a held seed is above a dirt block, a beam comes down from the middle of the
  seed and locks onto the nearest plant slot. Letting go of the seed plants it in that slot.
- **Not over dirt, no beam:** the path down the middle of the Plot does not trigger it, and letting go there is an
  ordinary drop.
- **2D players:** the slot is the one they are looking at, and F plants the selected seed there
  ([[2d-players#Interacting]]).

The plant goes through the same `PlantSlot.RequestPlant` as today ([[crops]]). Touching a slot with a held seed
still plants it too.

## How it is built

- **`Interaction/PlantingBeam`**, one in the scene, local only: it hears `Seed.LocalGrabbed` and `LocalReleased`
  (this client's own grabs), and each frame casts down from the middle of every seed this player holds.
- **What is under it:** the first fixed collider below, skipping anything with a Rigidbody (seeds, players) and
  plants and produce. If it is a dirt block (`Dirt.prefab`, whose own children are its four `Plant_Slot`s), the beam
  locks onto its free slot nearest the seed across the ground; anything else, the path included, is not dirt.
  A slot is free if this client has heard of nothing standing in it (`PlantSlot.IsTaken`).
- **The beam** is a `LineRenderer` from the seed's middle to the slot, with `Materials/Desktop/PlantingBeam` (URP
  Unlit, light blue, see-through).
- **2D:** the 2D controls mark the slot looked at (`PlantingBeam.Mark`), and the beam runs from the selected seed
  above the head to it.
