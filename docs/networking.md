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
  someone else grabs it or `AuthorityController` hands it back to the master. In Fusion 2.1 it does not pass on
  when that player leaves either: the object sits at `StateAuthority == PlayerRef.None` until someone requests it.
  (It transferred on *hover* until 2026-09-09; `OnHover`'s `RequestControl()` was commented out rather than
  deleted, because restoring it is one line.)
- **`OwnerId`** on a plot or seed: **whose it is**. A replicated fact decided by the master, in the same shape as
  `IHeldObject.HolderId`. See [[plot-ownership]].

A plant is simulated only by its state authority; every other client is a passive proxy applying RPC state.
Because authority outlives the grab that caused it, every decision that matters must be *announced* by the master,
never inferred from who happens to hold authority over an object.

## Messaging

**Status (2026-09-13):** built, compiles; **untested in-world, and a wire-format change** (see below).

All sync goes through `NetworkMessenger` carrying `(byte messageId, byte[] data)`, serialized by hand with
`BytesWriter`/`BytesReader` (Community Modules' own, identical to the copies we used to vendor).

`NetworkMessenger` is ours, a component that wraps a **sibling** `NetworkBridgeEvents` rather than subclassing it
(Fusion refused an unwoven `NetworkBehaviour` in our old separate assembly). The transport underneath takes a bare
`byte[]`, so the messenger frames the id into **byte 0** and unpacks it on the way out. The format lives in that one
file rather than in every send site and every handler. `SendToAll`, `SendToProxies`, `SendToController` and
`SendToPlayer` send; `MessageToAll`, `MessageToProxies`, `MessageToController` and `MessageToPlayer` receive.
`OnSpawned` and `OnStateAuthorityChanged` are inherited from the transport and keep their SDK names.

It replaced `NetworkBridge`, which carried seven networked arrays on every instance whether used or not. Community
Modules now marks that class `LEGACY - DO NOT USE ANYMORE`.

- Each component defines its **own private `enum ...MessageType : byte`**: `SeedMessageType`, `PlantMessageType`,
  `ProduceMessageType`, `PlantSlotMessageType`, `SellMessageType`, `PlotMessageType`. Ids are scoped per messenger,
  so `0` in one is unrelated to `0` in another, and each starts at 0. **Append new values, never insert**: these are
  wire ids, and renumbering makes two builds disagree about what a message means.
- `BytesWriter` is **pre-sized**: compute the exact byte count up front. Adding a field to a payload means updating
  the size calculation too, or it overflows. `GetExtraBroadcastStateSize()` shows how subclasses extend a parent's
  payload.
- Delivery targets, chosen deliberately: to all, to proxies (state sync, excluding the authority that already has
  the state), to the controller (the state authority), to one player.
- `long` timestamps are split into two `int`s (high/low); the low half must be re-read as `(uint)` to avoid sign
  extension.

> [!warning] Untested: the byte-0 framing
> Moving onto `NetworkMessenger` (2026-09-13: 33 send sites, 26 subscriptions, 15 prefabs and the scene) changed the
> wire format. A build on the old framing and one on the new cannot talk to each other. It needs an upload and two
> real clients before it counts. Since every world must be re-uploaded for Fusion 2.1 anyway, the first 2.1 test
> covers it.

## Late-join sync

**Status (2026-09):** tested in-world (see *Tested paths*).

New clients get state pushed, never pulled: `SceneNetworking.OnOtherPlayerJoined` → the authority calls
`broadcastState()`. `EconomyManager` does the same on `OnBecomeWorldMaster` (deferred one frame via
`BroadcastNextFrame`). `Seed` also re-broadcasts on `OnStateAuthorityChanged`, because `broadcastState()` is a no-op
without authority, so anything that changed while unowned was never sent. This is the mechanism behind issue #40.

`AnnounceStock` still re-broadcasts three times, in `BuyPoint`, `DispensingEntity` and `PlotLeaseManager`. Late
joiners do see stock, so the loop can most likely collapse to one broadcast; confirm before deleting it.

## Tested paths

**Status:** late join, a player leaving, and the master client leaving have all been exercised with real clients,
and they work. **Do not re-open them.** These docs once listed them as the outstanding risk long after the testing,
and that was repeated back as fact in several sessions. If a multiplayer path looks unverified, the likely reason is
a stale doc, not missing work. Ask before asserting it.

Multi-client behaviour (master authority, RPC state sync, late join) is only observable in-world with several real
clients; a single client tells you nothing about any of it ([[testing]]).

## Fusion 2.1

**Status (2026-10-31):** migrated, compiles clean; nothing below is tested in-world.

Somnium moved to Fusion 2.1.3 on 2026-10-20; worlds built on 2.0 no longer sync. How the migration ran:
[[community-modules#Migration to 4.0 and Fusion 2.1|community-modules → Migration]]. What changed for us:

- **The physics addon is gone.** `NetworkRigidbody3D` despawns its object in shared mode; the migration replaced it
  with `NetworkTransform` on every prefab. `WorldBridge.Place` now teleports through `NetworkTransform.Teleport`
  (state authority only) and zeroes the body's velocity.
- **`RequestStateAuthority()` returns `bool`**: `false` means the request was not sent.
- **`NetworkTransform` syncs parent-relative position.** XRI unparents an object while it is held, so grabbable
  network objects should sit at the scene root; `NetworkGrabbable`'s *Detach From Parent*, on by default, does that
  when the world loads.
- **Teleports belong in the network tick.** A change between ticks (`Update`, a coroutine, a UnityEvent) can be
  overwritten by the next tick; teleporting a `NetworkTransform` that way was found unreliable in Somnium. The
  pattern is to record the request and apply it in `OnFixedUpdateNetwork` on the state authority.

> [!warning] Open from the migration
> - **A second writer of `isKinematic`.** With Forecast off, `NetworkTransform` makes the Rigidbody kinematic on
>   every client that does not own it. `KinematicController` is meant to be the only writer
>   ([[principles#^one-owner|principles → one owner]]).
> - **Produce on sockets.** *Detach From Parent* would pull a produce off its pumpkin socket at load. It probably
>   wants turning off on the `Produce_*` prefabs; check what holds produce to a socket first ([[crops]]).
> - **Interpolation on kinematic grabs.** Objects grabbed with XRI Kinematic movement need *Disable Shared Mode
>   Interpolation* ticked on their `NetworkTransform`, or they stutter for everyone. The migration checks Forecast;
>   it does not set this.
> - **`WorldBridge.Place` teleports from wherever it is called**, not from the tick.
