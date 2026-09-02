# Bearing plants and Produce — design

Status: **design only, nothing implemented.**

Written 2026-09-02 against `feature/purchase-authority` @ 09b0a7c **plus the uncommitted working tree
at the time** — another session was mid-flight on `PlantSeed`, `ViningPlantSeed`, `ShopSlot`,
`NetworkGrabbable` and `unified_pumpkin.prefab`, and several of those changes shaped this design.
Read `CLAUDE.md` first; this assumes its Architecture section.

Deliberately references **method names, not line numbers** — these files are churning.

Goal: split a bearing plant into two pooled NetworkObjects — the **plant**, which stays in its slot,
and the **Produce**, which is carried away and sold.

---

## 1. Why — one object pretending to be two

`unified_pumpkin` is a single NetworkObject holding both the vine (`_SeedModel`) and the fruit
(`_fruitModel`) under one root, and the root is what the player grabs. To let the fruit travel while
the vine stays put, `ViningPlantSeed.LateUpdate` pins the vine's *world* transform back to
`_vineAnchoredWorldPos` every frame, cancelling out the root's motion.

That one decision causes four symptoms that look unrelated:

| Symptom | Why it follows |
|---|---|
| The pumpkin "seed" is an oversized vine | `_SeedModel` is overloaded as both shop item and vine, so there is nowhere to put a seed mesh |
| `HasLeftSlot()` swallows real exits | `Pumpkin_Fruit`'s collider sits 24.6 cm off the root; trigger events arrive from a child whose position is not the root being tested |
| A vine renders in a plot the slot already freed | `PlantSeed.OnTriggerExit` frees the slot when the root leaves, but the vine decays for 30 s after — so a new seed can be planted into a plot that still visually holds a vine |
| #43 multi-harvest plants are impossible | Plant and fruit are the same pooled instance, so the plant can never stay while the fruit is sold |

A fifth — decay state riding into the pool, leaving the next claim of that instance invisible — was
**fixed on 2026-09-02** by the new `PlantSeed.OnReturnedToPool()` hook and `ViningPlantSeed`'s override
of it. That hook is worth knowing about: it is the sanctioned place to reset anything a subclass
carries that outlives one life, and `BearingPlantSeed` will need it for its produce reference.

Rule 2 ("idle means idle") is violated *by construction* here — the vine writes its transform every
frame for as long as it is anchored. The split removes that rather than guarding it.

## 2. The hierarchy — two orthogonal axes

Bearing plants differ along **two axes that must not be conflated**:

```
form    vine / bush / tree / rooted     how the body renders and scales
yield   once / repeatedly               what happens after harvest
```

**Yield is the class axis; form is data.**

```
PlantSeed (abstract)
├── RootedPlantSeed               the plant IS the product      carrot, turnip
└── BearingPlantSeed (abstract)   produce leaves; plant stays
    ├── SingleHarvestPlantSeed    yields once, decays, frees slot    pumpkin, pea
    └── MultiHarvestPlantSeed     yields repeatedly, holds slot      apple, grape, blueberry  (#43)
```

`OnProduceHarvested()` is the only thing the two subclasses disagree about — decay, or bear again.

### Why these names are not botanical

"Vining" is a **growth habit** — trailing or climbing stems — and says nothing about lifespan.
Vining spans annuals (pumpkin, pea, cucumber) and perennials (grape, hops, kiwi) alike, so it cannot
be a position in this hierarchy.

The tempting botanical labels are all wrong too. A real pumpkin vine bears *several* pumpkins across
a season and only then dies at frost, so "annual" does not mean "yields once"; "monocarpic" (fruits
once, then dies) is closer but obscure; "determinate/indeterminate" is the gardener's term for this
exact axis but reads as jargon. **No botanical term matches the game's actual rule** — *after this
plant yields, does it end or continue?* — so the classes are named for the rule instead. This has
already caused two category errors during design; do not reintroduce plant taxonomy into type names.

`SingleHarvest` / `MultiHarvest` is deliberately a **matched pair on one axis** — same noun, same
question, opposite answers. "Regrowable" was rejected for the same reason the botanical names were:
it describes the *plant* regenerating, when what actually happens is the plant sits still and is
harvested again. Note that **#43 and the deleted `RegrowableFruit.cs` stub use the older
"regrowable" wording**; that vocabulary is retired here, and the issue title is worth updating when
#43 is picked up.

### `ViningPlantSeed` dissolves

