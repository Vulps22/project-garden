---
aliases:
  - "Entity components"
  - "Behaviour components"
tags:
  - tech
  - interaction
---
# Entity components

The small components that give an object its physical and interaction behaviour. Each one knows nothing about
crops: it is resolved through a one-member interface, so a watering can or a sword could carry the same set.
What the crops supply them is in [[crops]].

**Status (2026-09-13):** built and tested in-world. Code: `GrowAGarden/Motion/` and `GrowAGarden/Interaction/`.

They live on **`Seed` and `Produce` only**, the two things a hand can reach. A `Plant` is never touched, so it
carries none of them except `AuthorityController`, because a plant does belong to the world.

| Component | Owns | Talks to |
|---|---|---|
| `KinematicController` | the *only* writer of `Rigidbody.isKinematic` / `useGravity` | `IKinematicSource` |
| `AuthorityController` | returning state authority to the master for unheld world-owned objects | `IAuthoritySource` |
| `SingleHolderFilter` | refusing a grab when someone else holds it (`IXRSelectFilter`) | `IHeldObject` |
| `ReturnableEntity` | travelling back to a target position when recalled | `IHeldObject` |
| `AlignableEntity` | turning back to a stored target rotation when realigned | `IHeldObject` |
| `HoveringEntity` | falling, landing, rising to hover, bobbing, spinning, righting | told explicitly |
| `SellableEntity` | value, whether it can be sold, and what a sale does to it | `ISellValueSource` |

They re-evaluate on `ILifecycleNotifier.LifecycleChanged`, which `Seed` and `Produce` raise after every lifecycle
transition and on every `grabber` message. The event deliberately carries **no payload**: subscribers read what
they need from the source, so a new behaviour needs no interface change.

`Seed` and `Produce` supply the answers: `ShouldBeKinematic`, `ShouldUseGravity`, `ShouldMasterOwn`, `HolderId`,
`IsHeld`. Each is *derived* rather than remembered.

## Design notes

- **A produce hovers only once harvested.** Until a player takes it, it is attached to a plant or sitting in the
  earth and must not drift, so `HoveringEntity` stays off. That is [[principles#^idle-means-idle|idle means idle]]
  applied at the right seam, and it means a socket never fights a hovering body for position.
- **`ReturnableEntity` and `AlignableEntity` are deliberately separate**: coming back to a place and facing a
  particular way are different wants. `AlignableEntity`'s target is a `Quaternion` snapshot, not a live
  `Transform`, so it goes stale if whatever defined "upright" moves.
- **`HoveringEntity` rights its own up vector while spinning**, because the spin is what creates the conflict. It
  uses up-vector alignment rather than per-axis euler flags, since rotations don't decompose into independent axes
  past ~90°.
- **`SellableEntity` is a trait you add**, not an interface the class has to admit. Its design is in
  [[crops]] §2a.

## Tuning

Hover, fall and settle values are serialized on the prefabs, so tuning feel needs no code change: hover height
0.25, rise 0.6 m/s, fall gravity scale 0.35, max fall 2.5 m/s, spin 20°/s.
