---
aliases:
  - "Popcorn"
tags:
  - crop
---
# Popcorn

An upgrade, not a crop to sell ([[fun-seeds]]): grown, its popcorn flies to the Garden's screen and builds it, and the
screen plays whatever a player links. The screen's sound is what the light show dances to ([[light-show]]).

## The design

**Status (2026-11-04):** built; played in-world. In the stalls (untested there) and the Developer Market.

Vulps's design:

- **The seed is a kernel**, the unpopped bit (`Seed_Popcorn`).
- **The plant is a real piece of popcorn** growing out of the ground, about 30% buried like a carrot, swelling to
  50 cm while popcorn sprays out of its top, livelier as it grows.
- **The popcorn builds the screen.** When a grown plant takes the screen, its spray stops and a stream of popcorn flies
  from it to the screen, each piece joining the screen's building edge, which rises from the bottom as they arrive:
  nanobots, or programmable matter.
- **Permanent for the session.** No lifespan: it holds its PlantSlot until the Plot is cleaned up after its owner leaves.
- **A second one may be planted.** It grows and keeps spraying while it waits; only the oldest grown popcorn plant in
  the Garden holds the screen. When that one goes, the next takes over and rebuilds the screen from nothing.
- **Who sets the link is Somnium's rule**: the native controls let anyone take or release control.


## Every screen but world spawn's, hidden for now

**Status (2026-11-03):** temporary, for one session (Vulps); compiles clean, untested in-world.

`Video/ScreenStandIn` (on SceneManager) hides what every Garden's screen draws except world spawn's (`Town_Capital`'s
first Garden) and puts a notice in its place, readable from both sides: "Multi-screen support coming soon / in the mean
time join the party at world spawn / (you will need another popcorn seed)". The players keep running and syncing, so
popcorn planted at another Garden still builds its screen, hidden behind the notice. Remove it once screens play only
near their players ([[towns]]).
## The screen

**Status (2026-11-03):** built; played in-world.

- **Somnium's video player cannot be spawned.** `SomniumVideoPlayer` is a built-in component: the client attaches its
  real player (AVPro) at load and numbers these components by hierarchy order for syncing, so each must be in the scene
  from load and never created, deleted or reordered (`WorldCreatorContext.md` → Built-in runtime components). So every
  Garden has a player that always exists, and popcorn **reveals** it.
- **One per Garden**: Community Modules' `ARS Somnium Video Player` in each Garden's `GardenCentre`, at the Garden's
  east edge facing the plots, (10.4, 2.9, 5.0) for Garden 0 ([[towns]]). It must point at itself
  (`ARSVideoPlayer._videoPlayer`, `SomniumVideoPlayer._audioSettings`), as in Community Modules' prefab, or ARS never
  hears it.
- **No link, no autoplay.** A screen opens nothing until a player sets a link, and its controls stay off until popcorn
  has built it. A default link would make all 14 open the same stream at load, which pins the client's main thread.
- **It faces the plots: rotation 90.** The picture is a Unity quad and the controls a world-space canvas, both facing
  the player object's **−Z**: from behind you see the black `BackQuad`.
- **What Somnium builds under it**: `BridgeVideoPlayer(Clone)` → `VideoPlayer` (`AVProPlayerController`), `BackQuad`,
  `FrameParent/Frame` (a unit quad at the player's origin, AVPro's `Unlit/Opaque` material fed by `ApplyToMesh`, with a
  `BoxCollider`) and `Controls`, a world-space canvas (Play/Pause, Reload, Live, Mute, Volume, Change link, Release
  control, a Link field).

## How it is built

**Status (2026-11-03):** built; played in-world.

- **`PopcornSeed`**: nothing to sell; its price, grow time and rarity are in [[crops#Balance|crops → Balance]].
  `Buyable_Popcorn` sits under SceneManager and is in `WorldManager._buyables`, so the stalls sell it.
- **`StandingPlant`** (`Crops/`): grows, then stands, bearing nothing and never withering; the plant shape for
  upgrades ([[crops]]).
- **`ScreenManager`** (`Video/`, one per screen, on its video player): which grown popcorn plant in the Garden holds
  the screen, the oldest by planting time (`Holder`). Raises `Assembling(plant)` when a plant takes it in view, and
  `VisibilityChanged` when it is simply up (a late joiner) or gone. Keeps the building edge for the popcorn to aim at; a
  build takes `_buildSeconds` (6 s). A plant finds its Garden's screen with `ScreenManager.For(position)`.
- **`ScreenReveal`** (on the player): never touches the player's own transform, since Somnium sizes its controls from
  it around load and a squashed player gives a sliver of controls for good. It squashes only the picture and its
  backing (`FrameParent`, `BackQuad`, found once Somnium has attached them), pinned at the bottom edge, and switches the
  control canvases off until the screen is whole. Building, the edge rises over the build time and is reported to
  `ScreenManager`; with no holder it sinks over 3 s. The player stays active, as Somnium requires.
- **`PopcornPlant`** (on `Plant_Popcorn`): the `Spray` particle system (gravity, bouncing, shrinking away), off while
  this plant holds the screen; and the `Stream`, no gravity, emitting for the build time less the flight, each particle
  steered in `LateUpdate` to its own spot along the building edge (its random seed picks the spot), with a sideways
  wander, removed on arrival.
- **The body** is `Popcorn` (scaled by growth) holding `Mesh` 0.15 units up, so about 30% is buried at every size.
- **Models**: the kernel is built in the Editor, `Models/Popcorn/Kernel.asset` (a lathed teardrop, 1 unit tall, pivot at
  its base; its profile clamps sin at the tip, which otherwise goes negative and makes every vertex NaN). The popped
  piece is made in Blender by `Models/Popcorn/Popcorn.blender.py`: metaballs for the puffs, voxel-remeshed, clouds and
  Voronoi displacement, a golden hull fragment at the base; exported as `Popcorn.fbx` (3000 triangles, the plant) and
  `Popcorn_Small.fbx` (308, each particle; puffs only, in one material, since a particle mesh draws its first sub-mesh
  only). Materials `Popcorn` (cream) and `Popcorn_Kernel` (golden), each with a trip twin. Rerun:
  `blender --background --factory-startup --python Popcorn.blender.py -- <out_dir> <preview_dir>`.

> [!info] Networking
> Nothing new is sent. The plant is planted, grown and uprooted like any crop, its growth derived from one timestamp,
> so every client works out the holder, the build and the stream for itself. A late joiner gets the screen already up;
> a takeover, or a plant grown in view, builds it. Playback is synced by Somnium's player.

## Open

- **A real dissolve** instead of the squash: give `Frame` a material of ours with a dissolve and the same `_MainTex`;
  `ApplyToMesh` should keep feeding it the video. Untried.
- **Playing only near you**: a far screen, or one whose popcorn has gone, still plays on every client. Pause and Stop
  are synced, so they are out; how Base Reality does it is being asked ([[todolist]]).
