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

Status: **implemented and confirmed in-world 2026-09-03.** Both shapes run the full loop: a rooted
crop is bought, planted, grows, becomes produce, is carried and sold; a bearing crop grows a vine,
bears into a socket, ripens, is harvested, withers, and frees its plot to be replanted. Outstanding:
growth pivots (§12) and the late-join question. Rewritten 2026-09-02, `SellableEntity` added 2026-09-03, against `feature/runtime-spawn`
@ 6c507a1, after runtime spawn removed the constraint the first version was written under.

Supersedes the pooled version of this document. What survives from it: the naming rationale (§3),
the networking rules (§8) and the wire-id trap (§9). What changed: everything that assumed one
pooled NetworkObject had to be a seed, a plant and a fruit by turns.

Read [[principles]], [[networking]] and [[runtime-spawn]] first. Method names, not line numbers.

---

## 1. The shape

**Only `Seed` and `Produce` are ever grabbable.** A plant is spawned into a plot, grows there, and is
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
└── RootedProduce                  takes the plant's place, and its plot
```

These are behaviour classes, not data rows. `SeedDefinition` and its subclasses remain the data
axis, as they are today.

## 2. What each class owns

### `Seed`
Everything `PlantSeed` currently does *before* the ground: shop stock, purchase request, being
carried, and asking to be planted. Keeps `IHeldObject`, `IKinematicSource`, `IAuthoritySource`,
`ILifecycleNotifier` and the six behaviour components. Loses growth, phases, selling, and the whole
`IsSeed` pretence.

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
[SerializeField] private ProduceSlot[] _produceSlots;   // socket transform + current produce id
public void Bear();                                     // fill an empty slot with a Produce
protected abstract void OnProduceHarvested(ProduceSlot slot);
```

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
fruit 3 times a socket. **This is the ending #43 was missing:** a season, then the plant withers and frees
its plot.

### `Produce` (abstract)
Owns its own ripening, on its own timestamp, exactly as growth works today:

- `_ripenTimestamp` + `ripenDuration` from `ProduceDefinition`; every client derives the scale
- **not grabbable until fully ripe**
- raises `OnHarvested` when a player takes it
- carries a `SellableEntity` (§2a) and answers it with `ISellValueSource`; it does not know what a
  sell point is

### `RootedProduce`
Appears in the ground where its plant stood, rather than hanging off a socket, and so it is the
thing that owns that plot until it is pulled. `ripenDuration` of 1 — it does its growing as a plant.

## 2a. `SellableEntity` — a trait, not an interface

Selling is not a fact about being a crop. A produce is sellable; so, one day, might be a watering
can, a bucket of milk, or Excalibur. So it is a **component you add**, in the same family as
`KinematicController`, `SingleHolderFilter`, `ReturnableEntity`, `AlignableEntity` and
`HoveringEntity` — traits that attach to anything and know nothing about what they are attached to.
That family is already the answer to "behaviour that cuts across classes"; this is the sixth member,
not a new idea.

It carries behaviour rather than only marking, for the same reason the other five do: a marker
interface would leave the actual work in `SellPoint`, keyed by type, which is where it is now and is
what we are trying to stop.

**What it owns — the object's side of a sale:**

```csharp
public class SellableEntity : MonoBehaviour
{
    [SerializeField] private int _value;          // used when nothing else answers

    public int  SellValue  => _source != null ? _source.SellValue : _value;
    public bool CanBeSold  => enabled;            // disabled while unripe, growing, already sold
    public event System.Action Sold;

    public void SetPending(bool pending);         // leave the world / come back if refused
    public void Sell();                           // accepted: raise Sold, then end
    protected virtual void OnSold();              // default: despawn
}
```

- **Value** comes from an optional one-member `ISellValueSource` on the same GameObject, falling back
  to a serialized number. Exactly how `KinematicController` asks `IKinematicSource` for the live
  answer rather than caching one. A `Produce` implements it from its `ProduceDefinition`; Excalibur
  just types a number into the Inspector.
- **`CanBeSold` is the component's own `enabled` flag.** An unripe produce has it off and turns it on
  when it ripens. Idle means idle: nothing to check, nothing to derive, one switch.
