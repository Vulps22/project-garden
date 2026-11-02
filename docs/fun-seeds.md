---
aliases:
  - "Fun seeds"
tags:
  - crop
---
# Fun seeds

Seeds whose plant is an upgrade to the Garden rather than a crop to sell: the popcorn seed, which brings up the video
screen, and later the ARS seeds, whose lights and effect spheres react to the screen's audio. Weed came first and
is its own doc ([[weed]]).

## The video player

**Status (2026-11-02):** placed, untested in-world.

**Somnium's video player cannot be spawned.** `SomniumVideoPlayer` is one of Somnium's built-in components: the client
attaches its real implementation (AVPro) when the world loads, and numbers these components by hierarchy order for
syncing, so one must be in the scene from load and never created, deleted or reordered (`WorldCreatorContext.md` →
Built-in runtime components). Somnium's climbable surface failing on spawned islands was the same rule ([[climbing]]).

So the Garden has one player that always exists, and the seed **reveals** it. It is Community Modules'
`ARS Somnium Video Player` in the `GardenIsland` prefab, positioned by Vulps at the Garden's east edge facing the
plots, (10.4, 2.9, 5.0). Autoplay is off; it shipped looping a YouTube link for everyone on load.

- **Who sets the link is Somnium's own rule** (Vulps, 2026-11-02): the native controls let anyone take or release
  control, and we leave that alone.
- **One Garden, one player** for now; several Gardens are a later problem.

## The popcorn seed

**Status (2026-11-02):** first version played in-world (the seed was in the market and stalls; the screen faced away
from the plots). Reworked the same day as below: compiles clean, untested in-world.

Designed by Vulps 2026-11-02, revised the same day:

- **The seed is a kernel**, the unpopped bit (`Seed_Popcorn`).
- **The plant is a real piece of popcorn** growing out of the ground, about 30% buried like a carrot, swelling to
  50 cm while popcorn sprays out of its top, livelier as it grows.
- **The popcorn builds the screen.** When a grown plant takes the screen, its spray stops and a stream of popcorn
  flies from it to the screen, each piece joining the screen's building edge, which rises from the bottom as they
  arrive: nanobots, or programmable matter (Vulps). This replaced a rain of popcorn over the Garden.
- **Permanent for the session.** No lifespan: it holds its PlantSlot until the Plot is cleaned up after its owner
  leaves.
- **A second one may be planted.** It grows and keeps spraying while it waits; only the oldest grown popcorn plant
  holds the screen. When that one goes, the next takes over and its popcorn rebuilds the screen from nothing.

**Pieces:**

- **`StandingPlant`** (`Crops/`): a plant that grows and then stands, bearing nothing and never withering. The fourth
  plant shape, for upgrades ([[crops]]).
- **`PopcornSeed`**: buy 120, nothing to sell, grows in 60 s, `spawnWeight` 0.12. **Not in the shops yet** (Vulps,
  2026-11-02): only in the Developer Market until it is ready; `Buyable_Popcorn` stays under SceneManager, out of
  `WorldManager._buyables`, so listing it is adding it back.
- **`ScreenManager`** (on SceneManager, `Video/`): which grown popcorn plant holds the screen, the oldest by planting
  time (`Holder`). Raises `Assembling(plant)` when a plant takes it in view, and `VisibilityChanged` when it is simply
  up (a late joiner) or gone. Keeps the screen's building edge for the popcorn to aim at; a build takes
  `_buildSeconds` (6 s).