It is **deleted, not reparented.** Once the Produce split removes the pinning, the anchor and the
detach radius, what remains is "scale this mesh according to phase" — which is what
`PlantSeed.OnGrowthUpdated` already does by default, and what `RootedPlantSeed` varies by show/hide.
Form becomes a `[SerializeField] Transform _bodyToScale` on `BearingPlantSeed`. A vine and an apple
tree then run identical code with different prefabs.

**Unity binds a MonoBehaviour to its script by the `.meta` GUID.** To keep `unified_pumpkin`'s
component reference alive, *rename* `ViningPlantSeed.cs` to the new class name and keep its existing
`.meta` file — the class name must match the filename, but the GUID must not change. Deleting and
recreating the file mints a new GUID and every prefab referencing it shows "missing script". The repo
currently has zero duplicate GUIDs under `Assets/#User/`; keep it that way.

`BearingPlantSeed` owns:
- `_produceId`, the `_produceSocket` transform, and `_bodyToScale`
- claiming a `Produce` at bear time, and retrying when the pool is exhausted
- the current produce's network id (identified by `Id.Raw`, as `UnifiedPool.OnReturnRequested` does)
- `protected abstract void OnProduceHarvested()`
- clearing that produce reference in `OnReturnedToPool()`

## 3. Slot ownership inverts

Today the plant leaves and frees the slot. In the new model **the plant never leaves** — it holds the
slot for its whole life, harvest takes only the Produce, and the slot is freed when the plant itself
ends. This is what makes #43 a subclass override rather than a redesign.

`PlantSeed.OnTriggerExit` is now authority-gated and checks `_occupiedSlot`, so it is already much
safer than it was; phase D still has to stop it being the thing that frees the slot for bearing plants.

## 4. New types

### `Produce` — a new lean class, NOT a `PlantSeed` subclass

It has no seed state, no shop, no planting and no growth phases; most of `PlantSeed` is meaningless
to it.

It gets all six behaviour components **for free** by implementing the existing interfaces —
`KinematicController`, `AuthorityController`, `SingleHolderFilter`, `ReturnableEntity`,
`AlignableEntity` and `HoveringEntity` know nothing about seeds, only about `IHeldObject`,
`IKinematicSource`, `IAuthoritySource` and `ILifecycleNotifier`. This is the #45/#46 dependency
inversion paying for itself; do not weaken it by giving `Produce` a seed-shaped API.

`NetworkGrabbable` also now resolves `IHeldObject` rather than anything seed-specific, so it works on
a `Produce` unchanged.

It defines its own private `enum ProduceMessageType : byte`. Message ids are scoped per NetworkBridge,
so starting at 0 does not clash with `PlantMessageType`.

### `ProducePool` / `ProduceDefinition`

Siblings of `UnifiedPool` / `SeedDefinition`, per-crop, matching the existing pool-per-id shape.
`ProduceDefinition` carries `produceId`, `displayName`, `sellValue`, `ripenDuration`.

### `ISellable`

`SellPoint.OnTriggerEnter` does `GetComponent<PlantSeed>()` and reads `plant.seedDefinition.sellValue`;
a `Produce` is not a `PlantSeed`. Extract:

```csharp
public interface ISellable
{
    PlayerBalance GetGrabber();
    int  SellValue { get; }
    void Sell();
}
```

Same one-member-interface pattern the codebase already uses. Carrot and turnip keep working unchanged.

## 4a. Produce has no subtypes

One concrete `Produce` class. Strawberry, grape, apple, raspberry, blueberry, pea — all ripen, are
grabbable, are carried and are sold. Every difference between them is a mesh, a `sellValue` or a
`ripenDuration`, and those are `ProduceDefinition` data.

**The rule: a subtype earns its place when there is a virtual method to override.** This is the split
the codebase already makes — `CarrotSeed`/`TurnipSeed`/`PumpkinSeed` are `SeedDefinition` *data*;
`RootedPlantSeed`/`BearingPlantSeed` are *behaviour*.

Botanical classification is not a behavioural axis and must not be allowed to become one. Blueberries
and grapes are true berries; raspberries are aggregate fruits; strawberries and apples are accessory
fruits; a pea is a legume, where the pod is the fruit and the peas are its seeds. All six behave
identically in game.