- **`SetPending`** absorbs today's `PlantSeed.SalePending` — the hide-on-offer, reveal-on-refusal
  window that stops a plant hanging in mid-air for the round trip. That behaviour is not crop
  specific and should never have lived on `PlantSeed`.
- **`OnSold()` defaults to despawning**, which is right for produce and is your guess made the
  default rather than the rule. It is virtual because a sword you sell to a shop plausibly wants to
  be *kept by the shop*, not end.

**No NetworkBridge of its own.** `SellPoint` already names the thing being sold by its `NetworkId`
and announces pending/rejected/accepted on its own bridge; the master takes state authority before
completing, so it is the master that calls `Sell()` and the master that can despawn. Giving the trait
its own bridge would add a second conversation about one transaction.

**`SellPoint` stops knowing what a crop is.** `OnTriggerEnter` becomes
`other.TryGetComponent(out SellableEntity sellable) && sellable.CanBeSold`, and every
`plant.IsSeed` / `GetGrowthCompletion()` test in it disappears — those were the sell point deciding
whether a *crop* was ready, which is the crop's business.

**One balance consequence worth stating out loud:** whether a seed is sellable stops being a rule in
the code and becomes a decision about which prefabs get the component. `SellPoint` currently refuses
seeds outright. If a seed ever gets a `SellableEntity`, its value must sit below its buy price or the
shop is a money printer.

## 3. Why these names, and not botanical ones

Kept verbatim from the first version, because the reasoning caused two category errors during design
and will cause more.

"Vining" is a **growth habit** and says nothing about lifespan — it spans annuals (pumpkin, pea) and
perennials (grape, kiwi) alike, so it cannot be a position in this hierarchy. Nor can the tempting
botanical labels: a real pumpkin vine bears several pumpkins across a season and dies at frost, so
"annual" does not mean "yields once"; "monocarpic" is closer but obscure;
"determinate/indeterminate" is the gardener's term for exactly this axis but reads as jargon.

**No botanical term matches the game's actual rule** — *after this plant yields, does it end or
continue?* — so the classes are named for the rule. `SingleHarvest`/`MultiHarvest` is a matched pair
on one axis: same noun, same question, opposite answers. "Regrowable" was rejected because it
describes the plant regenerating, when what happens is the plant sits still and is harvested again.
**#43 and the deleted `RegrowableFruit.cs` stub use the older wording**; it is retired here.

## 4. The plant's life, end to end

1. A player carries a bought `Seed` into a `PlantSlot` trigger and the **holder** calls
   `RequestPlant(slot)` — only the holder knows its own hand is on the seed.
2. The **master** decides: is the slot free, is the seed bought? Then it spawns the plant prefab at
   the slot with master authority, stamps `_plantedTimestamp`, marks the slot occupied, and
   announces.
3. The seed's own authority despawns the seed on hearing the announcement — it is the only client
   that can. It hides locally the moment the player lets go, so the hand never waits.
4. The plant grows. Every client derives the scale from the timestamp; nothing is sent.
5. On full growth: a `RootedPlant` spawns its produce and despawns itself; a `BearingPlant` calls
   `Bear()`.
6. Produce ripens on its own clock and becomes grabbable.
7. A player grabs it. Harvest is **requested by the holder and decided by the master** (§8), and the
   announcement is what raises `OnHarvested` everywhere.
8. `OnProduceHarvested` on the plant: wither, or bear again.
9. Produce is carried to the sell point, sold, despawned.

## 5. Produce is inert until it is picked

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

The plant holds its slot for its whole life. Harvest takes only produce. The slot is freed when the
plant ends — which is at once for a `RootedPlant`, and after withering for either bearing plant, once its
sockets are spent.

`RootedProduce` is the exception worth thinking about, because the plant vanishes while the crop is
still sitting in the plot. **Recommend the produce holds the plot until it is picked**, otherwise a
player can plant a seed into a plot that visibly still contains a carrot. That makes `RootedProduce`'s
distinctness earn itself: it is the only produce that owns a slot.