- **`ScreenReveal`** (on the player): **never touches the player's own transform.** It squashes only Somnium's picture
  and its backing (`FrameParent`, `BackQuad`, found once Somnium has attached them), pinned at the bottom edge, and
  switches the control canvases off until the screen is whole. Building, the edge rises linearly over the build time
  and is reported to `ScreenManager` each frame; a takeover rebuilds from nothing. With no holder it sinks over 3 s.
  The player stays active, as Somnium requires.
  - **Why not the player's transform** (2026-11-02): the first version squashed the whole player to 0.1% height from
    load. Somnium sizes its controls from the player around then, so they came out a sliver (canvas scale 0 × 0, bar
    0 wide: Vulps's screenshot, and the first probe) and never recovered, though they still took the laser.
- **`PopcornPlant`** (on `Plant_Popcorn`): the `Spray` particle system (gravity, bouncing, shrinking away), off while
  this plant holds the screen; and the `Stream`, no gravity, emitting for the build time less the flight, each
  particle steered in `LateUpdate` to its own spot along the building edge (its random seed picks the spot), with a
  sideways wander so it reads as a cloud, and removed on arrival.
- **The plant's body** is `Popcorn` (scaled by growth) holding `Mesh` 0.15 units up, so about 30% is buried at every
  size.
- **Models:** the kernel is built in the Editor, like the splif: `Models/Popcorn/Kernel.asset` (a lathed teardrop,
  1 unit tall, pivot at its base; its profile clamps sin at the tip, which once came out negative and made every
  vertex NaN). The **popped piece is made in Blender** (2026-11-02) by `Models/Popcorn/Popcorn.blender.py`: metaballs
  for the puffs (a core, nine lobes burst outward and up, some with a smaller puff), voxel-remeshed, a knobbly clouds
  displacement and finer Voronoi wrinkles, and a golden hull fragment at the base; scaled to 1 unit and exported
  twice, `Popcorn.fbx` (3000 triangles, the plant) and `Popcorn_Small.fbx` (308, every particle, since the stream can
  hold hundreds at once; puffs only, in one material, because a particle mesh draws its first sub-mesh only and Unity
  warns otherwise). Its two materials, `Popcorn` and `Popcorn_Hull`, are remapped on import to our `Popcorn`
  (cream) and `Popcorn_Kernel` (golden), each with a trip twin. Rerun:
  `blender --background --factory-startup --python Popcorn.blender.py -- <out_dir> <preview_dir>`, which also renders
  a preview.
- **The screen faces the plots: rotation 90.** Somnium's screen is a Unity quad and its controls a world-space canvas,
  and both face the player object's **−Z**: the picture shows, and the laser meets the controls, only from that side;
  from behind you see the black `BackQuad`. At 270 (as first placed, and again after a double flip on 2026-11-02) −Z
  points away from the plots. Turned to 90, checked: −Z points at the plots.

> [!info] Networking
> Nothing new is sent. The plant is planted, grown and uprooted like any crop, its growth derived from one timestamp.
> Every client sees the same plants grow on the same clock, so each works out the screen's holder, the build and the
> stream for itself. A late joiner who arrives after a plant grew gets the screen already up; a takeover, or a plant
> grown in view, builds it. Playback is synced by Somnium's player.

**Not a real dissolve, yet.** The reveal squashes the transform from the bottom edge. A probe (2026-11-02, since
removed) showed what Somnium builds under the player: `BridgeVideoPlayer(Clone)` → `VideoPlayer`
(`AVProPlayerController`), `BackQuad` (Simple Lit), `FrameParent/Frame` (a **unit quad at the player's origin**, AVPro's
`Unlit/Opaque` material driven by AVPro's `ApplyToMesh`, with a `BoxCollider`) and `Controls`, a world-space canvas
of Play/Pause, Reload, Live, Mute, Volume, Change link, Release control and a Link field. So the squash's assumption
holds. A true dissolve looks possible: give `Frame` a material of ours with a dissolve and the same `_MainTex`, and
`ApplyToMesh` should keep feeding it the video. Untried.

## ARS seeds

**Status (2026-11-02):** planned.

Spotlights, and effect spheres: micro-skyboxes forming around the island, driven by ARS. ARS behaviours are plain
Community Modules scripts, so they can be grown on a plant. `ARSController` is a single instance listening to one
`AudioSource` for the whole world, so every ARS effect reacts to the same sound: the screen's. Nothing hands the
player's audio to a controller yet, and the scene has none; Community Modules' `ARS Controls` UI does that handover,
and we will do our own when the first ARS seed is built.
