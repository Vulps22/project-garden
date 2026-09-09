# Flight

The step doc for the custom flight that replaces Somnium's. Vulps's design; the engineering notes
are mine and are flagged as such.

Written 2026-09-09 on `add-advanced-flight`. **This is [`procgen-islands.md`](procgen-islands.md)'s
blocker** — the world's usable radius is a function of how a player moves through it, so the map
cannot be sized until this exists. Links back to [`roadmap.md`](roadmap.md)'s exploration steps.

---

## Why not Somnium's

**Somnium's fly** is a toggle: on, then follow the head to rise and descend. **Somnium's glide** is
arms out, constant forward speed, constant descent, rotate with the controller joysticks —
independent of head tilt and of where the hands actually are.

Neither is a foundation. The only thing either shares with what this wants is that it moves you at
all, and glide in particular has **no stop**, which the exploration loop needs: you arrive at an
island and you have to be able to hold still and read what the buy point is offering.

Both are disabled through the bridge, per-zone if wanted.

## The feel this is aiming at

Two words, and they are the point rather than decoration:

**Satisfying.** Every input should have weight and an obvious physical consequence. Diving should
feel fast and slightly dangerous; climbing should feel like it costs you something, because it does;
the flap should land like a thump in the chest. This is the mechanic players will spend the most
minutes inside, and it is the one that decides whether the world is a place worth crossing or a
loading screen with scenery.

**Relaxed.** The world's whole energy is a garden. There is no stamina bar, no fuel, no failure
state, and nothing to fight — the resting pose sinks gently rather than dropping you, and being
stationary is a *pose*, not a button you hold. A player should be able to stop paying attention for
ten seconds and not be punished for it.

Those two pull against each other exactly once, at the stall, and that tension is deliberate — see
below.

---

## The model

An energy-conserving glider. One scalar `speed` along the body's facing, plus altitude, and the
hands set the pitch that trades one for the other.

**θ** = wrist pitch from horizon, averaged across both hands. The wrists are front-facing control
surfaces: the *angle* matters, not how high the arms are raised.

| θ | forward | vertical |
|---|---|---|
| **≥ ~60°** | decays to 0 — stalled | −0.5 m/s, the park |
| **~40°** | bleeding | best climb |
| **0°** | equilibrium cruise | gentle sink |
| **−45°** | building | dive |

```
dSpeed/dt  = −g·sin(pitch) − drag(speed)
verticalV  =  speed·sin(pitch) − sink(speed)
```

**Level flight still costs height.** At θ = 0 you hold a constant forward speed and sink slowly,
because the sink is what pays for drag. That is what a real glider does and it is what makes
altitude feel like a resource.

**Climbing bleeds speed. Diving builds it.** No exceptions, no assists. This is the part that makes
the model readable without a single instrument: how fast you are going *is* how much height you have
banked.

**Yaw** is the hands in opposition — left down, right up, turn left:

```
yawRate = clamp((rightθ − leftθ) × k, ±max)
```

with a ~5° dead zone, so hands never being quite level doesn't cause a slow spin. **No roll in this
pass**, and no roll-to-turn; yaw is direct.

**Advanced flight**, toggled at the hut, is this same model with the clamps removed — loops, and
later barrel rolls. It is one boolean over one system, not a second flight model.

### The stall

Pull up too hard and you bleed all your speed, the hands stall out, and you drop into the park state
until you dive to recover. That is a real failure mode with a real recovery, and it is what makes
climbing a skill rather than a direction.

It is also the one place the design is deliberately not relaxed, and that is fine — because the
punishment for stalling is that you sink gently at half a metre a second, which is the same thing
that happens when you deliberately stop. **The worst outcome in the flight model is the resting
pose.** That is how you get tension without stress.

### The flap

**Once between leaving the ground and touching it again**, a player can flap their arms and throw
themselves upward — starting figure **20 m**, to be tuned.

It should be earned by the gesture rather than triggered by a pose: both hands sweeping down fast,
past a velocity threshold. And it should be the most physical moment in the whole game — anticipation,
impulse, and a recovery you can feel, with wind noise and a brief pull on the FOV.

**Once** is what makes it precious. A player will save it, and deciding when to spend it is the only
real decision the flight model asks of them.

**And it resets on landing.** Which turns out to be the mechanic that makes the world crossable —
see below.

---

## Engineering notes

These are mine, not design decisions.

### The bridge gives us everything we need, and I was wrong that it didn't

