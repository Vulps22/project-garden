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
which rebuild their dictionary from scratch and refresh the [[scoreboard]]. `PlayerBalanceChanged` fires locally
afterward.

- `RemoveBalance` deliberately applies locally on non-master clients before the broadcast arrives (optimistic
  deduction, from #39). `AddBalance` does not.
- Because the table is cleared and rebuilt on every broadcast, `GetLocalPlayer()` can be **transiently null**.
  Refuse rather than guess when it is; the next broadcast restores it.
- A departing player's balance row is removed by `PlayerLifecycle` after the departure grace
  ([[plot-ownership#Departures|plot-ownership → Departures]]).

## Starting balance

**Status (2026-11-04):** built; 10 in the scene.

A new player starts with `EconomyManager._startingBalance` Thatch, a serialized field on `EconomyManager` in the
scene. **10 is the real value.** It may be raised for a test build, so the rarer seeds can be reached without many
manual loops in VR; putting it back is on the pre-upload list ([[todolist]]).

## Upgrade bonus

**Status (2026-11-04):** built, compiles clean, untested in-world.

A grown upgrade (a `StandingPlant`: popcorn, Spotlight Tulip, Moonflower) raises the **profit** of everything its Plot
grows, by its seed's `upgradeBonus` (per seed in [[crops#Balance|crops → Balance]]). Rarer upgrades give more. Each kind counts once per Plot; the
kinds add up (all three: +42%).

- **Profit, not sell value** (Vulps, 2026-11-04). A fruit pays its sell value plus bonus × (sell value − its share
  of the seed's price), the share being `buyPrice / fruitPerPlant`. On sell value the thin-margin pumpkin would
  have tripled its profit and caught up with exploring; on profit every crop gains in the same proportion.
- **Rounded up from the hundredth** (Vulps, 2026-11-04): the bonus is taken to two decimal places, then up to the
  next whole Thatch: 7.01 → 8, 7.001 → 7, 0.04 → 1, 0.004 → 0. No upgrade is under 1%, and no profit under 1, so the
  smallest bonus is +1.
- **Counted at the sale,** from the Plot the produce grew in: a fruit picked before the upgrade grew still gets it,
  and one sold after the upgrade is gone does not.
- **Networking.** A produce carries the Plot it grew in (`Produce.Init`'s `grownIn`, appended to its state sync),
  so the master that prices the sale knows it after a handover and after the fruit is picked. Each standing plant
  registers with its Plot on every client once it knows its Plot (`IPlotUpgrade`, `PlotStateManager.UpgradeBonus`);
  whether it is grown is read from the clock at the sale. Nothing new is sent.
- Buffs are not built ([[roadmap]], [[yggdrasil]]).

## Crop scale

**Status (2026-11-06):** decided by Vulps; built, compiles clean, untested in-world.

A buff multiplies a crop's size and its profit, in proportion to how much of its growing it spent under the buff.
Every crop grows at scale 1; a buffing seed has its own multiplier, and Yggdrasil's is 20–30×, the endgame, given its
rarity ([[yggdrasil]]).

- **Counted at the sale** (Vulps), like the upgrade bonus: the crop carries how much buff it took while it grew, and
  the sale works the value out from that and the upgrades.
- **On profit, with the upgrade bonus** (Vulps): `(profit + upgrade bonus) × scale`.
- **The fruit is drawn at its scale** (Vulps): a huge carrot that blocks your view is the fun of it.

- **The scale** (Vulps, 2026-11-05): `floor(1 + (multiplier − 1) × share)`, share being the fraction of its growing
  done under the buff. 10× for half its growing → 5.5 → 5×; none → 1×; all → 10×. Never less than no buff.
- **Its growing** runs from planting for a rooted crop, and from being borne for a bearing one, to ripe.
- **A sale pays** its price, plus the upgrade bonus, plus (profit + upgrade bonus) × (scale − 1), floored
  (`Produce.SellValue`).
- **Drawn at its scale, unfloored**, growing as it does (`Produce.CropScale`).

> [!info] Networking
> Each Plot keeps the buffs that have stood on it, on every client, after their source is gone
> (`PlotStateManager.SetBuff`/`BuffScale`). The master stamps a crop's scale when it ripens, so it outlives the buff, a
> handover and a late join: `Produce`'s state sync gained its growing start and the scale, appended.

## Selling

**Status (2026-09-13):** built and tested in-world.

A sell must be **accepted** by the master, not observed by it ([[principles]]). `SellPoint` knows nothing about
crops: it deals in `SellableEntity`, which owns the object's half of a sale (`SellValue`, `CanBeSold`,
`SetPending`, `OnSold()`), designed in [[crops]] §2a.

**Several sales at once** (built, untested in-world). Each sale carries its own seller and price, any number can
run at once, and a rejection touches only its own item. A repeat offer of an item already being sold is ignored.

**Things a sale gives back** go on the ground in front of the sell point. The sell point is barrels and
bags under a tent with no collider, so there is nowhere on it to put anything. `SellPoint._dropPoint` marks the spot;
`SellPoint` passes it to the item it sells as `SellableEntity.GivenBackAt`, set just before `Sold`. The weed bloom's
splif is the first user ([[weed]]).
