---
aliases:
  - "Weed"
tags:
  - crop
---
# Weed

Models from damdamdaaam arrived 2026-11-01 (`weed.zip`). Everything in this doc is built: the crop, the splif,
eating, smoking, and the trip with its colours, melting world and things in the corner of the eye.

Plant a weed seed, grow a stem with leaves, harvest the blooms, sell them — and every bloom sold
leaves a **splif** in front of the sell point. The splif is not sold. It is picked up and smoked, through our own
trip system; eating a bloom instead of selling it is the intense version.

## Why this one matters beyond itself

It is the **first crop whose produce does something after the sale**, and so the first real customer
for `BehaviourModule`. Everything up to now has been another plant whose produce is a thing you carry
to the sell point; this is the case the module layer was built for, and building it will say whether
that layer is right.

If a splif needs anything the module base cannot give it, that is a finding about the architecture,
not about weed.

## The chain

Three prefabs and a definition, exactly like every other crop — the shape is not new.

```
Seed_Weed  ──_plantPrefab──▶  Plant_Weed  ──_producePrefab──▶  Produce_WeedBloom
                                                                      │
                                                          BloomModule │ on sale
                                                                      ▼
                                                                 Splif_Weed
```

**`Plant_Weed` is a `SingleHarvestPlant`.** A stem hung with blooms is a bearing plant, and the
sockets are where the blooms sit. `MultiHarvestPlant` is the tempting choice — a plant that keeps
producing suits weed — but it **has no ending** (#43): with plants ungrabbable there is no uproot
gesture, so a multi-harvest weed holds its PlantSlot until its owner's lease expires. Take the shape
that withers. Revisit if #43 ever gets a real answer.

**Author the sockets as a plain `Transform[]`.** `BearingPlant._produceSockets` already is one, and
it is one because arrays of custom `[Serializable]` classes arrive empty from the bundle export —
invisible by every means the Editor offers, only visible in-world.

## The splif

**Status (2026-11-01):** built (steps 2 and 3), tested in-world.

**A splif is a `BehaviourModule` on the bloom, not a change to selling.** `SellableEntity` raises `Sold` on every
client before `OnSold()` ends the object, and that is where reactions to a sale hang, so `SellPoint` never learns
what weed is.

```
SellPoint accepts
  └─ SellableEntity.Sell(dropPoint)
       ├─ Sold            ← BloomModule spawns the splif at GivenBackAt, master only
       └─ OnSold()        ← the bloom despawns as usual
```

**It appears on the ground in front of the sell point** (Vulps, 2026-11-01). The sell point is a bundle of
barrels and bags under a tent, with no collider and fences on two sides, so there is nothing to put things on
and the bloom's own drop point is inside the barrels. `SellPoint._dropPoint` is a `DropPoint` child placed on
the open side facing the Garden, at (7.15, 0.6, 2.8); `SellPoint` hands it to the item it is selling
(`SellableEntity.GivenBackAt`, set just before `Sold`), and `BloomModule` spawns there. Moving the marker moves
where anything a sale gives back appears.

**Spawning is a fact, so it is master-only**, `SharedModeStateAuthMasterClient` like every other runtime spawn.
`Splif_Weed` is on `SceneNetworking._networkPrefabs`.

**`Splif_Weed`** (`prefab_items/`) is a copy of the blank deed scroll's prefab: a `CollectibleEntity`
(`collectibleId` `splif`) that is grabbable and hovers, with the scroll's `PlotApplication` taken off. The mesh
is a 9 cm tapered roll of paper built in code (`Models/Weed/Splif.asset`), with a slightly larger grab box so it
is not fiddly in VR. **It is not sellable**: there is no `SellableEntity` on it at all.

## shifterhead's system

**Superseded (2026-11-01):** the plan was to wire the splif into shifterhead's system for getting high. Our own trip
system replaced it (see *Eating, smoking and the trip*), so none of its open questions need answers.

## The models

**Status (2026-11-01):** imported; step 1 built, tested in-world.

The pack is one FBX and PBR maps for four parts, named for the artist's scene rather than for their use:

| FBX part | What it is | Used as |
|---|---|---|
| `Seed` | a sprout: stem and two leaves | the shop item (`Seed_Weed`), as every crop shows its seedling |
| `Plant` | the grown plant, with nine buds and 48 leaves built into the mesh | `Plant_Weed` |
| `Blossom` | one bud; the nine on the plant are copies of it | `Produce_WeedBloom` |
| `Leave` | one leaf; the plant's 48 are copies of it | not imported: the stem already has its leaves |

**Prepared in Blender before import** (the scripts were one-off): the nine buds were split off the plant and
each one's pose fitted to the `Blossom` mesh (an exact fit; the stem's buds are 1.348× the loose one), so a
real bloom spawns exactly where a modelled bud was and the bare plant shows nothing once they are picked.
Those poses are the plant's nine `ProduceSocket`s. Pivots went to each part's base, and sizes to a 1.1 m
plant, a 15 cm sprout and a bloom matching the stem's buds (about 8 cm). The result is
`Models/Weed/Weed.fbx`, in the tracked tree rather than an asset package.

