# Runtime spawn — what it changes

Status: **design only, nothing implemented.** Written 2026-09-02 against `main` @ 53abd4f, tree clean.

Companion to `bearing-plants-and-produce.md`, which was designed against the pooled world and is
**revised by this** (see §7). Read `CLAUDE.md` first; this assumes its Architecture section.

Method names, not line numbers.

---

## 1. What was discovered, and what is actually true

`SceneNetworking` can register prefabs into Fusion's global `PrefabTable` at runner setup and hand
back a `NetworkPrefabId`, so anything can be spawned at runtime. The list is `_networkPrefabs: []`
in the scene — the capability was wired up by the SDK and has never been used here. CLAUDE.md's
"nothing is instantiated at runtime" was a belief about this project, not a constraint on it.

Verified from the Fusion 2 assembly docs and the prefab/scene assets, not assumed:

| Fact | Where it came from | Why it matters |
|---|---|---|
| `Runner.Spawn(NetworkPrefabId, pos?, rot?, inputAuthority?, onBeforeSpawned, flags)` | `Fusion.Runtime.xml` | The spawn call itself, plus a pre-replication init hook |
| `NetworkSpawnFlags.SharedModeStateAuthMasterClient` | same | **The master can be made the state authority of a spawn regardless of who called it** |
| `Runner.Despawn(no)` checks state authority and silently does nothing without it | same | Despawn is a decision only the owner can execute |
| Spawn position/rotation are **local to the instantiating client** and not networked | same | The real transform arrives via `NetworkRigidbody3D`; all three unified prefabs have one |
| Prefabs already carry `Flags: 524289` = `V1 \| AllowStateAuthorityOverride` | `prefabs_unified/*.prefab` | `RequestStateAuthority()` works on spawned instances exactly as on scene ones |
| Prefab ids come from a GUID list serialized in the scene, registered per client at `NetworkRunnerSetup` | `SceneNetworking.RegisterNetworkPrefabs` / `PrepareNetworkPrefabs` | Ids agree across clients as long as everyone runs the same build — no handshake needed |

Reference implementation: `Community Modules/.../Networked Components/NetworkSpawn.cs`.

**The `SharedModeStateAuthMasterClient` flag is the headline, not the spawn.** Half this project's
multiplayer bugs are the gap between *deciding* something and *owning* the object it is about —
`broadcastState()` self-gates on `HasStateAuthority`, so the master's writes went nowhere whenever the
buyer owned the seed. A master-authored spawn closes that gap by construction: at the instant the
master decides an object should exist, it owns it, and its broadcast lands.

## 2. What this deletes

Pooling exists only because a Fusion NetworkObject had to be pre-placed in the scene. Nothing else
wanted it.

- `UnifiedPool.cs`, `PoolManager.cs`, `PoolMessageType`, `_pendingRestore`,
  `RequestAuthorityAndReturn` and its 20 s timeout
- `PlantSeed.IsInPool`, `HideForPool()`, and the pool term in `ShouldBeKinematic`,
  `ShouldUseGravity`, `ShouldMasterOwn`, `SetState()` and `OnMessageToProxies`
- **153 scene NetworkObjects** (102 `RootedPlantSeed`, 51 `ViningPlantSeed`) and the `SortKey`
  ordering fragility that comes with registering them
- `ShopSlot.Update()`'s retry, and the paragraph of comment explaining that the join-time
  `SpawnSeed()` fires before Fusion has spawned the scene objects. There are no scene objects to
  wait for; a spawn either happens or errors.
- An exhausted pool returning `null`, and every caller that has to handle it

And one whole class of bug: **a peer picking a seed up was the only thing that ever handed it
authority, which is what made `UnifiedPool`'s retry teleport shop stock into the pool on first
contact.** That failure mode needs no guard because it has no mechanism.

## 3. The rules it satisfies structurally

CLAUDE.md's governing rule and its three corollaries stop being things the code must remember:

| Rule | Today | With spawn |
|---|---|---|
| "an object no player is holding should be owned by the master" | `IAuthoritySource.ShouldMasterOwn` + `AuthorityController` asking, and sometimes losing | the object is *born* master-owned via `SharedModeStateAuthMasterClient` |
| "facts are decided by the master and announced" | the master decides, then discovers it cannot broadcast | decision and ownership coincide at the spawn |
| "one owner per shared property" | `IsSeed` is one object pretending to be two | seed and plant are different objects (§5) |

`AuthorityController` does **not** go away — a seed that has been carried and dropped still has to
come home. It just stops being the mechanism that makes stock work in the first place.

## 4. New risks, and the one real regression

**a. Orphan reclaim does not cover spawned objects — but almost nothing needs it.**
`SceneNetworking.ReassignNullObjectsAuthority` iterates `_sceneNetworkObjects`, a
`FindObjectsByType` snapshot taken in `Awake`. Both callers — the 1 s `SlowLoop` and `OnPlayerLeft`
— therefore skip anything spawned later.

An earlier draft of this section called that a regression to fix inside phase A. Checked properly,
it is not, and the checking is worth writing down:

