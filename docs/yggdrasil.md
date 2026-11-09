---
aliases:
  - "Yggdrasil"
tags:
  - crop
  - mechanic
---
# Yggdrasil

The god seed: the endgame **buff**. Planted, a world tree about 90 m tall rises behind its Plot and multiplies what
the Plot grows for as long as it stands; then it dies, lets go of the island and falls into the void.

## The design

**Status (2026-11-06):** decided by Vulps; built, compiles clean, untested in-world.

- **In the stalls** (Vulps, 2026-11-09: "it's ready"): `spawnWeight` 0.0008, about 1 island stall in 1,000 (Vulps,
  2026-11-04), scaled from the Moonflower's 0.008 at about 1 in 100; `Buyable_Yggdrasil` is last in
  `WorldManager._buyables`. Also in the Developer Market ([[dev-tools]]). A stall holding it plays its voiceover to a
  player walking up ([[tutorial#Seed voiceovers]]). **Price 2,000 Thatch** for now, 5,000 once Thatch
  persists and the rest is rebalanced (Vulps, 2026-11-05): nobody plays long enough without a crash to save 5,000.
- **A buff, not an upgrade** (Vulps, 2026-11-05): it stands **30 minutes** from fully formed, then dies. It never goes
  back to a warehouse ([[warehouse]]).
- **One at a time, worldwide** (Vulps): while a Yggdrasil stands, no stall stocks its seed (`YggdrasilSeed.MayBeStocked`
  reads `YggdrasilPlant.AnyStanding`; `ShopKeeper` gives a crop that may not be stocked no weight), and none can be
  planted (`MayBePlanted`). At this rarity two in one
  session would be nigh on impossible anyway.
- **The buff is crop scale** (Vulps): every crop growing in its Plot while it stands grows bigger and is worth more,
  in proportion to how much of its growing it spent under the tree. The fruit really is drawn that big: carrying a
  huge carrot that blocks your view is the fun of it. How scale becomes value: [[economy#Crop scale|economy → Crop
  scale]].
- **Nobody can remove it** (Vulps): not the owner, not a teammate.
- **Its owner leaving does not end it** (Vulps): it stands until its time runs out or someone else claims the Plot,
  and then it falls, just to mess with people's hopes.
- **Climbable and walkable** (Vulps): a mesh collider under a `Climbable` marker ([[climbing]]).
- **Towering, low poly, just the tree** (Vulps): no Nine Worlds, no creatures.
- **It goes through the Moonflower's dome** for now ([[moonflower]]).

## Growing

**Status (2026-11-06):** decided by Vulps; built, compiles clean, untested in-world. The sapling's growth time (180 s)
and the multiplier (20×) are placeholders until Vulps sets them.

- **A sapling grows where the seed is planted**, in a PlantSlot like any plant.
- **Runes spew up from the sapling while it grows** (Vulps), a waterfall in reverse: glowing Elder Futhark glyphs
  rising on negative gravity, a trickle when planted and a torrent as it nears grown. They stop when the stream starts.
- **Fully grown, the sapling builds the tree:** a particle stream rises from it and constructs Yggdrasil behind the
  Plot, the way popcorn builds the screen ([[popcorn]]), while the sapling shrinks to nothing. A rolling, rumbling
  thunder plays through it, like the earth moving when an earthbender works (*Avatar: The Last Airbender*).
- **The slot frees once the tree is formed** (Vulps): the sapling takes a slot while it grows; the tree takes none.
- **The 30 minutes start when the tree is formed.**

## How it looks

**Status (2026-11-05):** Plot 0's approved by Vulps from Blender previews and the Editor; not seen in-world.

- **The trunk stands behind the Plot, not over it.** About 18 m across, four strands twisting round each other with a
  slight bend. It climbs out of the cliff below the Plot's back edge, its buried end curling back under the Plot into
  the rock, its flank leaning out to the back edge. Behind the rim it can be as wide as it likes; only the Plots are
  off limits.
- **Halfway up it opens like an upturned dome** (Vulps): seven thick limbs fan out 40–50 m like cupped fingers and
  each turns skyward into its own short tree with its own crown; an eighth rises from the middle, level with the
  ring so the canopy has no hole. The crowns overlap into one canopy about 132 × 125 m across, 70–91 m up. Its edge is
  about 66 m from the Plot; the nearest neighbouring Garden's island starts about 100 m away.
- **The arch is two roots** (Vulps, after old trees' roots lifting out of the ground): they leave the trunk about
  13 m up, bow out and come down on either side of the Plot, meeting the ground halfway along it and diving into the
  soil. From the road the tree straddles the back half of the Plot. The inner foot stands on the 0.78 m strip
  between Plots and stops at the Plot's dirt; the outer one has room to spare.
- **The other roots wrap the cliff**, fanned round the side facing the island, around it more than down it,
  sitting about three quarters proud of the real rock and never onto the Garden's top. They follow the rock's outer
  surface, found by casting in from beyond the island: the island is overlapping rocks, and the nearest surface is
  often one buried inside another rock. **A root with nothing to hold on to dangles** (Vulps), swaying down and
  tapering to a point; every root ends in a point.
- **Vines** (Vulps: the trunk was "pretty plain"): five strands of ivy spiral up the trunk to the dome, leaf pads
  lying flat either side of the stem, and lianas sag between the dome's limbs.
- **Colour:** a warm dark bark, so it stands apart from the rock; two greens in the canopy; the ivy the lighter one.
- **The seed** is a samara, the ash's winged key: a twisted wing on a pod ringed with five glowing Elder Futhark runes.

## The model

**Status (2026-11-05):** built by `Models/Yggdrasil/Yggdrasil.blender.py`; imported, untested in-world.

- **One variant per Plot**, `Yggdrasil_Plot0`–`3.fbx`: the four Plots back onto different cliffs and two are
  mirrored, so each variant's roots are traced over its own rock. Each is in its Plot's local space: put it at the
  Plot's transform, with no rotation or scale. Every Garden is identical, so four variants cover the world.
- **In each:** `Trunk_LOD0` (trunk, dome, sub-trees), `Crown_0`–`7_LOD0` (one per crown, so each can shed),
  `Vines_LOD0`, `Roots_LOD0` skinned to `RootRig`, a `Pivot` empty at the trunk's base, and the far `Trunk_LOD1` (roots
  merged in, not skinned; no vines) and `Canopy_LOD1`; Unity builds the LODGroup from the names. Plot 0's is about 10,600 faces near (the vines are about 4,000 of them), 2,400 far.
- **The rig:** `RootBase`, then a chain per root, `Root_<root>_<bone>`, a bone every 4 rings; roots 0 and 1 are
  the arch. About 120 bones, varying by Plot.
- **Normals are checked at build:** the script refuses to export a part whose normals point inward. Every tube was once
  wound inside out, which Blender's previews hid and Unity showed as flat, see-through roots.
- **Also:** `Yggdrasil_Leaf.fbx` (a 0.5 m two-sided ash leaflet, for falling leaves), `Yggdrasil_Sapling.fbx` (1.2 m,
  pivot at its base) and `Yggdrasil_Seed.fbx` (1 unit long, pivot at its centre; 30 cm in the seed prefab). Its market
  picture, `Textures/Market/YggdrasilSeed.png`, is rendered in Blender, not the Editor. Materials `Yggdrasil_Bark`, `_Leaves`, `_LeavesLight`, `_Vine`, `_SeedWing`,
  `_SeedPod`, `_Rune` (emissive) are embedded in the FBXs; `Materials/Yggdrasil_Stream`, `_FallingLeaf` and `_Runes` are
  assets. The runes' atlas, `Textures/Yggdrasil_Runes.png` (eight glyphs, 4×2: the seed's five and three more), is drawn
  by `Textures/yggdrasil_runes.py`.
- **Rebuilding it needs the island:** **Grow A Garden → Export Garden Island For Blender** writes
  `Library/GardenIsland.obj` in Blender's axes, then
  `blender --background --factory-startup --python Yggdrasil.blender.py -- <Models/Yggdrasil> <preview dir> Library/GardenIsland.obj [plot]`
  (with a Plot, only that variant is built and previewed, from the road, the town, above and below). If the Garden's
  rocks or Plots move, re-export and rebuild; the Plot positions are a table at the top of the script.

## Dying

**Status (2026-11-06):** decided by Vulps; built, compiles clean, untested in-world.

Its time runs out, or its Plot is freed and then claimed again. Seconds after the fall begins:

1. **0–16: the leaves rust**, and from 2 to 20 rusty leaves fall from the canopy.
2. **8–20: the crowns shed**, each shrinking into itself.
3. **21: the roots let go** and dangle (Vulps: "can we make the roots floppy?"); the colliders go.
4. **24 on: it topples outward**, away from the Plot, over the rim, and drops. The master despawns it at 34 s, far
   below the floor.

- **The fall is worked out from the clock, not simulated:** the same lean and drop on every client from the fall's
  timestamp, as growth is. A plant has no NetworkTransform, and with nothing to hit but the floor, a physics fall would
  only add a sync to look the same.
- **The roots dangle** by a script swinging each root's bones like a rope pinned at the trunk. No physics joints; each
  client dangles its own, and nothing is sent.

## How it is built

**Status (2026-11-06):** built, compiles clean, untested in-world.

- **Prefabs** `Seed_Yggdrasil` and `Plant_Yggdrasil` (`prefabs_crops/`), copied from the Moonflower's for their network
  setup, both registered on `SceneNetworking`; the seed is in the Developer Market, not on any stall.
- **`YggdrasilSeed`** (`Seeds/`): the definition. `buffMultiplier` is new on `SeedDefinition`.
- **`YggdrasilPlant`** (`WorldTree/`, a `Plant`): the sapling. Its life is planted, grown (+ growth), formed (+ 12 s
  build) and the fall (`Plant`'s wither timestamp). `MayBePlanted` refuses a second while one exists, asked of the prefab
  by `PlotStateManager` before planting. `FreeSlot` and `BeginFall` are the master's. It drives the rune fountain
  (`Runes`, on the prefab's root so it outlives the shrinking body) from the clock to the millisecond: rate, lift and
  spread swell with growth squared, and nothing is sent.
- **`YggdrasilTree`** (`WorldTree/`): the show and the master's decisions. Stands the variant for its Plot's index in
  its Garden at the Plot's transform; the stream, steered piece by piece onto the tree as far as it is built; the
  rumble (`sounds/Yggdrasil/yggdrasil_sounds.py`, `earth_rise.wav`), its mixer group copied from the Garden's dome so it
  follows the Ambience volume; the buff record on the Plot; the colliders; the dying.
- **`RootDangle`** (`WorldTree/`, on each variant): the roots' rope swing, off until the roots let go.
- **The tree in the prefab:** `Tree` (the `Climbable` marker) → `Variant_0`–`3`, each a pivot at its trunk's base →
  the model, unpacked, with mesh colliders on the trunk, roots and crowns, each crown under a `Shed_` pivot at its middle.

> [!info] Networking
> Nothing new is sent for the tree: planted and the fall are `Plant`'s state, and every client derives the build,
> the buff, the dying and the fall from them. The master frees the slot once it is formed (the slot byte goes out as
> 255) and starts the fall, on its time or on `PlotLeaseManager.OwnerChanged`. A new master picks both up from the clock.
> The buff on crops: [[economy#Crop scale|economy → Crop scale]].

## Future — the Moonflower crowns it

**Status (2026-11-05):** an idea from Vulps, not designed. The Moonflower's beam targets the top of Yggdrasil, and
its branches become the dome.
