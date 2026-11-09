---
aliases:
  - "Seed, Plant, Produce — design"
  - "Seed, Plant, Produce"
  - "Bearing plants and produce"
tags:
  - crop
  - mechanic
---
# Seed, Plant, Produce — design

What a crop is: the seed a player carries, the plant that grows in a PlantSlot, the produce it gives, and the
numbers that balance them. Read [[principles]], [[networking]] and [[runtime-spawn]] first. Method names, not line
numbers.

---

## 1. The shape

**Status (2026-11-04):** built; both crop shapes tested in-world (2026-09-03), `StandingPlant` played in-world.

**Only `Seed` and `Produce` are ever grabbable.** A plant is spawned into a PlantSlot, grows there, and is
never touched by a player. That single decision is what makes the rest of this simple.

Three structures for crops, and a fourth for upgrades (2026-11-02):

```
Seed → RootedPlant                     → Produce          carrot, turnip
Seed → SingleHarvestPlant : BearingPlant → Produce         pumpkin, weed, corn
Seed → MultiHarvestPlant  : BearingPlant → Produce         tomato, eggplant
Seed → StandingPlant                                       popcorn, moonflower, spotlight tulip (upgrades; [[fun-seeds]])
```

```
Plant (abstract)
├── RootedPlant                    the plant is replaced by its produce
├── StandingPlant                  grows, then stands for the session; an upgrade, not a crop
└── BearingPlant (abstract)        produce hangs off the plant; the plant stays
    ├── SingleHarvestPlant         withers once every produce slot has been harvested
    └── MultiHarvestPlant          refills a harvested slot, for a season, then withers

Produce (abstract)
└── RootedProduce                  takes the plant's place, and its PlantSlot
```

These are behaviour classes, not data rows. `SeedDefinition` and its subclasses are the data axis
(*Seed definitions*, below).

## 2. What each class owns

**Status (2026-11-04):** built and tested in-world, except where a section says otherwise.

### `Seed`
Everything before the ground: shop stock, purchase request, being carried, and asking to be planted.
Implements `IHeldObject`, `IKinematicSource`, `IAuthoritySource` and `ILifecycleNotifier`, and carries the behaviour
components ([[entity-components]]). It does not grow and is not sold.

### `Plant` (abstract)
Spawned by the master into a `PlantSlot`, at the slot. Owns:

- `_plantedTimestamp` and the body's growth, derived per frame on every client — no scale is ever
  sent
- holding the slot for its whole life, and releasing it when it ends
- despawning itself when its life ends
- `protected abstract void OnFullyGrown()` — the one thing the two branches genuinely disagree about

No grab, no `NetworkGrabbable`, no `SingleHolderFilter`, no `ReturnableEntity`, `AlignableEntity`,
`HoveringEntity` or `KinematicController`. Six components that exist only for things a hand can
touch, and nothing can touch a plant.

### `RootedPlant`
On full growth: spawn a `RootedProduce` at its own transform, then despawn itself. The carrot *is*
the crop, so the plant does not linger next to it.

### `BearingPlant` (abstract)
Owns the produce sockets and the bearing itself:

```csharp
[SerializeField] private Transform[] _produceSockets;   // authored on the prefab
protected ProduceSlot[] _produceSlots;                  // built from the sockets at Awake
public void Bear();                                     // fill every empty, unspent slot with a Produce
protected abstract void OnProduceHarvested(ProduceSlot slot);
```

**The sockets are authored as a plain `Transform[]`** and the `ProduceSlot`s built from them at `Awake`: an array of
a custom `[Serializable]` class arrives empty from the bundle export, invisible in the Editor and only seen in-world.

A slot holds the produce's **`NetworkId`**, not a reference — the same `Id.Raw` addressing every
other cross-object message in this project uses. `Bear()` runs on the master, spawns with
`SharedModeStateAuthMasterClient`, and records the id in the slot.

The array is what makes the three cases one mechanism: a pumpkin has one socket, a tomato has
several, an apple tree has many.