- **A spawned object is not destroyed when its owner leaves.** That only happens under
  `NetworkObjectFlags.DestroyWhenStateAuthorityLeaves` (262144). The unified prefabs are `524289` =
  `V1 | AllowStateAuthorityOverride`. So the object survives, owned by nobody.
- **Almost nothing in this project needs an owner.** Every `HasStateAuthority` gate in the codebase
  is on the seed/plant itself — growth in `PlantSeed.Update`, `broadcastState`,
  `KinematicController`, `ReturnableEntity`, `AlignableEntity`, `HoveringEntity`,
  `NetworkGrabbable`, and the two take-ownership coroutines. `EconomyManager`, `PlantSlot`,
  `SellPoint` and `BalanceDisplayManager` all speak over `RPC_SendMessageToAll`
  (`RpcSources.All, RpcTargets.All`) and gate on `IsMasterClient`, never on authority. The one RPC
  that genuinely requires an owner, `RPC_SendMessageToController` (`RpcTargets.StateAuthority`), has
  exactly one subscriber in the project: `UnifiedPool` — which phase B deletes.
- **So the answer to "what needs transferring to a new master?" is: after phase B, nothing.**
  `ReassignNullObjectsAuthority` will be sweeping a set of objects that do not care, and every
  object that does care will be spawned and therefore outside its reach.
- **The unowned case self-heals.** `NetworkGrabbable` requests authority on hover, and taking it
  from `None` succeeds, so an abandoned seed becomes owned again the moment anyone reaches for it.
  Until then it simply does not simulate — which for a seed lying on the ground is invisible.

The one case that does **not** self-heal is a seed whose holder disconnected mid-grab: `HolderId`
still names a player who is gone, so `SingleHolderFilter` refuses every future grab and
`ShouldMasterOwn` stays false, so `AuthorityController` never reclaims it either. **That bug exists
today, unchanged** — the sweep hides half of it by keeping the object owned, but the ghost holder
remains. It is therefore not caused by spawning and not phase A's to fix.

Its proper fix is fact-level, not authority-level, and belongs on its own: Somnium's
`SomniumPlayersContainer.PlayerRemoved` carries the same `ISomniumPlayer.Properties.Id` that
`_grabber` is keyed by, so the master can clear a departed player's hold and let
`AuthorityController` reclaim off the resulting `LifecycleChanged`. Announce the departure as a fact;
do not sweep for the symptom.

**b. Nothing is verified until it runs in an exported world.** `RegisterNetworkPrefabs` mutates
`NetworkProjectConfig.Global.PrefabTable` — global state carrying a `DANGER, DO NOT CHANGE, COULD
BREAK SOMNIUM` comment on its snapshot/restore. The SDK ships the capability, so it is presumably
meant to work, but nobody in this project has run it in-world. Phase A exists to prove exactly this
and nothing else.

**c. Object count becomes unbounded.** 153 pre-placed instances was a hard ceiling that also acted as
a budget. Spawning has none: a player can plant every plot, and produce accumulates. Decay and sale
both despawn, so the steady state is bounded by plot count — but the *transient* is not, and Somnium
may have a per-world NetworkObject limit nobody has hit yet.

**d. Despawning something in a hand.** `Runner.Despawn` destroys the GameObject on every client. If
XRI still has a select on it, the interactor is left holding a destroyed object. Every despawn path
must `ForceRelease()` first — `SellPoint` already hides and un-grabs before it completes, so the
shape exists; planting is the new path that needs it.

**e. Spawn hitching in VR.** Instantiation is not free and this runs at 72–90 Hz on a headset. Shop
restock is once per purchase and fine. Planting is once per plot. Nothing here spawns per frame, but
a garden-wide replant is a burst, and `SpawnAsync` exists if it turns out to matter. Do not
pre-optimise it; measure in-world.

**f. `Spawned()` runs before `Start()`.** Found while writing phase A, and it would have been a
baffling one to debug. Fusion raises `NetworkBehaviour.Spawned()` as part of instantiating the
prefab — the same frame the object is created — so a component that subscribes to
`NetworkBridge.OnSpawned` in `Start()` misses its own spawn callback entirely. Scene objects hide
this completely: the runner spawns them long after every `Start` in the scene has run. `PlantSeed`
and `ViningPlantSeed` now subscribe in `Awake`. **Any new component on a spawnable prefab must do
the same.**

**g. An RPC can outrun the object it is about.** A spawn exists on the spawner immediately and
reaches everyone else a few ticks later, so a state RPC sent right after `Spawn()` may arrive for an
object the receiver does not have, and is dropped. Pooling could never hit this — every seed existed
on every client from scene load. Phase A repeats the broadcast (`ShopSlot.AnnounceStock`) as a
deliberately crude probe; the playtest decides whether anything of the sort is needed. The proper
long-term answer, if it is, is to put birth state in the spawn snapshot rather than chasing it with
messages — `NetworkBridge` already exposes `[Networked] SyncByteArray` for precisely that.