This is also what kills **"plants land in the wrong slot"** outright. That bug is a seed's collider
overlapping two slot triggers and each client guessing differently. The master now spawns the plant
*at* the slot it chose; there is no trigger involved and nothing to guess.

## 6a. Ownership is a fact, not state authority

Gardens need owners: to stop a player planting in someone else's plot, to stop a dropped seed being
pocketed, and to clean up when someone leaves. Fusion's state authority is the obvious candidate and
is the wrong one.

**Why not authority.** Three reasons, in order of how much they cost:

1. **It gets theft backwards for the only things that can be stolen.** `NetworkGrabbable` calls
   `RequestControl()` on `firstHoverEntered`, so seeds and produce transfer authority to anyone whose
   hand comes *near* them. Under authority-as-ownership, reaching toward a dropped seed makes it
   yours without grabbing it. Plants are immune — they have no interactable at all — but plants are
   the one class nobody can touch anyway.
2. **Authority is already the master's action channel.** `SellPoint.TakeOwnershipAndComplete`,
   `ShopSlot.TakeOwnershipAndRecall` and `AuthorityController.Apply` all take authority as routine
   business. If authority meant ownership, each of those becomes the shop confiscating something, and
   the master cannot enforce anything without first seizing it.
3. **`DestroyWhenStateAuthorityLeaves` cannot wait.** It fires the instant authority departs, so a
   flaky connection costs a garden. The chosen behaviour is a grace period, which no Fusion flag
   expresses.

**What instead.** `OwnerId` — a Somnium player id, decided by the master, replicated as a fact.
Exactly the shape `IHeldObject.HolderId` already is, one level up: that was the same lesson, when
`isSelected` turned out to be an engine-local concept that could not answer "whose is this".

- **The plot owns, not the plant.** `PlantSlot` gains `OwnerId`. Anything standing in a plot is that
  player's by transitivity, so plants, vines and socket produce never need stamping. Ownership is
  checked once, at the moment the master decides a plant request.
- **Seeds carry their own `OwnerId`**, because they are the one thing that leaves the garden. A
  bought seed on the floor is still yours.
- **`RootedProduce` inherits the plot's owner**, since it holds the plot (§6) until it is pulled.

**Cleanup is a 120-second lease.** On `SomniumPlayersContainer.PlayerRemoved` the master starts a
timer; if the player has not come back when it expires, it frees their plots and despawns what stands
in them, along with their loose seeds. If they rejoin inside the window, the garden is exactly as
they left it. Long-term persistence across sessions is explicitly out of scope.

**One thing that is not part of the lease and must be immediate:** a departed player's `HolderId`.
That is not cleanup, it is a correction — they are demonstrably not holding anything. Leaving it set
makes `SingleHolderFilter` refuse that object to everyone for the rest of the session, which is a bug
that exists today, unrelated to any of this, and is fixed by the same `PlayerRemoved` handler.

## 7. Open questions

1. **Resolved (2026-11-01): seasons end a `MultiHarvestPlant`** (see *Seasons* in §2). An upgrade that
   should stand for good (the warehouse, [[roadmap]]) would still want a way to take it out.
2. **Un-harvested produce when a plant withers.** A `SingleHarvestPlant` only withers *because* it
   was harvested, so this is currently unreachable — but a tomato with three of four slots taken and
   a player who wanders off holds its plot until the lease expires.
3. **Resolved (2026-11-01): after a delay**, `MultiHarvestPlant._regrowDelay`, 30 s, on the plant.
4. **Several produce visible at once** is now the normal case, not a stretch goal — the socket array
   is in from the start.

## 8. Networking — read before writing any RPC

**Authority transfers on hover, not on grab.** `NetworkGrabbable.OnHover` calls `RequestControl()` as
the hand approaches. Two consequences:

1. **Authority is not evidence of anything.** A player who waves a hand near a ripe pumpkin owns it,
   having harvested nothing. Harvest **must** be an explicit announcement from the holder, never
   inferred from an authority transfer or any local state — the same conclusion the purchase flow
   reached about `isSelected`, from a different direction.
