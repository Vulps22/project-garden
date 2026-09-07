# Weed

Blocked on models from damdamdam. Designed now so the build is assembly rather than decisions.

Plant a weed seed, grow a stem with leaves, harvest the blooms, sell them — and every bloom sold
leaves a **splif** behind where it stood. The splif is not sold. It is picked up and smoked, through
shifterhead's existing system for getting high.

## Why this one matters beyond itself

It is the **first crop whose produce does something after the sale**, and so the first real customer
for `BehaviourModule`. Everything up to now has been another plant whose produce is a thing you carry
to the counter; this is the case the module layer was built for, and building it will say whether
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

**A splif is a `BehaviourModule` on the bloom, not a change to selling.**

`SellableEntity` already raises `Sold` on every client *before* `OnSold()` ends the object, and the
docstring says exactly what that is for: "anything that needs to react to the sale hangs off this
rather than off SellPoint." So `SellPoint` never learns what weed is, and `SellableEntity` is not
touched.

```
SellPoint accepts
  └─ SellableEntity.Sell()
       ├─ Sold            ← BloomModule spawns the splif here, master only
       └─ OnSold()        ← the bloom despawns as usual
```

**Position has to be captured before the sale, not read during it.** `SetPending(true)` runs the
moment the bloom is offered — it disables renderers and colliders and takes the object out of the
world — so by the time `Sold` fires, "where the bloom was" is already a question about an object
that has visually left. Record the transform on `SetPending`, spawn against the recorded pose.

Which raises the thing to **decide before building**: *where* should a splif appear?

- **At the counter**, where the bloom was handed over. Simple, and it reads as change given with the
  money. Risk: a pile of splifs accumulating at the sell point, which is also where every other
  player is standing.
- **At the plant it came from.** More poetic, harder — the bloom would have to remember its socket,
  and the plant may have withered by then.

The first is recommended, with the second recorded as the more interesting version if splifs turn
out to be worth walking for.

**Spawning is a fact, so it is master-only**, `SharedModeStateAuthMasterClient` like every other
runtime spawn — the client that decides a thing exists is the client that owns it. `Splif_Weed` goes
in `SceneNetworking._networkPrefabs` with the rest.

**A splif is not sellable.** No `SellableEntity` on it at all. It is grabbable, it hovers, and it is
consumed. That it cannot be sold is not a flag to set — it is a component not present.

## shifterhead's system — the open half

The one genuine unknown, and the only thing here that is not already solved by an existing seam.

**Questions to answer before writing any of it:**

- What is the integration surface — a component you add to an object, a call you make, an event you
  raise? That decides whether the splif *has* a shifterhead component or *calls* one.
- Does it survive the export? Anything it references has to compile server-side against
  `ScriptingReferences.txt`. If it lives in an assembly the uploader rejects, the world publishes
  with a 200 and silently never appears — the failure this project has already paid for twice.
- Is it per-client or replicated? Getting high is a local visual effect, which suggests it runs on
  the smoker's machine and needs no sync. Confirm rather than assume.
- What consumes the splif — a gesture, a timer, a trigger? That decides whether the splif needs its
  own `BehaviourModule` or a plain component.

**Until those are answered, build the splif as an inert prop.** Grabbable, hoverable, spawns on the
sale, despawns on some rule. Wiring it to shifterhead is then one component on a thing that already
exists and already works.

## Balance

Not set. Weed should sit low on `spawnWeight` so it is a find rather than a staple, and the blooms
should be worth carrying — a bearing plant is a longer commitment than a carrot and the price has to
pay for the wait. Numbers belong in `WeedSeed.Init()` with everything else, set against the economy
at the time it lands rather than today's.

## Build order

Only the first step needs the models.

1. **Three prefabs and `WeedSeed`.** Use **Grow A Garden ▸ New Seed** with `PumpkinSeed` as the
   template — it is the existing bearing crop, so the shape comes across correct. Then place the
   bloom sockets on the stem by hand.
2. **`Splif_Weed`** as an inert prop. Grabbable, hovering, registered on `SceneNetworking`.
3. **`BloomModule : BehaviourModule`** — capture pose on pending, spawn the splif on `Sold`, master
   only. This is the step that tests the module layer.
4. **shifterhead**, once the four questions above have answers.

Steps 1–3 are a working crop that drops a prop you can pick up. Step 4 makes it do something. They
are separate uploads, and step 3 is the one worth watching the logs for.

## Open

- Where the splif appears — counter or plant. See above; counter recommended.
- Whether a splif should ever expire. `runtime-spawn.md` §4c notes an unfound per-world object limit,
  and an item that is never sold and never despawns is the shape most likely to find it. Auto-recall
  or a lifetime, decided when it exists.
- Whether harvesting a bloom should drop anything, or only selling one. As written, only selling.
