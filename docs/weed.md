---
aliases:
  - "Weed"
tags:
  - crop
---
# Weed

Models from damdamdaaam (`weed.zip`). Everything in this doc is built: the crop, the splif, eating and smoking. The
trip they start is [[trip]].

Plant a weed seed, grow a stem with leaves, harvest the blooms, sell them — and every bloom sold
leaves a **splif** in front of the sell point. The splif is not sold. It is picked up and smoked, through our own
trip ([[trip]]); eating a bloom instead of selling it is the intense version.

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
sockets are where the blooms sit. It was chosen when `MultiHarvestPlant` had no ending (#43); since
2026-11-01 a regrowing plant ends after a season ([[crops]]), so weed could regrow if wanted.

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
is not fiddly in VR. **It is not sellable**: there is no `SellableEntity` on it at all. Like every grabbable, a far grab pulls it into the hand ([[crops]] → *To add a crop*).

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

**Status (2026-11-04):** rebalanced as the windfall find; compiles clean, untested in-world.

`WeedSeed.Init()` holds its numbers, nine blooms to a plant; they are in [[crops#Balance|crops → Balance]]. All nine
sockets are kept: Somnium's object limit is in the thousands (Vulps, 2026-11-01).

## Build order

Only the first step needs the models.

1. **Three prefabs and `WeedSeed`**, copied from the pumpkin's (the bearing crop), with the bloom sockets taken
   from the model's buds. See *The models*.
2. **`Splif_Weed`** as an inert prop. Grabbable, hovering, registered on `SceneNetworking`. No model came
   with the pack; a plain rolled-paper shape is fine (Vulps, 2026-11-01).
3. **`BloomModule : BehaviourModule`** — spawn the splif on `Sold` at the sell point's drop point, master
   only. This is the step that tests the module layer. Steps 2 and 3 built 2026-11-01.
4. **Smoking, eating and the trip** — our own trip system (*Eating and smoking*,
   [[trip]]). All built 2026-11-01.

All four are built and played in-world (2026-11-01).

## Open

- Whether a splif should ever expire, for tidiness rather than object count. Auto-recall or a lifetime, decided
  when it matters.
- Whether harvesting a bloom should drop anything, or only selling one. As written, only selling.

## Eating and smoking

**Status (2026-11-01):** built and played in-world; Vulps called weed done for this pass. The logs show eating and
smoking to the end.

Both start a trip, which is its own system ([[trip]]): eating the bloom the intense one, smoking the splif the mellow
one.

- **Eating** (`EdibleModule` on `Produce_WeedBloom`): a harvested bloom held within 12 cm of the mouth for 0.5 s is
  eaten: strength 1 for 180 s. The mouth is `PlayerManager.LocalMouthPosition`, a little below and in front of the
  headset.
- **Smoking** (`SmokeableEntity` on `Splif_Weed`): held at the mouth, a puff every 5 s, 12 puffs. Each adds 20 s of
  strength 0.25. A puff is message 8 on the splif's messenger (`CollectibleMessageType.puff`), so every
  player sees the smoke from the splif's lit end; the count travels with it, so a splif passed on carries on from where
  it was. The last puff's smoke drifts off on its own as the splif goes.

> [!info] Networking
> Eating and the last puff are ended **by the holder**, not the master: holding something gives that client state
> authority over it (the holder rule), and nobody else can hold it at the same time, so there is no decision to
> disagree with. Only the smoke is sent, so others see it.

## The design

**Status (2026-11-01):** proposed by Vulps 2026-10-31; built as *Eating and smoking*, which is current where the two
differ (eating at 12 cm rather than 20, the holder rather than the master ending an eaten bloom).

**Both, at two strengths, chosen by the player** (Vulps, 2026-10-31): eating the bloom raw is the **intense** trip;
selling it and smoking the splif it leaves is the **mellow** one. The bloom is the decision: Thatch plus a gentle
high, or no Thatch and the full experience. Because the intense version is opted into knowingly, it is free to be as
warped and disorienting as we like; the mellow version stays comfortable enough for anyone. How the trip looks and
feels: [[trip#The design|trip → The design]].

- **Eating:** held produce within about 20 cm of the local head counts as eaten.
- **Smoking** (Vulps, 2026-11-01): hold the splif to your mouth. Every 5 s held there is a puff: a puff of smoke,
  and the mild high lasts a little longer. After 12 puffs the splif is used up.
