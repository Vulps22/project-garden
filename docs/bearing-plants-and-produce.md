# Seed, Plant, Produce — design

Status: **implemented and confirmed in-world 2026-09-03.** Both shapes run the full loop: a rooted
crop is bought, planted, grows, becomes produce, is carried and sold; a bearing crop grows a vine,
bears into a socket, ripens, is harvested, withers, and frees its plot to be replanted. Outstanding:
growth pivots (§12) and the late-join question. Rewritten 2026-09-02, `SellableEntity` added 2026-09-03, against `feature/runtime-spawn`
@ 6c507a1, after runtime spawn removed the constraint the first version was written under.

Supersedes the pooled version of this document. What survives from it: the naming rationale (§3),
the networking rules (§8) and the wire-id trap (§9). What changed: everything that assumed one
pooled NetworkObject had to be a seed, a plant and a fruit by turns.

Read `CLAUDE.md` and `runtime-spawn.md` first. Method names, not line numbers.

---

## 1. The shape

**Only `Seed` and `Produce` are ever grabbable.** A plant is spawned into a plot, grows there, and is
never touched by a player. That single decision is what makes the rest of this simple.

Three structures, and they are expected to stay three:

```
Seed → RootedPlant                     → Produce          carrot, turnip
Seed → SingleHarvestPlant : BearingPlant → Produce         pumpkin, pea, tomato
Seed → MultiHarvestPlant  : BearingPlant → Produce         apple, grape, blueberry   (#43)
```

```
Plant (abstract)
├── RootedPlant                    the plant is replaced by its produce
└── BearingPlant (abstract)        produce hangs off the plant; the plant stays
    ├── SingleHarvestPlant         withers once every produce slot has been harvested
    └── MultiHarvestPlant          refills a harvested slot and goes on

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

### `SingleHarvestPlant`
`OnProduceHarvested` — when **every** slot has been harvested, begin withering. A pumpkin has one
socket so it withers on its first harvest; a tomato with four withers on the fourth.

### `MultiHarvestPlant`
`OnProduceHarvested` — refill that slot and carry on. Never withers on its own (see §7, open).

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
  go *behind the counter*, not out of existence.

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
plant ends — which is at once for a `RootedPlant`, after withering for a `SingleHarvestPlant`, and
never for a `MultiHarvestPlant`.

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

1. **Nothing ever ends a `MultiHarvestPlant`.** With plants ungrabbable there is no uproot gesture
   available at all, so an apple tree holds its plot until its owner leaves and the lease (§6a)
   expires. That is now an answer, but a thin one — a player who stays online can never reuse the
   plot. Needs a real one before #43 ships: a tool, a hold-to-remove on the plot, or a lifespan.
2. **Un-harvested produce when a plant withers.** A `SingleHarvestPlant` only withers *because* it
   was harvested, so this is currently unreachable — but a tomato with three of four slots taken and
   a player who wanders off holds its plot until the lease expires.
3. **Does `Bear()` refill instantly for a `MultiHarvestPlant`, or after a delay?** An apple tree that
   replaces an apple the instant it is picked reads as a vending machine. A regrow delay is probably
   wanted, and it belongs on the plant, not the produce.
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
`if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();`. A client that has just
gained authority knows *least* about the object, and with hover-transfer may have gained it by
accident.

**Spawned objects and RPCs.** A spawn reaches other clients a few ticks after the spawner, so an RPC
about it can arrive before the object does and be dropped. See `runtime-spawn.md` §4g and
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
that rather than being surprised by it. `MultiHarvestPlant` is written but no crop uses it, so it is
along for the ride, not under test.

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
