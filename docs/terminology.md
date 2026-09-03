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

The scripts use "plot" to mean **PlantSlot** throughout. These should be corrected **before `Plot`
becomes a class**, or the ambiguity gets baked into new code:

- `GardenLease` → `PlotCleanup`. It was going to be `PlotLease` — wrong unit *and* wrong level, since
  it leases neither a Garden nor a PlantSlot — but `PlayerManager` has since taken the timer, so it
  holds no lease at all now. It frees Plots on an event. The rename is still pending: it touches the
  scene, and a behaviour change and a scene-touching rename should not share an upload.
- `IPlotOccupant` → `IPlantSlotOccupant`. Its occupants are a `Plant` and a `RootedProduce`, and both
  stand in a PlantSlot.
- ~45 lowercase uses of "plot" in comments and docstrings that mean **PlantSlot** — including
  CLAUDE.md's "RootedProduce earns its class by owning the plot until it is pulled".
- Turn the Garden into the `GardenIsland` prefab.

Nothing in the code says "garden" except `GardenLease`, so the rename surface is small. It is the
*comments* that carry the old meaning, and comments are what the next person reads first.
