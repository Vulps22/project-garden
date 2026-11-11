---
aliases:
  - "Climbing"
tags:
  - mechanic
---
# Climbing

Our own climbing, replacing Somnium's. Vulps's design, 2026-11-01.

**Status (2026-11-01):** tested in-world: climbing, standing on a ledge, and catching by touch and with the arm
inside rock. The body capsule switches off without complaint from Somnium. The haptics shipped since and log
nothing, so they are unconfirmed.

## Why not Somnium's

- **It cannot reach a spawned island.** `SomniumClimbableSurface` is one of Somnium's built-in runtime components:
  it must be in the scene at load and never created at runtime, because the client attaches its implementation
  then (`WorldCreatorContext.md` → Built-in runtime components). Islands stopped being climbable when procgen
  began spawning them, and every spawn was creating one of these components.
- **Ledges were awkward when it did work.** The body capsule snagged on the lip, so getting over the top was a
  fight.

## How it works

`ClimbController` lives on SceneManager beside `FlightController`. It is entirely local: it moves the local
player's own `Root`, and Somnium replicates the avatar.

- **What is climbable:** any collider under a `Climbable` marker: both island prefabs and the Garden's
  `RockFormation`. Ours, so it works on islands spawned at runtime.
- **Taking hold:** the grip button, read straight from the controller as the boost button is. A grabbable object
  in reach wins: a seed against a rock is a seed. Three tries, nearest first, and the hold goes where the first
  one finds rock:
  1. **Touch:** the hand within `_grabRadius` (12 cm) of a climbable collider.
  2. **Arm inside rock:** a ray from head to hand hits rock before the hand, so the hand has sunk in. The hold is
     where the arm enters.
  3. **Swept through rock:** a ray from where the hand was last frame to where it is now hits rock, so a fall
     carried the rock through the hand between frames. The hold is where the path crossed.

  The island colliders are not convex, and Unity can only answer "is this point inside?" or "what is the nearest
  surface point?" for convex ones; a ray hits a non-convex skin fine, and where the arm or the path went in is the
  edge that was meant. The climb then pulls the body until the hand is back on that point.
- **Catching** (a defined term, Vulps 2026-11-01: grabbing to climb while falling, as the hand crosses into a
  climbable surface): off the ground (nothing within `_groundCheck`, 0.3 m, under the feet), a held grip runs the same
  three tries every frame, so reaching out with grip held catches the first rock the hand crosses. On the ground
  only a fresh press grabs, so walking past a rock with grip held does not.
- **A hand that is carrying something never takes hold** (Vulps, 2026-11-01; compiles clean, untested in-world).
  A seed in hand used to catch the wall on a fall, and letting go to free the hand dropped the seed. A hand counts
  as carrying when one of Somnium's XR interactors nearer that hand than the other has a selection; the interactors
  are found once per rig, and `FindInteractors()` logs them with the hand each was matched to. A carrying hand gets
  no reach buzz either. The other hand still climbs.
- **Haptics:** a short, light buzz on a free hand as it comes within reach of a hold (any of the three tries
  would succeed), and a longer, stronger one when it takes hold. Strengths and lengths are
  `_reachAmplitude`/`_reachSeconds` and `_grabAmplitude`/`_grabSeconds`.
- **Holding:** the grabbed point is stored on the collider. Each frame the body moves so the hand stays on it,
  so pulling the hand down lifts the player. The most recent hand to grab is the one that pulls; letting go with
  it hands the climb to the other hand if that one is holding.
- **While climbing:** Somnium's gravity and walking are zeroed, and the body capsule (`CharacterController`, the
  only body collider a player has; avatars carry none) is switched off, so ledges cannot snag it. Flight lands
  when a hand takes hold, and stays out until the last hand lets go.
- **Letting go:** a ray goes down from the head (`_standRayLength`, 2.5 m). If it finds ground flat enough
  (`_standMinNormalY`, 0.6) and the feet are below it, the feet are put on it: topping out leaves the body under
  the ledge, and the capsule coming back on there would push the player off rather than up. Otherwise they fall,
  and can fly from the fall. Then the capsule, gravity and walking are handed back.

## To watch for in-world

- **The capsule switched off under Somnium.** If their controller keeps calling `Move` on it, the log fills with
  a warning per frame while climbing. If so, the capsule needs another way out of the way.
- **Grip is also Somnium's grab.** The seed check covers our own grabbables; anything else of theirs in reach of
  a rock is untested.
- **2D players climb without hands**, below.

## 2D players

**Status (2026-11-11):** built, compiles clean, untested in-world. Decided by Vulps 2026-11-09.

As in established games: **walking into climbable rock grabs on by itself**, with no key
(`ClimbController.Desktop.cs`). The rest is my design, for Vulps to confirm.

- **Grabbing:** a ray from the body capsule's centre along the walking keys (WASD, the arrows or the left stick,
  relative to the camera; listened to, not bound) finds climbable rock within `_desktopReach` (0.35 m) past the
  capsule. It must be steep (normal under `_standMinNormalY`), faced squarely (`_desktopPushDot`, 0.7), and pressed
  into for `_desktopPushSeconds` (0.15 s), so brushing past or walking up a slope does not grab. In the air too, so a
  fall or a flight into a cliff catches it.
- **On the face:** the same handover as VR (flight lands, gravity, walking and the capsule off). Forward and back on
  the keys climb up and down, left and right go across, at `_desktopClimbSpeed` (2 m/s). Each frame a ray finds the
  face again and the body is held `_desktopHoldGap` (5 cm) off it, following it round as it curves.
- **Getting off:**
  - **Topping out:** climbing up past the lip, where the ray finds no rock, steps the body forward over it and lets
    go, and the ledge stand puts the feet on top.
  - **Climbing down** to flat ground stands.
  - **Jump** lets go and falls; so does going off the side of the face.
  - After letting go, the rock cannot be grabbed again for `_desktopRegrabDelay` (0.5 s).
