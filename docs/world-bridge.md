# WorldBridge — one point of contact with the SDK

Status: **deferred. Do not start until the seed → plant → produce → sale loop is confirmed working
in-world.** Noted 2026-09-03 on `feature/runtime-spawn`.

Refactoring the spawn path while debugging the spawn path costs the only diagnostic tool this
project has — "this changed, that broke". This is a cleanup, and cleanups wait for green.

---

## Why

Two arguments, and the second is the one that matters.

**It is already duplicated, and the duplication has already cost us.** `Plant.SpawnProduce` and
`PlantSlot.SpawnPlant` are the same twenty-five lines twice: resolve the runner, look up the prefab
id, spawn master-owned, catch the throw, write the pose, `GetComponent`. The explicit pose write was
missing from *both*, produced a carrot that grew perfectly and appeared nowhere near its plot, and
had to be fixed twice by hand. Rule 1 — one owner per shared property — is why `KinematicController`
exists; this is the same shape applied to objects entering and leaving the world.

Worse than the spawn path: `SellPoint.TakeOwnershipAndComplete`, `ShopSlot.TakeOwnershipAndRecall`
and `PlantSlot.TakeOwnershipAndDespawn` are **three copies of the same coroutine**, each with its own
timeout field. Ask for authority, wait, act or give up.

**The SDK is an unstable dependency and should touch a small surface.** This is the real reason, and
it is a straight dependency-inversion argument that this project has already paid for twice:
Community Modules V2 → V3 forced a re-vendor of everything built on it, and SDK 3.1.7 changed asset
bundle compression under a client that could not read the result. The ProSDK ships as opaque DLLs, so
a deprecation does not arrive as a compile error — it arrives as a world that does not work.

If `NetworkBridge` is dropped for raw Fusion calls, or a method is deprecated, that should be one
file to change. Today it would be every script and **every prefab**, because `NetworkBridge` is not
merely called — it is a serialized field on nine crop prefabs plus `PlantSlot`, `SellPoint`,
`EconomyManager` and `BalanceDisplayManager`. That is a scene-wide re-wire, not a code change.

## Naming

**`WorldBridge`**, not `WorldManager`. "Manager" says nothing about the job; "bridge" states it — the
one crossing point between this game and everything underneath it, and it matches the vocabulary
already in use.

It sits **beside** `SceneNetworking`, wrapping it. It does not replace or edit it: `SceneNetworking`
is vendored Community Modules and has to stay diffable against upstream, with its one deliberate
divergence documented. Adding our own API into that file would make the next re-vendor harder, which
is the exact problem this is meant to solve.

## What goes behind it

In rough order of how much each is worth:

1. **Spawn.** Prefab-id lookup, `SharedModeStateAuthMasterClient`, the try/catch that stops a spawn
   throw blacking out the world through `ExceptionAlarm`, and the explicit pose write that Fusion's
   local-only spawn position makes necessary.
2. **Despawn.** The authority check — and it must be **loud** when it cannot act. Fusion's own
   `Despawn` silently does nothing without state authority, and that silence cost real time. An
   abstraction that inherits the silence is worse than no abstraction.
3. **Take authority, then act.** The three duplicated coroutines, with one timeout.
4. **World queries** — master client, network readiness, local player. Today these are static reads
   of `SceneNetworking` scattered through every class.
5. **Messaging** — the `RPC_SendMessageToAll` / `ToProxies` / `ToController` triplet and its
   receive events.

## What does not go behind it, and the honest limit

**A serialized `NetworkBridge` field on a prefab is Inspector wiring, not a call, and no wrapper
hides it.** Removing that coupling means our own messaging component owning the reference and every
class talking to *that* — which is item 5, and it is the big one. It touches every class in the game
and cannot be verified in a single upload.

So stage it: **1–3 first.** They are pure de-duplication with no behaviour change, they are where the
bug actually happened, and they can land in one upload and be tested by the loop still working. Items
4 and 5 earn their place separately, or never — an abstraction added before it is needed is its own
kind of debt.

Do not wrap Unity itself. `transform`, `Rigidbody` and XRI are not the unstable dependency here and
have decades of stability behind them; hiding them buys nothing and costs readability.