**Maps:** BaseColor and Normal per part, and a baked MetallicSmoothness (smoothness = 1 − roughness) for
the plant and the bloom, whose roughness varies; the sprout's is flat, so it is a constant. Alpha, emission
and metallic were flat and displacement is unused by URP Lit, so none were imported. The plant and sprout
materials draw both faces, since their leaves are single cards.

**The prefabs are copies of the pumpkin's**, which is the bearing shape. There is no "New Seed" tool,
whatever earlier text here said. Each copy got the weed definition, mesh and colliders, and every
reference between the three was pointed at its weed counterpart. `Buyable_Weed` sits under SceneManager
with the other definitions, and the three prefabs are on `SceneNetworking`.

## Balance

**Status (2026-11-01):** agreed by Vulps; grown and sold in-world.

`WeedSeed.Init()`: buy 80, **30 per bloom** with nine to a plant (270 a plant), 90 s to grow, 60 s to ripen,
30 s to wither, `spawnWeight` 0.15, a rarer find than pumpkin's 0.25. All nine sockets are kept: Somnium
now handles the object count (Vulps, 2026-11-01), which answers the per-world limit worry below for
blooms.

## Build order

Only the first step needs the models.

1. **Three prefabs and `WeedSeed`**, copied from the pumpkin's (the bearing crop), with the bloom sockets taken
   from the model's buds. See *The models*.
2. **`Splif_Weed`** as an inert prop. Grabbable, hovering, registered on `SceneNetworking`. No model came
   with the pack; a plain rolled-paper shape is fine (Vulps, 2026-11-01).
3. **`BloomModule : BehaviourModule`** — spawn the splif on `Sold` at the sell point's drop point, master
   only. This is the step that tests the module layer. Steps 2 and 3 built 2026-11-01.
4. **Smoking, eating and the trip** — our own trip system rather than shifterhead's (see *Eating, smoking and the
   trip*). All built 2026-11-01.

All four are built and played in-world (2026-11-01).

## Open

- Whether a splif should ever expire. [[runtime-spawn]] §4c notes an unfound per-world object limit,
  and an item that is never sold and never despawns is the shape most likely to find it. Auto-recall
  or a lifetime, decided when it exists.
- Whether harvesting a bloom should drop anything, or only selling one. As written, only selling.

## Eating, smoking and the trip

**Status (2026-11-01):** all built and played in-world; Vulps called weed done for this pass. The logs show eating,
smoking to the end, the colour trip, the world melting and coming back, and control flips (28 on foot, 7 flying).
The visions and ending early by covering the eyes log nothing and are not confirmed by the log.

**Built:**
- **`TripManager`** (on SceneManager): one local trip with a strength. A global `TripVolume` (priority 10, weight 0
  at rest) whose profile `Models/Weed/Trip.asset` is the trip at full strength: `ColorAdjustments` (hue cycling ±180°
  over 40 s, saturation +45, contrast +15), `ChromaticAberration` 0.6, `Vignette` 0.3. The trip eases in over 12 s and
  out over 20 s, and its strength is the volume's weight. Both hands over the eyes for 2 s ends it early (out over 4 s).
  Needs `Unity.RenderPipelines.Core.Runtime`, now referenced; it is on Somnium's allowed list.
- **Eating** (`EdibleModule` on `Produce_WeedBloom`): a harvested bloom held within 12 cm of the mouth for 0.5 s is
  eaten: strength 1 for 180 s. The mouth is `PlayerManager.LocalMouthPosition`, a little below and in front of the
  headset.
- **Smoking** (`SmokeableEntity` on `Splif_Weed`): held at the mouth, a puff every 5 s, 12 puffs. Each adds 20 s of
  strength 0.25. A puff is message 8 on the splif's bridge (`CollectibleMessageType.puff`, reserved there), so every
  player sees the smoke from the splif's lit end; the count travels with it, so a splif passed on carries on from where
  it was. The last puff's smoke drifts off on its own as the splif goes.