**Pods, if ever wanted.** Requiring peas to be shelled is the one idea in this space that is a real
behaviour difference — and it is still not a `Produce` subtype. A pod is *produce that bears produce*:
the same bearing relationship one level down, reached for through `IBearer`, not through a
`PodProduce` class. Nothing here precludes it. Build nothing for it now. It is also more a playtest
question than an architecture one.

## 5. Ripening belongs to the Produce

Rule 1 — one owner per shared property. The plant's phases describe the *plant body* only; the fruit's
ripening is the fruit's own business, driven from its own `_ripenTimestamp` exactly as `PlantSeed`
derives growth. So the plant does not drive the fruit's scale, and syncing one timestamp syncs it.

Pumpkin rebalances as:

| | before | after |
|---|---|---|
| `PumpkinSeed.phases` | vine 60 s, fruit 60 s, decay 30 s | vine 60 s, decay 30 s |
| `PumpkinProduce` | — | `ripenDuration` 60 s, `sellValue` 110 |

`sellValue` moves to `ProduceDefinition` for bearing plants; rooted plants keep it on `SeedDefinition`.
`ISellable.SellValue` is what lets `SellPoint` stop caring which.

## 6. The fruit is positioned once, never driven

The plant is rooted in a slot and never moves, so at bear time the authority sets the Produce's
position at `_produceSocket` and **never writes it again**. Rule 2 is satisfied by construction rather
than by the per-frame `LateUpdate` fight it replaces.

Do not parent the Produce to the socket — networked reparenting under Fusion is fiddly and buys
nothing here, because the thing it would follow does not move.

## 7. Networking — read this before writing any RPC

Derived from `broadcaststate-needs-authority-decisions-dont` and
`local-state-must-not-drive-networked-decisions`, and from the 2026-09-02 fixes that came out of them.

**Authority now transfers on hover, not on grab.** `NetworkGrabbable.OnHover` calls `RequestControl()`
as the hand approaches, to spend the ~200 ms round trip during the reach instead of after the grab.
Two consequences for this design, and the first is the important one:

1. **Authority is not evidence of anything.** A player who merely waves a hand near a ripe pumpkin
   owns it, having harvested nothing. So harvest **must** be an explicit announcement from the holder,
   never inferred from an authority transfer or from any local state. This is the same conclusion the
   purchase flow reached about `isSelected`, arrived at from a different direction.

2. **The holder *requests*; the master *decides*.** CLAUDE.md's governing rule puts harvesting in the
   **Facts** tier — "planting, harvesting and pooling are facts the master decides, not events it
   learns about". But only the holder knows its own hand closed on the fruit. So it is the two-step
   the purchase flow already uses: `RequestPurchase()` → `PlantSeed.PurchaseRequested` →
   `ShopSlot.OnPurchaseRequested` decides on the master. Copy that shape exactly — a
   `RequestHarvest()` on `Produce`, decided by `BearingPlantSeed` on the master.

   Both halves travel by `RPC_SendMessageToAll`, which is `RpcSources.All` — any client may send it
   regardless of Fusion authority. The template for the *announcement* half is `PlantSeed.Purchase()`
   → `PlantMessageType.purchased` → `ApplyPurchase()`: the decider sends, and *every* client
   including the sender applies the change in the `Apply…` method. Never write lifecycle flags
   locally and rely on `broadcastState()` to carry them — it self-gates on `HasStateAuthority`, and
   the decider usually is not the authority.

3. **Optimistic locally, authoritative eventually.** The fruit comes off in the hand the instant the
   player grabs it; the master confirms ~200 ms later and only intervenes if the answer was no. A
   decision may be slow; a hand must not be. Making the player wait for the master is exactly the
   flicker the hover-authority change removed.

### Which tier is what

| Thing | Tier | So it travels as |
|---|---|---|
| this plant has borne / this produce was harvested | **fact** | master decides, RPC to all |
| ripening progress, current scale | **derivation** | not sent — every client computes it from the ripen timestamp |
| where a carried fruit is this frame | **simulation** | Fusion transform replication |

Reach for **derivation** first. Syncing *"started ripening at T"* once means every client computes the
same scale every frame forever, with no further messages — and a phase transition becomes the clock
crossing a line everyone can already see, not an event needing an RPC.

**`OnStateAuthorityChanged` — copy the corrected form.** `PlantSeed`'s handler is now
`if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();`. The master gate is the whole
point: a client that has just gained authority is the client that knows *least* about the object, and
with hover-transfer it may have gained it by accident. A `Produce` copying the old ungated version
would have a passing player assert a stale "unharvested, still at the socket" copy over everyone.