**h. Proxies instantiate at the prefab's own transform.** The `position`/`rotation` arguments are
local-only; the correct pose reaches proxies on the next `NetworkRigidbody3D` tick. Expect a
one-frame pop at spawn. If it reads badly, the fix is spawning invisible and revealing on the first
state sync, not fighting the transform.

## 5. The bigger half: seed and plant stop being one object

`UpdateVisuals(bool isSeed)`, `SetState(bool)`, `IsSeed`-as-state, `IsInPool` and `HideForPool()` all
exist because one pooled instance had to be both a seed and a grown plant. Separate prefabs delete
them outright.

Planting becomes: **master despawns the seed and spawns the plant at the slot.** Which is also the
first time the lifecycle honours the governing rule properly — today `PlantSeed.OnTriggerEnterSeed`
plants on whichever client holds state authority, i.e. the buyer.

The flow, copying the purchase two-step exactly:

1. The holder's seed enters a `PlantSlot` trigger → the holder calls `RequestPlant(slotId)`.
   Only the holder knows its own hand is on the seed; this is the same conclusion `RequestPurchase()`
   reached, for the same reason.
2. Every client receives it; only the master acts. It checks the slot is free and the seed is bought,
   then spawns the plant prefab at the slot with `SharedModeStateAuthMasterClient`, sets
   `_plantedTimestamp` in `onBeforeSpawned`, marks the slot occupied, and announces.
3. The seed's own authority despawns it on hearing the announcement — it is the only client that
   can. Locally it can hide immediately, so the hand feels instant (optimistic locally,
   authoritative eventually).

**This kills the pumpkin's oversized-vine problem structurally.** A pumpkin seed prefab is a seed
mesh; the pumpkin plant prefab is a vine. `_SeedModel` stops being overloaded, and the 24.6 cm
collider offset that lets `HasLeftSlot()` swallow real exits stops existing because the shop stock is
no longer a vine with a fruit hanging off it. Turnip's 8.3 cm of slack goes the same way.

Growth stays a **derivation**: the plant is spawned, its planted timestamp is broadcast once, and
every client computes scale forever. No new sync.

## 6. Phasing — one per upload

| | Change | What the in-world test proves |
|---|---|---|
| **A** | Register `Unified_Carrot` in `SceneNetworking._networkPrefabs`. `ShopSlot.SpawnSeed()` spawns it master-authored instead of claiming from the pool, **carrot only**; `SellPoint` despawns a spawned plant instead of pooling it. Turnip and pumpkin keep pooling untouched, as the control. | The mechanism itself: does registration survive export, does a spawned object replicate, does a **late joiner** see it, does the master hold authority, does a carrot still buy and sell |
| **B** | All three crops spawn. Delete `UnifiedPool`, `PoolManager`, `IsInPool`, `HideForPool()`, the 153 scene instances and the `ShopSlot.Update()` retry. | Nothing regressed with the scaffolding gone; shop restock under multiple clients |
| **C** | Seed and plant become separate prefabs. `RequestPlant` → master spawns the plant, seed authority despawns the seed. Deletes `IsSeed`, `SetState(bool)`, `UpdateVisuals(bool)`. Pumpkin gets a real seed prefab while it is open. | The full buy → plant → grow → sell loop, and that planting is decided in one place |
| **D** | `Produce` and the bearing-plant split — `bearing-plants-and-produce.md` phases A–D, minus everything pool-shaped | as that doc describes |

A and B are deliberately not merged. A is the only phase whose purpose is to answer "does this work
in Somnium at all", and it wants the pooled path still standing next to it as a control.

## 7. What this revises in `bearing-plants-and-produce.md`

That doc was designed against a world where every NetworkObject was pre-placed. It is **unblocked**
by this, not invalidated — the hierarchy (`BearingPlantSeed` → `SingleHarvest` / `MultiHarvest`), the
`ISellable` extraction, ripening-as-derivation, the wire-id reservations and the harvest two-step all
stand unchanged. What changes:

- **`ProducePool` never gets written.** A `Produce` is spawned at the socket by the plant's authority
  and despawned at sale. Its §7 "pool exhaustion → retry each frame" paragraph is deleted, along with
  its phase B "fruit prefab **+ pool** in scene".
- **`BearingPlantSeed` holds a spawned produce's `NetworkId`, not a pooled instance's.** Same
  `Id.Raw` addressing, so the code shape is identical.
- **Open question 4 ("can one plant bear more than one Produce at once?") gets cheap.** With no pool
  to size, `_produceSocket` → `_produceSockets[]` costs a loop. Still ship one socket first; the
  reason is feel, not cost.
- **§9's "what deletes" grows** — `RegrowableFruit.cs` and the `_fruitModel`/`_fruitCollider` split
  were already listed; the seed/plant prefab split (§5 here) subsumes more of `ViningPlantSeed` than
  that doc anticipated, since it assumed one instance must still be both seed and plant.

Its §2 note about **renaming `ViningPlantSeed.cs` and keeping its `.meta` GUID** still applies and is
still the trap most likely to cost an hour.