2. **The holder requests; the master decides.** Harvesting is in the **Facts** tier. Copy the
   purchase two-step exactly: `RequestHarvest()` on `Produce` → decided by the plant on the master →
   announced by `RPC_SendMessageToAll`, and *every* client including the sender applies it in the
   `Apply…` method. Never write lifecycle flags locally and rely on `broadcastState()` to carry
   them — it self-gates on `HasStateAuthority`, and the decider usually is not the authority.
3. **Optimistic locally, authoritative eventually.** The fruit comes off in the hand the instant the
   player grabs; the master confirms ~200 ms later and only intervenes if the answer was no.

**Tiers:**

| Thing | Tier | Travels as |
|---|---|---|
| this plant has borne / this produce was harvested / what is planted where | **fact** | master decides, RPC to all |
| ripening progress, growth scale, current phase | **derivation** | not sent — derived from a timestamp |
| where a carried produce is this frame | **simulation** | Fusion transform replication |

**`OnStateAuthorityChanged`** — copy `PlantSeed`'s corrected form:
`if (hasAuthority && PlayerManager.IsMaster) broadcastState();`. A client that has just gained
authority knows *least* about the object — it may hold it only because it was the last to grab
the thing, long after putting it down.

**Spawned objects and RPCs.** A spawn reaches other clients a few ticks after the spawner, so an RPC
about it can arrive before the object does and be dropped. See [[runtime-spawn]] §4g and
`ShopSlot.AnnounceStock`; whatever answer that probe produces applies to `Bear()` too.

## 9. Wire-id trap

`PlantMessageType` is currently:

```
enable 0 · disable 1 · sold 2 (retired) · stateSync 3 · grabber 4 · vineAnchor 5 · vineDecayStart 6
purchaseRequest 7 · purchased 8 · restoredToShop 9
```

Splitting the class is the moment to be careful: `Seed`, `Plant` and `Produce` each get their **own**
private enum, and message ids are scoped per NetworkBridge, so each may start at 0 without clashing.
Do not carry `PlantMessageType` across all three — that is how a hole in one becomes a hole in all.

`BytesWriter` is pre-sized; any payload change needs its size calculation updated, including
`GetExtraBroadcastStateSize()`.

## 10. What deletes

- `ViningPlantSeed` entirely — the vine pinning, `_vineAnchoredWorldPos/_Rot`, `_vineDetachRadius`,
  `AnchorVine()`, the `vineAnchor` send and its state payload. The fruit travelling while the vine
  stays put was the whole reason for the per-frame `LateUpdate` fight, and Produce being its own
  object removes it. If the file is renamed rather than replaced, **keep its `.meta` GUID** — Unity
  binds a MonoBehaviour to its script by that GUID, and minting a new one shows "missing script" on
  every prefab referencing it. Duplicate GUIDs under `Assets/#User/` are currently 0; keep it there.
- `RootedPlantSeed`, `PlantSeed.IsSeed`, `SetState(bool)`, `UpdateVisuals(bool)` and the `isSeed`
  byte from the state payload
