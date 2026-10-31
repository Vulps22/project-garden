---
aliases:
  - "Terminology"
tags:
  - reference
  - rules
---
# Terminology

Locked 2026-09-03. These words are load-bearing. The prefabs already agree with them; the scripts do
not yet.

| Term | What it is |
|---|---|
| **World** | The scene. Everything, including the void and whatever floats in it. |
| **Island** | A floating chunk of map. One is home; the rest are out there to be found. |
| **Garden** | The constructed farming area on the home island — public ground, four Plots, a SellPoint, three BuyPoints, and in future a shed. Becomes a prefab, `GardenIsland`. |
| **Plot** | The patch of Garden a player can claim. Four of them, 24 PlantSlots each. |
| **PlantSlot** | Where a seed goes and a plant grows. |

**Garden is a purpose, not a container**, and that is the whole point of the split. The word had been
doing two jobs — the scene *and* the farm — so an exploration island risked being called "a garden"
simply by being somewhere you stand. It cannot now: Garden names what a place is *for*, and only one
place is for that.

## What the prefabs already say

```
World (the scene)
└── GardenIsland                        (to be made a prefab)
    └── BorderedPlotWithRoad  ×4        = BorderedPlot + grass/path
        └── BorderedPlot     ×1         = Plot + border
            └── Plot         ×1         ← the claimable unit
                └── Dirt     ×6
                    └── Plant_Slot ×4   → 24 PlantSlots per Plot
```

Verified against the prefabs and the scene on 2026-09-03: six `Dirt` per `Plot`, four `Plant_Slot`
per `Dirt`, four `BorderedPlotWithRoad` instances in `GrowAGardenScene`.

## What the code still calls things

**Status (2026-10-31):** done, except the Garden prefab.

- `GardenLease` is now `PlotCleanup`: `PlayerManager` owns the departure timer, so it holds no lease. It frees Plots
  on an event.
- `IPlotOccupant` is now `IPlantSlotOccupant`. Its occupants are a `Plant` and a `RootedProduce`, and both stand in a
  PlantSlot.
- Comments that said "plot" for a single square of dirt now say PlantSlot. A variable named `plot` holds a
  `PlotStateManager` or `PlotLeaseManager`, which is a Plot.
- Still open: turn the Garden into the `GardenIsland` prefab ([[todolist]]).
