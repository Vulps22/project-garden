---
aliases:
  - "Economy"
  - "Thatch"
tags:
  - mechanic
  - game
---
# Economy

The currency is **Thatch**. Seeds are bought at buy points ([[shop]]) and produce is sold at the sell point; prices
per crop are in [[crops#Balance|crops → Balance]].

## Balances

**Status (2026-09-13):** built and tested in-world.

`EconomyManager` (singleton, on `SceneManager` in the scene) holds `Dictionary<playerId, PlayerBalance>` and is the
only writer. Mutations are master-only; every change re-broadcasts the **entire** balance table to all clients,
which rebuild their dictionary from scratch and refresh `BalanceDisplayManager` (a sorted scoreboard).
`OnPlayerBalanceChanged` fires locally afterward.

- `RemoveBalance` deliberately applies locally on non-master clients before the broadcast arrives (optimistic
  deduction, from #39). `AddBalance` does not.
- Because the table is cleared and rebuilt on every broadcast, `GetLocalPlayer()` can be **transiently null**.
  Refuse rather than guess when it is; the next broadcast restores it.
- A departing player's balance row is removed by `PlayerLifecycle` after the departure grace
  ([[plot-ownership#Departures|plot-ownership → Departures]]).

**`_startingBalance` is 10** (reset 2026-09-03). It was raised to 500 for testing, so the pumpkin could be reached
without ten manual carrot loops in VR. If it is raised again for a test, it is one serialized field on
`EconomyManager` in the scene, and putting it back is the last thing to do before an upload that is not a test
build ([[todolist]]).

## Selling

**Status (2026-09-13):** built and tested in-world.

A sell must be **accepted** by the master, not observed by it ([[principles]]). `SellPoint` knows nothing about
crops: it deals in `SellableEntity`, which owns the object's half of a sale (`SellValue`, `CanBeSold`,
`SetPending`, `OnSold()`), designed in [[crops]] §2a.