**Pool exhaustion.** `ProducePool.Claim()` returns null when empty; the plant retries each frame from
the authority, as `ShopSlot.Update()` and `UnifiedPool._pendingRestore` already do. A `MultiHarvestPlantSeed` must
not bear again while its previous Produce is still unharvested.

## 8. Wire-id trap

`PlantMessageType` is currently:

```
enable 0 · disable 1 · sold 2 · stateSync 3 · grabber 4 · vineAnchor 5 · vineDecayStart 6
purchaseRequest 7 · purchased 8 · restoredToShop 9
```

`vineAnchor` and `vineDecayStart` become unused, but they **must stay as reserved holes**. Deleting
them shifts everything from `purchaseRequest` down by two, and two builds would then disagree about
what a message means. Leave them in place, commented as retired. Append only.

`BytesWriter` is pre-sized — any payload change needs its size calculation updated too, including
`GetExtraBroadcastStateSize()`.

## 9. What deletes

- **`ViningPlantSeed` in its entirety** — renamed into `SingleHarvestPlantSeed` keeping its `.meta` GUID
  (see §2). `AnchorVine()`, `LateUpdate()` pinning, `_vineAnchoredWorldPos/_Rot`, `_vineDetachRadius`,
  the `vineAnchor` send and its state payload all go with it
- `RegrowableFruit.cs` — subsumed by `Produce`. Nothing about a fruit depends on whether the plant
  that bore it yields again, and its own comment ("I don't know if we need this yet") is now
  answered: you don't
- `_fruitModel` / `_fruitCollider` from the plant prefab

Decay now starts on *harvest*, not on the root travelling `_vineDetachRadius` from an anchor.

## 10. Phasing — one per upload

| | Change | What the in-world test proves |
|---|---|---|
| **A** | `ISellable`; `SellPoint` accepts it | Nothing regressed — carrot and turnip still sell |
| **B** | `Produce`, `ProducePool`, `ProduceDefinition`, fruit prefab + pool in scene. Not wired to any plant; place one by hand | Grab, carry, hover, sell and pool-return on a standalone fruit |
| **C** | `BearingPlantSeed` + `SingleHarvestPlantSeed`; dissolve `ViningPlantSeed` into them; delete the vine pinning. Give the pumpkin a real seed mesh while the prefab is open | The full bear → harvest → sell loop |
| **D** | Slot-ownership inversion; decay completion frees the slot | Replanting the same plot; sets up #43 |

The pumpkin seed mesh rides in C because C already has the prefab open, and it independently fixes the
`HasLeftSlot()` collider-offset bug.

Multi-client behaviour is only observable with several real clients connected — in particular the
hover-transfer race, where two players reach for the same ripe fruit.

## 11. Open questions

1. **Uproot.** Can a player pull a planted crop? Not needed while everything decays out, but a
   multi-harvest plant otherwise holds its plot forever. Likely wanted before #43 ships.
2. **Multi-harvest slot release.** If there is no uproot, what ever ends a `MultiHarvestPlantSeed`?
   Nothing, currently — it would hold its plot for the lifetime of the world.
3. **Does the plant stay grabbable at all?** Post-split the Produce is what players take. A grabbable
   plant is either an uproot interaction or a bug.
4. **Can one plant bear more than one Produce at once?** More likely to bite than anything else here:
   an apple tree bearing a single apple looks wrong. Recommend shipping one socket first — widening
   `_produceSocket` to `_produceSockets[]` and the produce id to a list is mechanical, and nothing
   about `Produce` itself changes. Note that one Produce need not mean one fruit: a bunch of grapes or
   a punnet of berries is one carryable, which is a mesh decision, not a code one.
5. **Sale is still observed, not accepted.** `SellPoint.OnTriggerEnter` watches a trigger on the
   master rather than being asked. CLAUDE.md's governing rule calls this out as not yet honoured;
   phase A touches this file, so it is the cheap moment to fix it.

## 12. Superseded by work already done

Two entries under `CLAUDE.md` → "Known open problems" were closed on 2026-09-02 while this was being
written, and should not be re-diagnosed:

- *"Nothing clears a shop return target when a seed is planted"* — `PlantSeed.ClearShopClaim()` now
  runs from `Plant()`, `ApplyPurchase()`, `ReturnToPool()` and the `disable` RPC.
- *"`unified_pumpkin.prefab` has no `NetworkGrabbable`"* — it does now.