```
Assets/Plugins/SomniumSpace/Bridge/SomniumSpace.Bridge.dll     the assembly
Assets/Plugins/SomniumSpace/Bridge/SomniumSpace.Bridge.xml     195 KB, 658 documented members
```

```
SomniumBridge.PlayersContainer.LocalPlayer
  .References.Body   →  Transform Root, Head, Neck, LeftHand, RightHand
  .Features.Motion   →  SetGravity, SetCollisionDisableState,
                        SetFlyModeDisableState, SetGlideDisableState,
                        SetMovementSpeed, SetGlideSpeed, DoTeleportToPoint
```

**Real `Transform` references, handed over by the documented API.** Hand pitch is
`LeftHand`/`RightHand` rotation; moving the player is writing `Root`. An earlier read of this called
the whole approach off-contract, on the strength of `ISomniumPlayerMotion` alone — that was wrong,
and `References.Body` is the thing that was missed. `PlayerManager.GetLocalPlayer()` already returns
the `ISomniumPlayer` this hangs off, so the path exists in the project today.

### ⚠ The Editor cannot test this

`SomniumSpace.SDK.Controllers.PlayerController` — the rig visible in this project, with
`_characterController`, `_gravity`, `_velocity`, `_respawnHeight` — carries a
`WorldTestingStateType` field and sits beside the only prefab in `Assets/SomniumSpace/Prefabs`,
`WorldTestingManager.prefab`. **It is the SDK's world-testing stand-in, not the player that exists in
the Somnium client.** In-world you get Somnium's own networked rig.

So Play mode flies the wrong rig, and flight is upload-and-test from the first line — the slowest
loop in the project. **Build the instrumentation before the mechanic.** This session lost hours to
`NetworkGrabbable` logging success on failure; a flight system that cannot say what it did to `Root`
and what `Root` looked like on the next frame will cost far more than that.

### The first upload should not fly

Disable Somnium's fly and glide, disable collision and zero gravity through `Motion`, resolve `Root`
from the bridge, write a fixed small offset every frame, and log the position before and after. If
`Root` moves and stays moved, everything after is design work. If something writes it back, that is
the whole problem found in one trip instead of six.

If it *is* written back, `DoTeleportToPoint` at frame rate is the fallback, and it is probably
unusable — it is a teleport API and likely fades or resolves collision per call — but it is an
afternoon to rule out.

### Nothing about this is networked

The player moves their own `Root`; Somnium replicates the avatar as it always has. No RPC, no master
decision, no facts, no late-join. It is the cheapest system in the whole design and the only one that
can be built without thinking about authority at all.

### ⚠ Altitude is strictly spent — and the flap is what fixes it

With energy conservation and no lift source, every metre of height is either dived for or gone. Dive
and zoom recovers some, but drag takes a cut each cycle, so a long crossing nets downward.

**The flap resetting on landing is what closes this.** Land on an island, get the flap back, launch
again — so islands are altitude refills and the world is crossed by hopping rather than by one long
glide. That is a much better exploration loop than thermals would give, it needs no new mechanic, and
it makes landing on an island something a player wants to do rather than a thing they tolerate.

It also produces the one number that ties this doc to [`procgen-islands.md`](procgen-islands.md):

> **flap height × glide ratio = the island spacing ceiling.**

At 20 m and 12:1 that is **240 m**. Space islands further apart than that and hopping stops working,
and the world needs thermals or a bigger flap instead. Space them closer and every island is
comfortably reachable from the last. **This is the constraint that unblocks procgen's radius
question** — spacing and flap height are one decision, not two.

### Starting numbers

| | value | why |
|---|---|---|
| best glide speed | 18 m/s | ~65 km/h. Fast enough to read as flight, slow enough that an island is a decision rather than an overshoot |
| sink at best glide | 1.5 m/s | gives **12:1** — from 1 km up you cross 12 km |
| park sink | 0.5 m/s | Vulps's figure. Not a hover, but reads as one |
| stall angle | ~60° | Vulps's figure |
| flap | 20 m | starting guess, expected to move |
| yaw dead zone | 5° | hands are never level |

All of it is expected to change in a headset. Nothing about feel survives contact with VR, and the
numbers above exist so there is something to react to rather than because they are right.

---

## First in-world run, 2026-09-09

Flown once from `SceneManager`. Everything below is what the log said rather than what it felt like,
except where noted.

