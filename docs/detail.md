---
aliases:
  - "Detail"
  - "LOD"
tags:
  - tech
---
# Level of detail

How much of the world each player draws, by their distance, so a world of 14 Gardens and a field of islands costs
what the player can actually see. Everything here is local: each client decides for itself, and nothing is sent.

## Gardens

**Status (2026-11-09):** tested in-world. Designed with Vulps 2026-11-03.

**Drawing is Unity's `LODGroup`**, one on the `GardenIsland` prefab, switched by the engine during culling with no
script running:

| LOD | Out to | Drawn |
|---|---|---|
| 0 | 300 m (`GardenDetail._nearDistance`) | all 317 renderers |
| 1 | the player's draw distance | the 16 renderers 6 m or bigger: the island's grass and rocks, the hut's log house and windows |
| culled | beyond | nothing |

- Particles and the dome are LOD 0 only, so the dome's outside shows within 300 m and not further.
- **The distances are metres, kept true.** Unity switches on a share of the screen, scaled by its LOD bias (1.5 here);
  `GardenDetail.SetDrawDistance` works the shares out from the camera's field of view and the bias. `DetailManager`
  sets them when the draw distance changes (`GameSettings.Changed`) and once the camera is known. Unity's global
  `lodBias` is left alone: Somnium may rely on it.

**The bubble is gameplay, and an event.** `DetailManager` (on SceneManager) works out, whenever the player moves 5 m
(`PlayerTracker`), whether they are inside each Garden's dome's sphere (45 m; 10 m further out to leave, so the edge
does not flicker), and `GardenDetail.SetBubble` acts on a change and raises `BubbleChanged`:

- **Inside:** the cut-up hut (`Pieces_*`) on its light-show materials, the board's and tarp's show materials, the
  fences' `Fence_Eq`, the lights, the fire and smoke, the screen.
- **Outside:** the original `Log house.fbx` meshes on `Hut_*_Plain` copies (`_SHOW_PIECES` off), `_Plain` board and
  tarp materials, no `Fence_Eq`, lights off, particles paused, the screen's renderers hidden (`forceRenderingOff`;
  Somnium builds them at runtime, so they are looked for at the change). All still `ShowLit`, so the trip melts them.
- **The trip's material swap can put a show material back** (it restores what it saw when it started), so the hut's
  meshes and materials are checked again whenever the trip swaps (`TripManager.MaterialsSwapped`).
- **The dome brings night on** while its Garden's bubble holds the player (`BubbleChanged`).
- Only meshes, materials, lights, particles and renderers change, never objects, so nothing networked is touched.
  Plants and seeds spawned on a Garden are not in its LODGroup and always draw.

## Islands

**Status (2026-11-09):** tested in-world.

`IslandField` loads islands out to the player's draw distance instead of a fixed 1.2 km
([[procgen-islands]]). Their colliders stay as they are at every distance (Vulps): static colliders cost little, and
switching them would rebuild the physics as the player flies and drop whatever rests on an island.

## Draw distance

**Status (2026-11-09):** tested in-world.

World Settings → **Draw Distance**: Somnium's own slider row, cloned from Audio's Main volume
(`TabletManager.AddSettingsSlider`), 300 m to 2 km in 100 m steps, the arrows a step each, default 1.2 km. Stored per
player as `drawDistance` in `player_settings`, saved a second after the slider stops. It sets where Gardens' LODGroups cull and
how far islands load. Below about 800 m the other towns disappear.