- **Melting** (`Vulps/TripLit`, `Shaders/TripLit.shader` + `TripWobble.hlsl`): URP Lit, copied, with every vertex
  pass that draws the world wrapped to sag and wobble by the global `_GAG_Trip`, which only the tripping player's
  `TripManager` sets (to the trip's level squared, so the mild high barely stirs and the intense one melts). At 0 it
  is identical to Lit. `Materials/Trip/` holds a twin of each of the 17 world materials (ground, rocks, crops,
  fences, weed); `TripManager` swaps renderers to their twins only while tripping, every 2 s to catch new spawns,
  and swaps them all back when it ends. Prefabs are untouched, and nobody pays for the shader at rest. Amounts are in
  object space, so a carrot wobbles a little and a great rock a lot.
- **Visions** (`TripVisions` on SceneManager): from strength 0.5, local only. A carrot, pumpkin, turnip, bloom, sprout
  or splif appears 40–60° off where the head points, 2.5–4 m away, every 9 s at the threshold down to 3 s at full
  strength, at most 6 at once. It fades in, spins and drifts; looked at (within 15° of where the head points: there
  is no eye tracking) it dissolves into a rainbow swirl (`TripSwirl`). Unseen ones fade after 20 s.
- **Flipped controls** (Vulps, 2026-11-01; played in-world): only an **eaten** trip, at strength 0.8
  and up, flips the controls for 0.5 s every 6–15 s. Flight is ours, so `FlightController` negates pitch and turning
  while `TripManager.ControlsFlipped`. Walking is Somnium's and the bridge only scales its speed, so walking is
  flipped by setting the speed to −1 for the moment. Somnium accepts the call; whether walking actually reverses is
  for Vulps to say, since the log only shows the speed being set. The `Flip()` line says which was flipped. Flight and climbing own the speed while they run, so walking is only
  flipped on the ground and only handed back if neither has taken it since.

> [!info] Networking
> Eating and the last puff are ended **by the holder**, not the master: holding something gives that client state
> authority over it (the holder rule), and nobody else can hold it at the same time, so there is no decision to
> disagree with. The trip is local and sends nothing; only the smoke is sent, so others see it.

### The design

Proposed by Vulps 2026-10-31, kept as written. Where the built values differ (eating at 12 cm rather than 20, the
holder rather than the master ending an eaten bloom), *Built* above is current.

**Both, at two strengths, chosen by the player** (Vulps, 2026-10-31): eating the bloom raw is the **intense** trip;
selling it and smoking the splif it leaves is the **mellow** one. The bloom is the decision: Thatch plus a gentle
high, or no Thatch and the full experience. Because the intense version is opted into knowingly, it is free to be as
warped and disorienting as we like; the mellow version stays comfortable enough for anyone. One trip system with an
intensity value, not two systems, and the splif may use it instead of shifterhead's.


- **Eating:** held produce within about 20 cm of the local head counts as eaten.
- **Colour:** a URP Volume enabled only on the eater's client (the scene already has `PostProcessing`), slowly
  cycling hue shift, saturation and colour filter. No flashing: Somnium forbids seizure-risk effects, so changes stay
  slow and smooth.
- **Droop and wobble:** a full-screen distortion needs a renderer feature or the opaque texture, both owned by
  Somnium's client, and lens distortion is generally unsupported in XR. What the world can do is make **our own**
  materials droop and wobble from one global shader value: islands, crops and the Garden melt while Somnium's avatars
  and UI do not. Needs our own shader variants of the rock and crop materials.
- **Things in the corner of your eye:** no eye-tracking API, so head direction stands in for gaze. Seeds, animals and
  tools spawn locally 40–60° off head-forward; when one comes within about 15° of centre it dissolves into a swirl of
  colour.
- **Comfort:** eased in and out, with an obvious way to end it early at either strength. Whole-view warping is the
  quickest route to VR sickness, which is what the intense trip is for and the mellow one must avoid.
- **Somnium's content rules do not object** (Vulps, 2026-11-01). Players are all ID-verified adults.
- **Smoking** (Vulps, 2026-11-01): hold the splif to your mouth. Every 5 s held there is a puff: a puff of smoke,
  and the mild high lasts a little longer. After 12 puffs the splif is used up.
