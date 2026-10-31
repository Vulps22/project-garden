---
aliases:
  - "Layers"
tags:
  - rules
  - tech
---
# Layers

The shape of the code. The bridges themselves, what each fronts and what is still raw, are in [[world-bridge]].

**Status (2026-09-13):** the rule. The tree mostly matches it; the exceptions are under *Where the tree does not
match this yet*.

```
components (Seed, Plant, BuyPoint, SellPoint)   the thing a hand touches
orchestrators (PlayerLifecycle)                 listen, then command
managers (Economy, Player, World, PlotState)    own state; the only caller of a bridge
bridges (World, Player, Storage, Messenger)     the only code that names anything outside ours
```

It is the shape of a server, and the analogy is worth keeping in mind: **bridges are DAOs, managers are services,
orchestrators and components are handlers.** Everything below follows from that.

## Each layer calls the one below it and no further

Components and orchestrators call managers. Managers call bridges. Bridges call Fusion, Photon, the ProSDK and
Community Modules. A component reaching past its manager straight to a bridge is the same mistake as reaching past
the bridge to the SDK; it has only travelled one layer further before making it.

## No layer talks sideways

A bridge does not call another bridge; a manager does not call another manager. Two things in the same layer that
need each other are coordinated from **above**: an orchestrator subscribes and calls them both, so the order is
written down, or a component does it where a component is the suitable place. Manager-to-manager is the one that
keeps getting attempted: two managers reacting to the same event have an order decided by `Awake` order, which is
neither controlled nor visible, and nobody finds out until the day it flips.

Components may subscribe freely. A `Seed` dropping a departed player's `HolderId` reacts to a fact that is already
true and nothing waits on it. The line is **notification versus decision**, not who is allowed to listen. A
notification is "this is true, update yourselves"; a decision is "given this, do these things, in this order".

## What a bridge is

**A bridge is where our code touches anything that is not ours.** The boundary is our own code, and everything on
the far side of it is the same kind of dependency: Fusion, Photon, the Somnium ProSDK, Community Modules, the
`NetworkBridgeEvents` transport. None of them are ours, none of them are stable, and none of them announce a
breaking change as a compile error. The ProSDK ships as opaque DLLs, so a deprecation arrives as a world that does
not work. The Fusion 2.1 migration ([[community-modules#Migration to 4.0 and Fusion 2.1|community-modules → Migration]]) is the case in point: the only game file it broke outright was `WorldBridge`.

The one thing deliberately *not* behind a bridge is Unity itself. `transform`, `Rigidbody` and XRI are not the
unstable dependency here and wrapping them buys nothing.

**A bridge must not re-export the types it fronts.** A bridge that hands an outside type back to its callers has
moved the coupling, not removed it; every caller still names the thing that will change.

## Managers

**A manager that is a pure passthrough is still doing its job.** Being the callsite *is* the job. When behaviour has
to be added later (a guard, a cache, a correction, a log line) it lands in one method. Skip the layer and the same
change means finding every reference across a hundred seeds, plants and points, which is the cost this layer
exists to avoid. A method that forwards one line today is a place reserved, not a wasted one.

**Managers do not speak to Fusion or the SDK.** A manager that names an outside type has become a bridge with a
manager's name on it, and the churn it was meant to absorb passes straight through it. It calls a bridge instead.

Guards: see [[principles#Every function states its own rules|principles → Every function states its own rules]].

## Where the tree does not match this yet

- **`NetworkMessenger` is a serialized field on the crop prefabs** and on several managers, so the messaging
  component is wired all over the component layer. That is our own type now, but no wrapper hides Inspector
  wiring, so a change to it still touches every prefab.
