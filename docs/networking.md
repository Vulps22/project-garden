---
aliases:
  - "Networking"
tags:
  - tech
  - rules
---
# Networking

The mechanism under [[principles]]: who owns what, how a message travels, how a late joiner catches up, and what
Fusion 2.1 changed. Spawning and despawning are in [[runtime-spawn]]; plot and seed ownership in
[[plot-ownership]].

Networking is Photon Fusion 2.1.3 in shared mode, under Somnium's `NetworkBridgeEvents` transport from Community
Modules 4.0 ([[community-modules]]). There is no Fusion `[Networked]` property anywhere in our code, and no
`NetworkBehaviour` of our own.

## Three notions of ownership

**Status (2026-09-13):** built and tested in-world.

Master-client authoritative, with **three** distinct notions of ownership. Do not conflate them:

- **`SceneNetworking.IsMasterClient`**: scene-wide owner. Gates economy mutation, spawning, planting decisions and
  slot bookkeeping. It resolves to the **first player to join** (confirmed empirically, not yet in a busy
  multi-client session; treat as confirmed-but-provisional). Game code asks `PlayerManager.IsMaster`.
- **`Object.HasStateAuthority`**: **per-NetworkObject** Fusion authority, who *simulates* this object and who may
  despawn it. `NetworkGrabbable` requests it on grab, so it follows a grab, but it is **not evidence of whose
  something is**: it never transfers back on release, so whoever last grabbed an object keeps simulating it until
  someone else grabs it or `AuthorityController` moves it to a player in its sector
  ([[#Authority by sector]]). In Fusion 2.1 it does not pass on when that player leaves either: the object sits at
  `StateAuthority == PlayerRef.None` until someone requests it.
  (It transferred on *hover* until 2026-09-09; `OnHover`'s `RequestControl()` was commented out rather than
  deleted, because restoring it is one line.)
- **`OwnerId`** on a plot or seed: **whose it is**. A replicated fact decided by the master, in the same shape as
  `IHeldObject.HolderId`. See [[plot-ownership]].

A plant is simulated only by its state authority; every other client is a passive proxy applying RPC state.
Because authority outlives the grab that caused it, every decision that matters must be *announced* by the master,
never inferred from who happens to hold authority over an object.

## Authority by sector

**Status (2026-11-01):** built; played alone in-world, where the local player is every sector's pick. The point of
it, several players in different sectors, is untested.

The master no longer simulates everything. Once the world spreads over kilometres it would be simulating seeds on
islands it has not loaded, and they would fall through where the island should be. **The master decides; a player
standing there simulates.**

- **Sectors are public.** `SectorManager` (on `SceneManager`, message id 2) hears from `PlayerTracker` when the local
  player crosses into another 1 km sector and announces it with its Fusion peer id, again when anyone joins, and once
  Fusion gives this client its peer id (`WorldBridge.NetworkReady`). A late joiner's scene messenger can still be
  attaching at that moment, and an RPC then throws inside Fusion, so `NetworkMessenger.CanSend` also needs
  `NetworkObject.IsValid` and an announcement that cannot be sent yet goes out on the messenger's `OnSpawned`. Every
  client holds the same table of who is where; a peer is dropped from it when it leaves the room. Sector 0 is
  centred on the Garden; the maths (`SectorOf`, `SectorCentre`) lives here and the map uses it.
- **Each networked object declares a rule**, `IAuthoritySource.Authority`:
  - **Master:** plants, and produce still on the plant. Neither has physics, and both are the master's decisions.
  - **Holder:** anything in a hand stays with that hand.
  - **Sector:** everything else (seeds, harvested produce, deeds), including shop stock.
- **`AuthorityController`** does the moving. For a Sector object, the pick is the **lowest peer id in the sector the
  object is in**. Every client computes the same pick from the table, and only the pick requests authority, and only
  when the current authority is not in that sector already. **Event driven:** it decides when the sector table
  changes, the master changes, the object spawns, changes hands or changes lifecycle, and when the object comes to
  rest somewhere new. Only the client simulating an object sees it move, so that client reports it: when the object is
  released (`lastSelectExited`), lands (`OnCollisionEnter`) or finishes a recall (`ReturnableEntity.Arrived`), and its
  sector differs from the last one known, it sends the object's network id and new sector (message id 14 on
  SceneManager's messenger, `SectorManager.ReportObjectSector`) and every client decides from that sector rather than
  the transform, which lags on proxies. **It never takes an object from a player the sector table does not know
  yet** (`SectorManager.IsKnownPeer`): that player may be holding it, and taking it back snaps it out of their hand
  to where they picked it up. The starting sector needs no message: every client sees the spawn pose. At
  home with the master in the Garden, the master is normally the pick, so the Garden behaves as before.
- **Nobody there.** `KinematicController` holds a Sector object kinematic on the client simulating it when that
  client does not have its ground loaded (`SectorManager.HasGroundAt`: an island cell `IslandField` has loaded, or
  within 60 m of a Garden). It looks again when islands are placed or removed (`IslandField.GroundChanged`), when the
  object is released or recalled, and when authority changes. It stays put until a player enters its sector, takes
  authority and it is physical again.
  This also covers the master inheriting a disconnected player's objects.
- **Loading follows the sector.** `IslandField` loads every island that touches the local sector, plus every cell
  within the draw distance, refreshed when the player moves into another 150 m cell or sector or the draw distance
  changes. "Touches", not "centred in", because a seed can sit on the part of an island that crosses
  a sector line, and the pick for that sector must have that island.
- **Decisions still travel as RPCs to everyone**, applied by everyone, so it does not matter who simulates. A
  despawn goes to whoever simulates the object (`Seed.RequestDiscard`), so the master never has to fight a sector
  player for authority to remove something.
- **`Socket` arms recall on every client**, not just the master, because whoever simulates the stock drives its
  return.

## Messaging

**Status (2026-10-31):** built, tested in-world.

All sync goes through `NetworkMessenger` carrying `(byte messageId, byte[] data)`, serialized by hand with
`BytesWriter`/`BytesReader` (Community Modules' own, identical to the copies we used to vendor).

`NetworkMessenger` is ours, a component that wraps a **sibling** `NetworkBridgeEvents` rather than subclassing it
(Fusion refused an unwoven `NetworkBehaviour` in our old separate assembly). The transport underneath takes a bare
`byte[]`, so the messenger frames the id into **byte 0** and unpacks it on the way out. The format lives in that one
file rather than in every send site and every handler.

**Fusion drops an RPC over 512 bytes** (logged as "payload is too large"), and the whole stock table, balance table or
access list outgrows that (the stock table at about 36 stalls; there are 42 in the Gardens alone). So the messenger
splits any message over 400 bytes into numbered pieces (marker id **255, reserved**; the message id, a per-sender key,
index and count) and raises it once, whole, when every piece has arrived, in any order (built 2026-11-04 after the
stock table never reached a joiner; round-trip checked in the Editor, untested in-world). Up to 255 pieces. `SendToAll`, `SendToProxies`, `SendToController` and
`SendToPlayer` send; `MessageToAll`, `MessageToProxies`, `MessageToController` and `MessageToPlayer` receive.
`OnSpawned` and `OnStateAuthorityChanged` are inherited from the transport and keep their SDK names.

Community Modules' `NetworkBridge` (marked `LEGACY - DO NOT USE ANYMORE`) is not used anywhere.

- Each component defines its **own private `enum ...MessageType : byte`**: `SeedMessageType`, `PlantMessageType`,
  `ProduceMessageType`, `CollectibleMessageType`, `SellMessageType`, `PlotMessageType`. Ids are scoped per messenger,
  so `0` in one is unrelated to `0` in another, and each starts at 0. **Append new values, never insert**: these are
  wire ids, and renumbering makes two builds disagree about what a message means. Components that share a messenger
  share one enum (`PlotMessageType` for a Plot's managers, [[plot-ownership#What is networked, and what is not|plot-ownership]];
  `CollectibleMessageType` for `CollectibleEntity`, `PlotApplication` and `SmokeableEntity`).
- **Several components share `SceneManager`'s messenger**, so they share its id space, each id a `const byte` in
  its component. The next free id is **24**; 20–21 are reserved for the world version check
  ([[playtest-2026-11-04#Designed, not built|playtest 2026-11-04 → Designed, not built]]).

| Id | Component | Message |
|---|---|---|
| 0 | `EconomyManager` | the balance table |
| 1 | `WorldSeed` | the world seed |
| 2 | `SectorManager` | a peer's sector |
| 3–6 | `ShopKeeper` | purchase request, taken, taken set (sold-out list), recycled |
| 7–8 | `DeveloperMarket` | item request, delivered |
| 9–10 | `DeveloperAccess` | grant or take, the access list |
| 11 | `WorldManager` | seconds to the next cycle |
| 12–13 | `DeveloperBroadcast` | ask, show |
| 14 | `SectorManager` | an object's new sector |
| 15 | `DeveloperMarket` | Grow Wand request |
| 16–19 | `ShopKeeper` | stocked, emptied, the stock table, table request |
| 22–23 | `EconomyManager` | balance-table request, a peer's offer of the table |

- `BytesWriter` is **pre-sized**: compute the exact byte count up front. Adding a field to a payload means updating
  the size calculation too, or it overflows. `GetExtraBroadcastStateSize()` shows how subclasses extend a parent's
  payload.
- Delivery targets, chosen deliberately: to all, to proxies (state sync, excluding the authority that already has
  the state), to the controller (the state authority), to one player.
- `long` timestamps are split into two `int`s (high/low); the low half must be re-read as `(uint)` to avoid sign
  extension.

## Late-join sync

**Status (2026-09):** tested in-world (see *Tested paths*).

New clients get state pushed: `SceneNetworking.OnOtherPlayerJoined` → the authority calls `broadcastState()`. A
seed or produce that has just spawned on a proxy also asks its owner for its state (`stateRequest`, to the
controller), because the owner's state sent at the moment of spawning can reach a proxy before the object does. `EconomyManager` does the same on `OnBecomeWorldMaster` (deferred one frame via
`BroadcastNextFrame`). `Seed`, `Plant` and `Produce` also re-broadcast on `OnStateAuthorityChanged` when this client is
the master, because `broadcastState()` is a no-op
without authority, so anything that changed while unowned was never sent. This is the mechanism behind issue #40.

Stock no longer re-broadcasts on a timer: the stock table is the master's ([[shop]]) and a new seed asks for its
state. `DispensingEntity` (scrolls) still re-broadcasts three times after spawning; the same
spawn-time request replaces that in the next event-driven pass.

## Handover gaps

**Status (2026-11-04):** found by a code review after a player reconnected to placeholder text on the scoreboard.
All ten built (compile clean, untested in-world).
Most severe first; every fix hooks an event.

1. **Balances reach a joiner only when a balance changes.** `EconomyManager` has no join handler, and
   `EnsureBalance` sends nothing for a player still inside the 180 s grace period, so a rejoiner's board keeps its
   placeholder text. **Built:** the master broadcasts balances on `OtherPlayerJoined`, and a joiner without a table
   asks once its messenger can talk (`EconomyManager` message 22, as `ShopKeeper.AskForTable` does).
2. **A master that never received the balance table wipes everyone's balances** when it takes over (it broadcasts
   only its own row). **Built:** such a master does not broadcast on taking over; it asks everyone (message 22 with
   a flag), any client holding the table offers it (message 23), and the first offer replaces the master's blank
   rows and goes out. Still exposed: a sale or join in the moment before an offer arrives broadcasts the partial
   table.
3. **A new master never hears harvests from plants the old master bore:** `BearingPlant` subscribes to
   `Produce.Harvested` only on the master that called `Bear()`. Those plants never re-bear or wither. **Built:** on
   `BecameWorldMaster` a bearing plant listens to every socket's produce, and one already taken counts as harvested.
4. **Produce left in the ground is never released by a new master:** `Produce._plot` and `_slotIndex` are set only
   in `Init`, not in its state. **Built:** the slot index rides in the produce's state sync after the Plot it grew in.
5. **Slot occupants are known only to the master that planted them** (`PlotStateManager`), so cleanup after a new
   master frees slots without uprooting their plants. **Built:** a plant (`Plant.AdoptSlot`) and a rooted produce set
   themselves as occupant on every client; a freed slot clears it; cleanup skips an occupant already destroyed.
6. **Objects with no authority send no state to joiners** (loose seeds, harvested produce, deeds and scrolls in an
   empty sector), and a joiner holding a blank copy can later broadcast it over everyone's. **Built:** the master
   takes a Sector object nobody simulates in a sector nobody is in (`AuthorityController.Apply`, on
   `BecameWorldMaster` and `SectorsChanged`); a seed, produce or collectible broadcasts only once it holds real state
   (it spawned the object, a state sync arrived, or a collectible's whole-state message did).
7. **A master that joined after someone left never clears them:** `PlayerManager._pending` holds only departures
   that client saw. **Built** without a message: the balance table lists everyone not yet cleared, so when a table is
   applied on the master, anyone in it who is not present starts a grace period there (`PlayerLifecycle`,
   `PlayerManager.HoldDeparted`). Their grace restarts from when this master learned of them.
8. **A scroll's chosen Plot is not in its state sync** (`PlotApplication.AssociatedPlotId`). **Built:** the master
   announces it again to every joiner.
9. **Minor:** a new master marked a pinned stall sold out on its first sale (`ShopKeeper._pinned` was master-only).
   **Built:** every client learns pinned stalls from the stalls' requests. A purchase or sale in flight when the
   master leaves was lost (the seed stayed unbought in the hand, the produce hidden for good). **Built:** every
   client remembers the requests it hears until they are decided (`ShopKeeper._asked`, `SellPoint._offered`), and on
   `BecameWorldMaster` the new master decides those still open: a seed still stock and still in a hand, an item
   still there. Still exposed: an old master that left between its two messages (the seed bought but not charged,
   or a seller paid but the sale not announced, which would pay twice).
10. **Duplicate stock:** `DispensingEntity` (the scroll dispenser) had the deeds' defect. **Built:** it adopts its
    stock on every client ([[plot-ownership#Discarding a scroll|plot-ownership → Discarding a scroll]]); deeds are
    no longer spawned at all.

## Tested paths

**Status:** late join, a player leaving, and the master client leaving have all been exercised with real clients,
and they work. **Do not re-open them.** These docs once listed them as the outstanding risk long after the testing,
and that was repeated back as fact in several sessions. If a multiplayer path looks unverified, the likely reason is
a stale doc, not missing work. Ask before asserting it.

Multi-client behaviour (master authority, RPC state sync, late join) is only observable in-world with several real
clients; a single client tells you nothing about any of it ([[testing]]).

## Fusion 2.1

**Status (2026-10-31):** migrated, tested in-world.

Somnium moved to Fusion 2.1.3 on 2026-10-20; worlds built on 2.0 no longer sync. How the migration ran:
[[community-modules#Migration to 4.0 and Fusion 2.1|community-modules → Migration]]. What changed for us:

- **The physics addon is gone.** `NetworkRigidbody3D` despawns its object in shared mode; the migration replaced it
  with `NetworkTransform` on every prefab. **Forecast physics stayed on until 2026-11-02:** the tool's
  recommended-settings step logged "Forecast setting not found" for every prefab and changed nothing, while 2.1.3
  keeps it at `PhysicsSettings.ForecastEnabled`. With it on, other players see a local physics guess instead of the
  owner's path, which is the drifting, rubber-banding grab the guide warns of. It is now off on all 17 of our
  networked prefabs (the Community Modules examples still have it on; the world does not use them). Every
  `NetworkTransform` added from now on needs it off too. `WorldBridge.Place` now teleports through `NetworkTransform.Teleport`
  (state authority only) and zeroes the body's velocity.
- **`NetworkTransform` replicates nothing about the Rigidbody.** A proxy left physical falls between ticks and is
  snapped back, so other players' held items bounced (2026-11-04). `KinematicController` now holds every proxy
  kinematic, gives the body its declared mode only on the client simulating it, and leaves it alone while a hand
  holds it (XRI makes it kinematic and weightless for the grab; release applies again). Built, untested in-world.
- **`RequestStateAuthority()` returns `bool`**: `false` means the request was not sent.
- **`NetworkTransform` syncs parent-relative position.** XRI unparents an object while it is held, so grabbable
  network objects should sit at the scene root; `NetworkGrabbable`'s *Detach From Parent*, on by default, does that
  when the world loads.
- **Teleports belong in the network tick.** A change between ticks (`Update`, a coroutine, a UnityEvent) can be
  overwritten by the next tick; teleporting a `NetworkTransform` that way was found unreliable in Somnium. The
  pattern is to record the request and apply it in `OnFixedUpdateNetwork` on the state authority.
