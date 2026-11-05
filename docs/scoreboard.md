---
aliases:
  - "Scoreboard"
tags:
  - mechanic
---
# Scoreboard

Every Garden has a scoreboard: the top ten players by Thatch, sorted, drawn by `BalanceDisplayManager`. The balances
come from [[economy]]; Thatch will persist between sessions ([[persistence]]).

## Showing the board

**Status (2026-09-13):** built and tested in-world.

- Each balance broadcast from `EconomyManager` refreshes `BalanceDisplayManager`.
- The top ten goes out on Garden 0's board's bridge, and every board shows what arrives
  (`BalanceDisplayManager.Shown`, [[towns]]).

## What it ranks

**Status (2026-11-04):** decided by Vulps.

- **Current Thatch, not Thatch earned.** A cumulative total only climbs, so the top player of an idle game would never
  come down. Ranking the balance makes spending a real decision: the leader weighing a Yggdrasil ([[roadmap]]) has to
  ask whether the hit costs them first place. Spending costing rank is the point.