**The bridge route works.** `Root` resolved to `XR.Body`, hands to `LeftHandAnchor` /
`RightHandAnchor`, and writes to `Root` stick. The route was never in doubt after that.

**`forward` is the right hand axis.** Left and right agree on it (`54/56`, `62/62`, `57/67`) while
`right` mirrors between hands (`L -85 / R +58`) — mirroring is the signature of an axis pointing out
of the body, agreement is the signature of one pointing along the arm.

**But Somnium's hand anchors are not level when an arm is level.** Arms out read **54-69 degrees**,
which parked the player above the 60 degree stall angle: `speed=0.0` on eight of twenty reports,
before they had done anything wrong. The angle is therefore **calibrated from the launch pose**
rather than assumed, which sidesteps the rig convention entirely.

**Flying backwards was `Root`, not the hands.** Movement followed `_root.forward`, and `XR.Body`'s
forward is not where the player is looking. It follows the **head**, flattened, now.

**The bounce on every landing was the flap.** `IsGrounded` re-armed it on every grounded frame and
2.5 m/s of downward hand movement is ordinary walking, so a landing armed and instantly spent it.
Threshold raised to 4 m/s, plus an arm delay, plus arming only on the ground-to-air transition.

**Disabling fly and glide does not disable walking.** The joystick still moved the player and fought
the model. `SetMovementSpeed(0, 0, 0)` while flying, restored on landing.

**Somnium already respawns a player who falls too far.** One `Root moved 35.77m` entry, from
y=-21 back to spawn — `PlayerController._respawnHeight` doing its job. That answers
[`procgen-islands.md`](procgen-islands.md)'s open question about the trash-can floor and players:
nothing needs building.

**⚠ Unresolved: something still pulls Y down about 0.06 m every frame**, X and Z untouched, despite
`SetGravity(0, 0)`. Steady rather than bursty, so it reads more like a character controller step than
gravity. The instrumentation now reports drift as an average, a peak and a count per interval instead
of per frame, which should say what it is on the next run.

**And the first instrumentation was itself a bug** — edge-triggered on a signal that alternated every
frame, so it produced a WARN/INFO pair per frame: precisely the per-frame spam it was written to
avoid. Worth remembering that "log only on change" is only cheap when the thing actually changes
rarely.

---

## Open questions

1. **Does `Root` stay written?** The probe above. Everything is downstream.
2. **Is the flap a height impulse or a speed impulse?** Height is more readable and more satisfying;
   speed is more consistent with the energy model. Height, unless it feels wrong.
3. **How is the flap gesture detected**, and does it false-fire while carrying something? A player
   with a seed in each hand still has to be able to flap.
4. **Is there a launch at the Garden**, or does the first flap serve? A launch makes the outbound
   trip free and might make the flap feel less precious.
5. **Does flight need engaging**, or are you flying whenever you are off the ground?
6. **Advanced flight's clamps** — which ones come off, and does yaw become roll-to-turn when they do?
7. **The terrain mask has to stop including Default before seeds exist again.** Fine while the
   model is being tuned in an empty world, and left wide on purpose for that. But `Seed_*`,
   `Produce_*`, `Deed` and `Collectible_BlankDeedScroll` are all `m_Layer: 0`, and a carried one
   sits inside the contact sphere — so once crops are back, carrying anything would suspend flight
   permanently, which is precisely the loop the design exists for. Two fixes: move island geometry
   to `Terrain` (hundreds of scene objects, and the prefab assets too or spawned islands land on
   Default), or move the eleven carryable prefabs to layer 7 `PhysicalObject`, which is where a
   carryable rigidbody arguably belongs and gets procgen right for free. Note the collision-matrix
   export trap does *not* apply — `_terrainMask` is a serialized int on our own component, so it
   travels; only ProjectSettings' layer *collision rules* are lost.
8. **⚠ What happens when an inverted player touches terrain and Somnium takes over?** Advanced-only
   by construction: basic mode writes only `position` and a yaw `Rotate`, so `Root` cannot leave
   upright. Once loops exist it can, and handing back to Somnium mid-loop has three possible
   outcomes — it rights the player smoothly, it snaps them upright, or it leaves them inverted and
   everything downstream (walking, gravity, the camera) is wrong. Only a test will say which.
   If it snaps or does nothing, the fix belongs here rather than in the SDK: tween `Root` back to
   upright over a few tenths of a second on leaving flight. It has to be a tween — a snap rotation
   in a headset is genuinely unpleasant, and this one would fire on every landing after a roll.