### Seasons: `HarvestCount` and `harvestedCount`

**Status (2026-11-01):** built, compiles clean, untested in-world.

**A season is per plant, not per crop** (Vulps, 2026-11-01). The plant has a `HarvestCount`: how many
times each of its sockets fruits. Each socket (`ProduceSlot`) has a `harvestedCount`, raised by one on
every harvest. A socket is **spent** at `harvestedCount >= HarvestCount`; `Bear()` skips spent sockets,
and a plant whose sockets are all spent withers.

`HarvestCount` starts from the crop's `SeedDefinition.harvestCount` (default 1) but belongs to the plant,
because a plant's stats are meant to be rolled later (bigger plants worth more and harvested less often,
buffed or golden ones, possibly from the stall's rarity), so a season cannot be fixed at 3 per crop.
Rolling it is not built: today every plant takes its crop's value.

**Networking:** the master owns both counts. They travel in the plant's state with the slot ids:
`HarvestCount` (byte), the slot count (byte), then per slot the produce id (int) and `harvestedCount`
(byte), so late joiners get the season where it stands.

### `SingleHarvestPlant`
`OnProduceHarvested` — once every socket is spent, begin withering. With a `HarvestCount` of 1, which
pumpkin and weed have, that is once every socket has been harvested once.

### `MultiHarvestPlant`
`OnProduceHarvested` — if every socket is spent, wither; otherwise bear again after `_regrowDelay`
(30 s), refilling the empty sockets that still have harvests left. Tomato (4 sockets) and eggplant (3)
fruit 3 times a socket: a season, then the plant withers and frees its PlantSlot.

### `Produce` (abstract)
Owns its own ripening, on its own timestamp, as a plant's growth does:

- `_ripenTimestamp` + `ripenDuration` from `ProduceDefinition`; every client derives the scale
- **not grabbable until fully ripe**
- raises `Harvested` when a player takes it
- carries a `SellableEntity` (§2a) and answers it with `ISellValueSource`; it does not know what a
  sell point is

### `RootedProduce`
Appears in the ground where its plant stood, rather than hanging off a socket, and so it is the
thing that owns that PlantSlot until it is pulled. `ripenDuration` of 1 — it does its growing as a plant.

## 2a. `SellableEntity` — a trait, not an interface

**Status (2026-11-04):** built and tested in-world.

Selling is not a fact about being a crop. A produce is sellable; so, one day, might be a watering
can, a bucket of milk, or Excalibur. So it is a **component you add**, in the same family as
`KinematicController`, `SingleHolderFilter`, `ReturnableEntity`, `AlignableEntity` and
`HoveringEntity` ([[entity-components]]) — traits that attach to anything and know nothing about what they are
attached to.

It carries behaviour rather than only marking, for the same reason the others do: a marker interface would leave
the work in `SellPoint`, keyed by type.

**What it owns — the object's side of a sale:**

```csharp
public class SellableEntity : MonoBehaviour
{
    [SerializeField] private int _value;          // used when nothing else answers

    public int  SellValue  => _source != null ? _source.SellValue : _value;
    public bool CanBeSold  => enabled;            // disabled while unripe, growing, already sold
    public event System.Action Sold;
    public Transform GivenBackAt { get; }         // where a sale puts anything it gives back

    public void SetPending(bool pending);         // leave the world / come back if refused
    public void Sell(Transform givenBackAt = null); // accepted: raise Sold, then end
    protected virtual void OnSold();              // default: despawn
}
```

- **Value** comes from an optional one-member `ISellValueSource` on the same GameObject, falling back
  to a serialized number, as `KinematicController` asks `IKinematicSource` for the live answer rather than caching
  one. A `Produce` implements it from its definition; Excalibur would type a number into the Inspector.
- **`CanBeSold` is the component's own `enabled` flag.** An unripe produce has it off and turns it on
  when it ripens. Idle means idle: nothing to check, nothing to derive, one switch.
