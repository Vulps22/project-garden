---
aliases:
  - "2D players"
  - "PC mode"
tags:
  - interaction
---
# 2D players

Players on a desktop, with keyboard and mouse. Somnium calls it **PC mode**, and its API calls it
**NonXR**. Every VR interaction in the game needs a 2D equivalent (Vulps, 2026-11-09). Somnium requires the same:
"every world must work in VR and PC mode" (`WorldCreatorContext.md` §0).

**Status (2026-11-11):** the whole 2D interface is built to Vulps's design (Vulps, 2026-11-11), for one upload:
flight, climbing, the hands, K, 1, 2, Z, the HUD, toasts, the tablet and the tutorial. Compiles clean, untested
in-world. Keyboard and mouse only: **a gamepad is out of scope** (Vulps).

## What Somnium gives a 2D player

From `WorldCreatorContext.md`. These are the platform's own statements, not tested by us.

- **No hands.** `Body.LeftHand`, `Body.RightHand` and `Body.Neck` are `null`. `Body.Head` is a point at head
  height. `Body.LeftHand != null` is the VR test (§6).
- **No hand colliders.** Every "touch" interaction needs a click, a trigger volume or a grab instead (§8).
- **XRI grabs work in PC mode** ("prefer XRI interactions… they work in VR and PC", §9). How a held item sits,
  turns, drops and is thrown is **not described**.
- **Mouse clicks on 3D objects:** the PC camera has a `PhysicsRaycaster`. `IPointerClickHandler` and
  `IPointerEnterHandler` on a collider receive the mouse, only on the layers Default, TransparentFX, Water, UI, 8,
  Terrain and BuildingItem (§9). The cursor is free; right mouse looks.
- **World-space canvases** need a `GraphicRaycaster` for PC beside the `TrackedDeviceGraphicRaycaster` (§9).
- **The camera can be third person.** For "where the player looks from", use `Body.Head` when `Camera.main` is
  more than about 0.75 m from it (§9).
- **The client's keys:** W A S D, the arrows, Space, Shift, Tab (tablet), Q/E, M, I, F (interact), R, X, Esc, the
  mouse buttons and the wheel. **Keys found free:** Z, Y, U, O, P, K, L, N and the digits. Our own Input Actions
  asset is allowed, bound for both modes (§9). We read keys straight off the keyboard instead (`DesktopControls`),
  which listens without binding, so the client still hears its own.
- `SetHeight` ignores its PC height, so the 2D body is a fixed size (§6).

## The controls

**Status (2026-11-11):** built (Vulps's design); compiles clean, untested in-world.

| Key | Does |
|---|---|
| **K** | The interact key: whatever the prompt says (below) |
| **1**, **2** | Select the item in that hand |
| **Z** | Eat the selected item if it is edible; put a selected splif to the mouth, or back up |
| Space | Jump; a double jump takes off; in flight a jump boosts height and a double jump ends flight ([[flight#2D players]]) |
| W / S in flight | Forward flies; back brakes to a hover without ending flight ([[flight#2D players]]) |
| Walking into rock | Climbs it ([[climbing#2D players]]) |
| Tab, then the mouse | The tablet, the client's own, point and click |

## Interacting

**Status (2026-11-11):** built; compiles clean, untested in-world. `Desktop/DesktopInteraction` (the 2D controls
script) does all of it, and does nothing for a VR player.

- **The prompt.** The first thing on the line of sight, within 3 m of the head, that K would act on is highlighted
  blue and "Press K to …" shows at the bottom of the screen: **Grab**, **Buy** (a seed in a stall), **Harvest** (a
  produce still on its plant), **Plant**, **Sell**, **Claim**, **Use** (an in-world `SettingsButton`), or **Drop**
  with nothing else to act on. Solid things hide what is behind them; triggers do not. A sell point or a hut the
  player stands inside counts too, so they need not look at it. The highlight is a shell with the tutorial glow's
  shader, in blue (`Materials/Desktop/DesktopHighlight`), referenced by the script so the upload keeps it.
- **Two hands, above the head.** Each hand is an XRI interactor of our own (`HandSocket`) on a socket above the
  head, left and right. Taking something is a real XRI select by it, so everything that hears a grab works
  unchanged: the holder, a stall's purchase as the seed leaves it, a harvest, `NetworkGrabbable` taking authority.
  The holder simulates what it holds, so everyone sees it over their head, as they see a VR player's hands.
- **The HUD.** Bottom left, two translucent white squares, the selected one brighter, each showing its item with
  the Developer Market's tile for it, else its crop's Almanac picture, else its name.
- **Selecting and dropping.** 1 and 2 select a hand. Taking goes into the selected hand, else the other; with both
  full, a notification says "Your hands are full". K with nothing to act on puts the selected item down in front of
  the player.
- **Putting things in.** Sell, Claim and the tutorial's zones are `IItemReceiver`s: K carries the selected item there
  and hands it over exactly as a hand carrying it into the trigger would. A sell sells the selected item only; when a
  selected hand empties, the selection moves to the other.
- **Plant:** the free slot of the dirt block looked at, nearest where the look lands, which the planting beam marks
  ([[planting-beam]]). K carries the seed there and asks the slot, as touching it does.
- **Eat and smoke:** Z takes a selected edible to the mouth, where the edible's own dwell eats it. Z takes a splif to
  the mouth, where the smoking's own 5 s dwell drives the puffs as in VR ([[weed#Eating and smoking]]); Z again puts
  it back up.
- **Notifications** show as HUD toasts at the top of the screen ([[notifier]]).
- **The tablet** is the client's own, point and click. In PC mode it is not under the body, so `TabletBridge`
  looks for it across the scene by name (the same `Tablet.XR(Clone)`, else any `Tablet.*(Clone)` with `Canvases`)
  and the apps, map and settings page use whatever it finds. `TabletProbe.Search()` still logs, for Vulps only,
  every object named like a tablet, so a 2D tablet built differently inside shows up in the log. Build 11 found it
  (`Tablet.NonXR(Clone)` under `NonXR.Body`, laid out inside like the VR one) and the apps were built on it, so the probe
  is no longer called: it walked the whole tablet every second, a suspect for the freezes in that session.
- **The Developer Market** in 2D: a mouse click on a cell puts the item on the ground in front of the player
  ([[dev-tools]]).

**Out of the plan** (Vulps, 2026-11-11): teammates (the system is dead), the Grow Wand (it does not work), and
ending a trip early (it did not work, and is gone from the game: [[trip]]).

## The plan

**Status (2026-11-11):** all five phases built for one upload (Vulps: "ONE UPLOAD"); compiles clean, untested
in-world.

1. **Flight and climbing:** [[flight#2D players]] and [[climbing#2D players]], on `PlayerBridge.IsVR`,
   `PlayerRig.IsUsable`, `DesktopControls` (the keys, listened to) and `PlaceLocalBody` (the body is placed through its
   Rigidbody in 2D).
2. **Interacting:** above.
3. **Eat and smoke** on Z; the Developer Market's drop in front.
4. **The tablet apps**, through the scene-wide tablet search.
5. **The tutorial for 2D players:** [[tutorial#2D players]].