- `RegrowableFruit.cs` — subsumed by `Produce`, and its own comment ("I don't know if we need this
  yet") is now answered
- `_fruitModel` / `_fruitCollider` from the plant prefabs
- `PlantSeed.SalePending`, `SetSalePending()` and the hiding they drive — they move wholesale into
  `SellableEntity` (§2a), where anything can have them
- **`ISellable` never gets written** — see §2a. It was an interface because `SellPoint` had to accept
  two unrelated classes. A trait component does that better and does not need the class to admit it

## 11. Landing it

Decided to land as **one change, not four**. The split cannot be half-done — the moment `PlantSeed`
stops being both a seed and a plant, every prefab, every message enum and every call site moves at
once, and an intermediate state would be more code than either end.

The cost is worth stating: this is the first upload on this branch whose in-world test cannot isolate
a failure. If the shop stocks but nothing grows, the suspects are the plant prefab, the plant spawn,
the slot handoff, the growth derivation and the message enums, all new in the same build. Budget for
that rather than being surprised by it. `MultiHarvestPlant` was written but unused then; tomato and eggplant
use it since 2026-11-01.

The in-world test that matters: **buy → plant → grow → harvest → sell**, for carrot (rooted) and
pumpkin (bearing), with the plot replantable afterwards. Then a second client, for the late-join
question `ShopSlot.AnnounceStock` still has open.

## 12. Seed models

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

`SeedDefinition` is an abstract `MonoBehaviour`, not a ScriptableObject. Its concrete subclasses (`CarrotSeed`,
`TurnipSeed`, `PumpkinSeed`) **hardcode** id, prices and durations in `Init()`, called from both `OnEnable` and
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
twins of its materials on `TripManager` ([[weed]]). Nothing points a buy point at it: stalls roll from the buyables. **A crop still being worked on stays out of
`_buyables`** and is taken from the Developer Market instead ([[dev-tools]]); listing it is adding it to the list.
**The fruit's solid collider goes on the body child, not the root** (2026-11-02): growth scales only `_bodyToScale`,
so a root collider stays at the mesh's full size while the fruit is drawn at `_maxScale`. Carrot, turnip, tomato and
eggplant had colliders twice the size of the fruit, which pushed things away before a hand reached them.
**Every grabbable pulls into the hand on a far grab** (Vulps, 2026-11-02): `XRGrabInteractable` Far Attach Mode is
**Near** on all 17 grabbable prefabs (seeds, produce, the scrolls and the splif). The default defers to Somnium's
interactor, which kept a far-grabbed object at the end of the laser, and players were reeling seeds in hand over hand.
A new grabbable needs the same.

## Balance

**Status (2026-11-01):** current values; tuned for testing, not designed. Tomato, corn and eggplant added
2026-11-01 from the same asset pack as carrot, turnip and pumpkin (compiles clean, untested in-world). Sell
values of bearing crops are per fruit.

| Crop | Buy / sell (Thatch) | Timing | `spawnWeight` |
|---|---|---|---|
| Carrot | 10 / 15 | grows 10 s | 1.0 |
| Tomato | 20 / 6 a fruit, 4 sockets × 3 harvests | grows 20 s, ripens 15 s, regrows 30 s, withers 20 s | 0.85 |
| Turnip | 18 / 25 | grows 15 s | 0.6 |
| Corn | 25 / 13 a cob, 3 cobs | grows 20 s, ripens 15 s, withers 20 s | 0.45 |
| Eggplant | 45 / 14 a fruit, 3 sockets × 3 harvests | grows 40 s, ripens 25 s, regrows 30 s, withers 20 s | 0.3 |
| Pumpkin | 60 / 110 | vine 60 s, fruit ripens 60 s, withers 30 s | 0.25 |
| Popcorn | 120 / nothing to sell | grows 60 s, then stands for the session ([[fun-seeds]]); not in the shops yet | 0.12 |
| Moonflower | 150 / nothing to sell | grows 60 s, then stands for the session ([[fun-seeds]]); not in the shops yet | 0.1 |
| Spotlight Tulip | 60 / nothing to sell | grows 40 s, then stands for the session ([[fun-seeds]]); not in the shops yet | 0.2 |
| Weed | 80 / 30 a bloom, 9 blooms | grows 90 s, ripens 60 s, withers 30 s ([[weed]]) | 0.15 |

**Corn is a single-harvest bearing crop** (2026-11-02): a 1.15 m stalk with three cobs, which withers once they are
picked. It started as a carrot copy and came up as a cob half-buried in the dirt. The pack's stalk has its three cobs
built into the mesh, so they were cut out in the Editor (`Models/Corn/Corn_Plant_Bare.asset`, 456 of 1557 triangles
kept), and a `ProduceSocket` sits at each cob's centre along its axis. A cob is 13.5 cm, matching the modelled ones,
with a box collider that size.
**Tomato and eggplant regrow** (Vulps, 2026-11-01): `MultiHarvestPlant`s whose fruit hang from `ProduceSocket_N`
children placed around the stem, a season of 3 harvests each. Their prefabs started as carrot copies and were
converted. A plant's visible size is its `_maxScale` (growth overwrites the body's scale), so the new crops' sizes
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

Fusion's spawn pose is local to the spawner and not networked, so the plot's `NetworkId` rides in the state payload
and each client places the plant from it. A plant never moves, so paying for a `NetworkTransform` to replicate a
value that changes once would be absurd.