- **`SetPending`** is the hide-on-offer, reveal-on-refusal window that stops an item hanging in mid-air for the
  round trip to the master.
- **`OnSold()` defaults to despawning**, which is right for produce. It is virtual because a sword sold to a shop
  plausibly wants to be *kept by the shop*.

**No NetworkMessenger of its own.** `SellPoint` names the thing being sold by its `NetworkId` and announces
pending/rejected/accepted on its own messenger; the master takes state authority before completing, so it is the
master that calls `Sell(_dropPoint)` and the master that can despawn ([[economy#Selling|economy → Selling]]).

**`SellPoint` does not know what a crop is.** It takes `other.TryGetComponent(out SellableEntity sellable) &&
sellable.CanBeSold`; whether a crop is ready is the crop's business.

**Which prefabs are sellable is a balance decision**, not a rule in code: today only the seven `Produce_*` prefabs
carry the component, so seeds, deeds and scrolls are refused. If a bought seed ever gets one, its value must sit below
its buy price or the shop is a money printer.

## 3. Why these names, and not botanical ones

**Status (2026-11-04):** settled.

"Vining" is a **growth habit** and says nothing about lifespan — it spans annuals (pumpkin, pea) and
perennials (grape, kiwi) alike, so it cannot be a position in this hierarchy. Nor can the tempting
botanical labels: a real pumpkin vine bears several pumpkins across a season and dies at frost, so
"annual" does not mean "yields once"; "monocarpic" is closer but obscure;
"determinate/indeterminate" is the gardener's term for exactly this axis but reads as jargon.

**No botanical term matches the game's actual rule** — *after this plant yields, does it end or
continue?* — so the classes are named for the rule. `SingleHarvest`/`MultiHarvest` is a matched pair
on one axis: same noun, same question, opposite answers. "Regrowable" is not used: it describes the plant
regenerating, when what happens is the plant sits still and is harvested again. Issue #43 uses that older wording.

## 4. The plant's life, end to end

**Status (2026-11-04):** built and tested in-world.

1. A player carries a bought `Seed` into a `PlantSlot` trigger and the **holder** calls
   `RequestPlant(slot)` — only the holder knows its own hand is on the seed.
2. The **master** decides (`PlotStateManager`): is the slot free, is the seed bought, may the planter use this Plot
   ([[plot-ownership]])? Then it spawns the plant prefab at the slot with master authority, stamps
   `_plantedTimestamp`, marks the slot occupied, and announces.
3. The seed's own authority despawns the seed on hearing the announcement — it is the only client
   that can. It hides locally the moment the player lets go, so the hand never waits.
4. The plant grows. Every client derives the scale from the timestamp; nothing is sent.
5. On full growth: a `RootedPlant` spawns its produce and despawns itself; a `BearingPlant` calls
   `Bear()`; a `StandingPlant` stands.
6. Produce ripens on its own clock and becomes grabbable.
7. A player grabs it. Harvest is **requested by the holder and decided by the master** (§8), and the
   announcement is what marks it harvested everywhere.
8. `OnProduceHarvested` on the plant: wither, or bear again.
9. Produce is carried to the sell point, sold, despawned.

## 5. Produce is inert until it is picked

**Status (2026-11-04):** built and tested in-world.

`HoveringEntity` goes on the produce prefab but stays **off** until the produce has been harvested by
a grab. Before that it is a thing attached to a plant: it holds its position, ripens, and does
nothing else. Afterwards it is a loose object in the world and behaves like a dropped seed — falls,
lands, rises, bobs.

This is rule 2 ("idle means idle") applied at the right seam. It also means the socket never has to
fight a hovering body for position: the produce is not hovering yet.

Harvest is therefore a real state on the produce, not just an event:

```
ripening   → not grabbable, anchored, no hover
ripe       → grabbable, anchored, no hover
harvested  → grabbable, loose, hovering
```

## 6. Slot ownership

**Status (2026-11-04):** built and tested in-world.

The plant holds its PlantSlot for its whole life. Harvest takes only produce. The slot is freed when the
plant ends — after withering for either bearing plant, once its sockets are spent; never for a `StandingPlant`,
which stands until its Plot is cleaned up.

**A `RootedProduce` holds the slot until it is picked**: a `RootedPlant` hands its slot straight to the produce it
spawns (`PlotStateManager.SetOccupant`), so nobody can plant into a slot that visibly still holds a carrot. It is
the only produce that owns a slot.

The master spawns the plant *at* the slot it chose, so a seed whose collider overlaps two slot triggers cannot land
a plant in the wrong one; a seed is accepted once
([[plot-ownership#One seed, one plant|plot-ownership → One seed, one plant]]). Who may plant at all, and what
happens to a departed player's slots: [[plot-ownership]].

## 7. Open questions

**Status (2026-11-04):** open.

1. **An upgrade that stands for good** (the warehouse, [[warehouse]]) would want a way to take it out.
2. **Un-harvested produce when a plant withers.** A `SingleHarvestPlant` only withers *because* it
   was harvested, so this is currently unreachable — but a tomato with three of four slots taken and
   a player who wanders off holds its slot until its Plot is cleaned up.

## 8. Networking — read before writing any RPC

**Status (2026-11-04):** built and tested in-world. The general rules: [[networking]].

**State authority follows a grab** and is never evidence of anything: a player who last held a produce simulates it,
having decided nothing ([[networking#Three notions of ownership|networking → Three notions of ownership]]).

1. **Harvest is an explicit announcement, never inferred** from an authority transfer or any local state.
2. **The holder requests; the master decides.** `Produce.RequestHarvest()` → the master checks it is ripe and not
   already harvested → announced to all, and *every* client including the sender applies it. Never write lifecycle
   flags locally and rely on `broadcastState()` to carry them — it self-gates on state authority, and the decider
   usually is not the authority.
3. **Optimistic locally, authoritative eventually.** The fruit comes off in the hand the instant the
   player grabs; the master confirms ~200 ms later and only intervenes if the answer was no.

**Tiers:**

| Thing | Tier | Travels as |
|---|---|---|
| this plant has borne / this produce was harvested / what is planted where | **fact** | master decides, RPC to all |
| ripening progress, growth scale, current phase | **derivation** | not sent — derived from a timestamp |
| where a carried produce is this frame | **simulation** | Fusion transform replication |

`Seed`, `Plant` and `Produce` each have their own private message enum on their own `NetworkMessenger`
([[networking#Messaging|networking → Messaging]]).

## Seed models

**Status (2026-11-04):** built.

Carrot and turnip keep their foliage mesh as the seedling — a small leafy sprout reads correctly as
something you have just planted. **The pumpkin alone uses its fruit mesh as its seed**, because its
foliage is a full vine and always looked wrong at shop scale. A stripped-down young-vine mesh is the
real answer and is on the art todo, along with growth stages generally: a vine that grows and puts
out leaves, a tree that grows branches, sugar cane or bamboo with stages that can be harvested
independently. None of that changes the classes here — a plant body's appearance over time is the
plant's own business, and independently harvestable stages are a `MultiHarvestPlant` with more
sockets.

## Seed definitions

**Status (2026-09-13):** built and tested in-world. Code: `GrowAGarden/SeedDefinition.cs`, `GrowAGarden/Seeds/`.

`SeedDefinition` is an abstract `MonoBehaviour`, not a ScriptableObject. Its concrete subclasses, one per crop in
`Seeds/`, **hardcode** id, prices and durations in `Init()`, called from both `OnEnable` and
`OnValidate`. Balance changes are code changes, not Inspector edits.

**One definition per crop, referenced by all three of its prefabs** (seed, plant and produce), so there is one place
per crop to read and to change. The plant owns `growthDuration`, the produce owns `ripenDuration`, and a bearing
plant owns `witherDuration`.

A produce **must not** be handed the plant's definition component. A `RootedPlant` despawns itself the statement
after it bears, so the produce would spend its life pointing at a component on a destroyed GameObject. Unity's
overloaded null then makes every `!= null` read false, which is how a carrot came to sell for nothing while its
ripening still looked right by coincidence.

**To add a crop:** a `SeedDefinition` subclass (with a `tagline` for the stall sign, [[shop]]), three prefabs (copy an existing crop of the same shape and swap the
definition, models, colliders and the references between the three), all three registered on `SceneNetworking`, a
`Buyable_<Crop>` object under SceneManager carrying the definition, listed in `WorldManager._buyables`, and trip
twins of its materials on `TripManager` ([[trip]]). Nothing points a buy point at it: stalls roll from the buyables. **A crop still being worked on stays out of
`_buyables`** and is taken from the Developer Market instead ([[dev-tools]]); listing it is adding it to the list.
**The fruit's solid collider goes on the body child, not the root**: growth scales only `_bodyToScale`, so a root
collider stays at the mesh's full size while the fruit is drawn at `_maxScale`, and pushes things away before a hand
reaches it.
**Every grabbable pulls into the hand on a far grab** (Vulps, 2026-11-02): `XRGrabInteractable` Far Attach Mode is
**Near** on all 17 grabbable prefabs (seeds, produce, the scrolls and the splif). The default defers to Somnium's
interactor, which keeps a far-grabbed object at the end of the laser. A new grabbable needs the same.

## Balance

**Status (2026-11-04):** rebalanced, compiles clean, untested in-world. Sell values of bearing crops are per fruit.

Players carry two things at a time (an early-game limit: [[roadmap#Future — the portal seed|roadmap → portal seed]]),
so income is limited by trips, not slots or money: a crop is judged by its profit per thing carried (the seed in,
each fruit out). The pumpkin is pinned at every Garden and stays the reliable home crop at 7.5 a trip; everything
found on the islands earns more a trip, and more the rarer it is. Simulated: farming pumpkins at home about 3,000 an
hour, exploring 4,000–6,300 depending on trip length. An upgrade sells for nothing; grown, it adds its bonus to the
profit of everything its Plot grows ([[economy#Upgrade bonus|economy → Upgrade bonus]]).

| Crop | Buy / sell (Thatch) | Profit a plant (upgrade: its bonus) | Per trip | Timing | `spawnWeight` | Island stalls |
|---|---|---|---|---|---|---|
| Carrot | 10 / 15 | 5 | 2.5 | grows 10 s | 1.0 | 1 in 5 |
| Turnip | 18 / 26 | 8 | 4 | grows 15 s | 0.6 | 1 in 6 |
| Pumpkin | 60 / 75 | 15 | 7.5 | vine 60 s, fruit ripens 60 s, withers 30 s | 0.25 | 1 in 6 |
| Tomato | 20 / 22 a fruit, 4 sockets × 3 harvests | 244 | 19 | grows 20 s, ripens 15 s, regrows 30 s, withers 20 s | 0.5 | 1 in 5 |
| Corn | 30 / 55 a cob, 3 cobs | 135 | 34 | grows 20 s, ripens 15 s, withers 20 s | 0.2 | 1 in 7 |
| Eggplant | 90 / 50 a fruit, 3 sockets × 3 harvests | 360 | 36 | grows 40 s, ripens 25 s, regrows 30 s, withers 20 s | 0.08 | 1 in 12 |
| Weed | 200 / 140 a bloom, 9 blooms | 1,060 | 106 | grows 90 s, ripens 60 s, withers 30 s ([[weed]]) | 0.02 | 1 in 41 |
| Spotlight Tulip | 250 / nothing to sell | +7% on its Plot | – | grows 40 s, then stands for the session ([[spotlight-tulip]]) | 0.04 | 1 in 22 |
| Moonflower | 600 / nothing to sell | +30% on its Plot | – | grows 60 s, then stands for the session ([[moonflower]]) | 0.008 | 1 in 99 |
| Popcorn | 400 / nothing to sell | +5% on its Plot | – | grows 60 s, then stands for the session ([[popcorn]]) | 0.045 | 1 in 20 |

**Corn is a single-harvest bearing crop**: a 1.15 m stalk with three cobs, which withers once they are
picked. The pack's stalk has its three cobs built into the mesh, so they were cut out in the Editor (`Models/Corn/Corn_Plant_Bare.asset`, 456 of 1557 triangles
kept), and a `ProduceSocket` sits at each cob's centre along its axis. A cob is 13.5 cm, matching the modelled ones,
with a box collider that size.
**Tomato and eggplant regrow** (Vulps): `MultiHarvestPlant`s whose fruit hang from `ProduceSocket_N`
children placed around the stem, a season of 3 harvests each. A plant's visible size is its `_maxScale` (growth overwrites the body's scale), so the new crops' sizes
are set there: tomato, eggplant and corn plants 0.5, tomato fruit 0.5, eggplant fruit 0.6, corn cob 0.22.

## The eggplant easter egg

**Status (2026-11-01):** built (Vulps's idea), compiles clean; untested in-world.

A harvested eggplant works with SPS avatars: brought near an avatar's SPS target, it bends to meet it. It uses
Community Modules' SPS plugin (`Scripts/SPS Plugin/`), which every second finds objects tagged `Player` named
`SPS_Mesh…` (penetrators) and `SPS_Target…` (receivers, which SPS avatars carry), and gives each penetrator a trigger
that drives its material toward the nearest target. It is local rendering on every client, so nothing is sent.

- **`SPSController`** sits on SceneManager, pointed at the plugin's own `[SPS_Mesh]` and `[SPS_Target]` prefabs.
  CM's `SPS Controller` prefab was not used, because it carries a visible logo.
- **`Produce_Eggplant`**: under `Eggplant_Fruit`, an `SPS_Mesh` (tagged `Player`) holding `SPS_Renderer` (the
  eggplant mesh, material `Materials/Eggplant_SPS`), `SPS_Base` at the stem and `SPS_End` at the tip of the bulb
  (0.41 m apart in the mesh's units). The fruit's own renderer is off, so only the SPS copy shows.
- **The shader is ours.** The plugin only accepts materials whose shader is named `Community/SPS…`, and no such
  shader was in the project (SPS avatars bring their own). `Community/SPS Lit (Vulps)` (`Shaders/SpsLit.shader` +
  `SpsBend.hlsl`) is URP Lit with every vertex pass bending the mesh about `_Pivot`, from none at the base to a full
  turn toward `_Target` at the tip, while `_Enable` is set. If the official shader turns up, swapping it in is a
  material change.
- **`OwnMaterials`** on the renderer gives each eggplant its own material copy at spawn: `SPSTrigger` writes to the
  shared material, which would otherwise bend every eggplant in the world at once.
- Tagging `Player` is safe here: nothing of ours reads that tag, and Somnium's own player tag is `PlayerCollider`.
- **To test it without an SPS avatar**, take the SPS Receiver from the Developer Market ([[dev-tools]]): a ring carrying
  an `SPS_Target`.

## Growth pivots

**Status (2026-09-13):** open, designed, not built.

Everything scales about its mesh centre, so a pumpkin inflates through its vine and a carrot grows out of the soil as
much as into it. The fix is an empty `ScalePivot` parent between root and mesh, with the mesh offset so the stem or
soil line sits at the pivot's origin, and `_bodyToScale` pointed at the pivot. No code change: `_bodyToScale` is
just a transform.

## A plant positions itself from its plot

**Status (2026-09-13):** built and tested in-world.

Fusion's spawn pose is local to the spawner and not networked, so the Plot's `NetworkId` and the slot's index ride in
the state payload (`Plant.AdoptSlot`) and each client places the plant from them. A plant never moves, so paying for a `NetworkTransform` to replicate a
value that changes once would be absurd.
