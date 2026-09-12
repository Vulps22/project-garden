# Bridges — one seam per thing that isn't ours

Status: **`WorldBridge` built 2026-09-11** (`20fa8fe`), **`PlayerBridge` built 2026-09-12**. Both are
in use and neither is finished — what remains is at the bottom.

This started as a design for a single wrapper over the SDK and is now the record of a layer with two
of them in it. The layer's rules live in CLAUDE.md under **Layers**; this doc is the mechanism and
the history.

---

## Why

Three arguments, and the second is the one that matters.

**It was already duplicated, and the duplication had already cost us.** `Plant.SpawnProduce` and the
plant-spawning in what is now `PlotStateManager` were the same twenty-five lines twice: resolve the
runner, look up the prefab id, spawn master-owned, catch the throw, write the pose, `GetComponent`.
The explicit pose write was missing from *both*, produced a carrot that grew perfectly and appeared
nowhere near its plot, and had to be fixed twice by hand. Rule 1 — one owner per shared property —
is why `KinematicController` exists; this is the same shape applied to objects entering and leaving
the world.

Worse than the spawn path: `SellPoint`, `Socket` and `PlotStateManager` each carried their own copy
of the same take-authority-then-act coroutine, with their own timeout field. Ask for authority,
wait, act or give up.

**The SDK is an unstable dependency and should touch a small surface.** This is the real reason, and
it is a straight dependency-inversion argument the project has already paid for twice: Community
Modules V2 → V3 forced a re-vendor of everything built on it, and SDK 3.1.7 changed asset bundle
compression under a client that could not read the result. The ProSDK ships as opaque DLLs, so a
deprecation does not arrive as a compile error — it arrives as a world that does not work.

**It is where the SDK's vocabulary can be made ours.** A pass-through is not a wasted method: it is
the only place a name can be corrected. `SceneNetworking.OnOtherPlayerJoined` breaks the project's
own event convention — an event is the fact, `On` belongs to the handler — and it cannot be renamed,
because it is vendored and has to stay diffable against upstream. Behind a seam it becomes
`OtherPlayerJoined` for everyone downstream.

Worth doing **opportunistically**: normalise a name the next time that function is touched for
another reason. A rename sweep for its own sake is churn; a rename taken on the way past is free.

## Naming

**Bridge**, not manager. "Manager" says nothing about the job; "bridge" states it — the crossing
point between this game and something underneath it.

They sit **beside** what they wrap and never edit it. `SceneNetworking` is vendored Community
Modules and has to stay diffable against upstream, with its divergences documented. Adding our own
API into that file would make the next re-vendor harder, which is the exact problem this solves.

## One bridge per dependency, not one bridge

The original plan was a single `WorldBridge` over everything. That was wrong, and `PlayerBridge`
is why: object lifecycle and player identity have nothing to do with each other, change for
different reasons, and would have made one file that every class in the game had a reason to touch.

- **`WorldBridge`** — Fusion. Spawn, place, despawn, take authority. A static class; Fusion exposes
  no events it has to stay subscribed to.
- **`PlayerBridge`** — Somnium's player list, avatar rig and locomotion, plus `SceneNetworking`'s
  master-client flag. A MonoBehaviour on `SceneManager`, because Somnium publishes its player list
  as UnityEvents and something has to be alive to add and remove listeners.

`PlayerBridge` established the rule that a bridge does not re-export what it fronts. It answers in
`PlayerIdentity` (an id and a name, a snapshot rather than a handle) and `PlayerRig` (root, head,
hands, with `IsUsable` because Somnium assembles the rig after the player joins). `ISomniumPlayer`
and `SomniumPlayersContainer` are named in that one file. Before it, `PlayerManager` returned
`ISomniumPlayer` and 12 files named the type, so going through the manager bought them nothing.

`PlayerIdentity` is a struct, so `== null` on one silently always passes. Ask `.Exists`.

## Who is allowed to call one

**Managers, and only managers.** Components and orchestrators call a manager, which calls the
bridge — see Layers. This is the part the original draft got wrong: it proposed de-duplicating
`Plant.SpawnProduce` by having `Plant` call `WorldBridge` directly, which removes the duplication
but puts a component two layers down.

The manager is often a one-line forward today, and that is the job. It is the callsite: when a
guard, a cache or a correction is needed later it lands in one method rather than in every seed,
plant and point that asked. `PlayerManager` is the worked example — a row of forwards to
`PlayerBridge`, and the reason the `ISomniumPlayer` sweep touched one file instead of twelve.

## What went behind it

1. **Spawn.** Prefab-id lookup, `SharedModeStateAuthMasterClient`, the try/catch that stops a spawn
   throw blacking out the world through `ExceptionAlarm`, and the explicit pose write that Fusion's
   local-only spawn position makes necessary — `Teleport()` on a `NetworkRigidbody3D`, since a
   kinematic one ignores `transform.position`.
2. **Despawn.** The authority check, and it is **loud** when it cannot act. Fusion's own `Despawn`
   silently does nothing without state authority, and that silence cost real time. An abstraction
   that inherited the silence would be worse than no abstraction.
3. **Take authority, then act.** The three duplicated coroutines, with one timeout, as
   `TakeAuthority(obj, timeout, granted)`.
4. **World and player queries.** `IsMaster`, identity, the avatar rig and locomotion went behind
   `PlayerBridge`; find-by-network-id and the Fusion readiness check behind `WorldBridge`, each of
   which had been hand-rolled five and three times over.
5. **The vendored events.** `OtherPlayerJoined`, `BecameWorldMaster` and `NetworkReady` are the
   bridges' now, with the `On` prefix dropped because an event is the fact. `OtherPlayerJoined`
   loses its `PlayerRef` on the way through — no subscriber used it, and a bridge does not hand out
   the types it fronts.

`SceneNetworking` is named in `PlayerBridge` and `WorldBridge` and nowhere else in the game.

**Who calls which:** components and orchestrators call `WorldManager` or `PlayerManager`; those two
call the bridges; `PlotStateManager`, `PlotLeaseManager` and `EconomyManager` call a bridge directly,
because a manager calling a manager is the sideways call.

## What has not

**Messaging — the big one.** The `RPC_SendMessageToAll` / `ToProxies` / `ToController` triplet and
its receive events are still raw `NetworkBridge`, and `NetworkBridge` is not merely called: it is a
**serialized field on nine crop prefabs** plus several managers. No wrapper hides Inspector wiring.
Removing that coupling means our own messaging component owning the reference and every class
talking to *that* — it touches every class in the game and cannot be verified in a single upload.

**Not Unity itself.** `transform`, `Rigidbody` and XRI are not the unstable dependency here and have
decades of stability behind them; hiding them buys nothing and costs readability.
