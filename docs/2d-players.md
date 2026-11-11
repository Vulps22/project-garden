---
aliases:
  - "2D players"
  - "PC mode"
tags:
  - interaction
---
# 2D players

Players on a desktop: keyboard and mouse, or a gamepad. Somnium calls it **PC mode**, and its API calls it
**NonXR**. Every VR interaction in the game needs a 2D equivalent (Vulps, 2026-11-09). Somnium requires the same:
"every world must work in VR and PC mode" (`WorldCreatorContext.md` §0).

**Status (2026-11-11):** nothing but flight and climbing works in 2D (Vulps, in-world), so the 2D interface is built
from scratch, to the design below (Vulps, 2026-11-11). Flight and climbing are built and untested since their fixes.
Keyboard and mouse only: **a gamepad is out of scope** (Vulps). The tutorial ignores 2D players until this pass is
done ([[tutorial]]).

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

**Status (2026-11-11):** decided (Vulps); flight and climbing built, the rest not.

| Key | Does |
|---|---|
| **K** | The interact key: whatever the prompt says (below) |
| **1**, **2** | Select the item in that hand |
| **Z** | Eat the selected item if it is edible; put a selected splif in your mouth |
| Space | Jump; a double jump takes off; in flight a jump boosts height and a double jump ends flight ([[flight#2D players]]) |
| W / S in flight | Forward flies; back brakes to a hover without ending flight ([[flight#2D players]]) |
| Walking into rock | Climbs it ([[climbing#2D players]]) |
| The mouse on the tablet | The tablet's own point and click, as it is |

## Interacting

**Status (2026-11-11):** decided (Vulps), not built.

- **The prompt.** Walking up to something the player can act on highlights it blue and puts up "Press K to …":
  **Grab**, **Buy**, **Harvest**, **Plant**, **Sell**, **Claim**, or **Drop** with nothing in front of them. The
  target is what they are looking at within reach. The highlight's shader is referenced by the script driving the 2D
  controls, so the upload keeps it.
- **Carrying: two hands.** A 2D-only HUD in the bottom left: two translucent white squares, one per hand, showing
  what each holds with the same tiles as the Almanac and the Developer Market. What a 2D player grabs or buys is
  locked to one of **two sockets above their head**, so everyone can see what they carry. Each client puts a carried
  item on its holder's socket itself, from the holder already recorded on the item, so nothing more is sent.
- **Selecting and dropping.** 1 and 2 select a hand; K with nothing to act on drops the selected item.
- **Hands full.** Grabbing or buying with both hands full sends the player a notification that their hands are
  full. In 2D, notifications show as HUD toasts ([[notifier]]).
- **Plant:** the slot looked at, which the planting beam marks ([[planting-beam]]). K plants the selected seed there.
- **Sell:** K at the sell point sells the selected item only; the selection then moves to the remaining item.
- **Claim a plot:** K at the hut with the deed selected.
- **Eat:** Z on a selected edible item. **Smoke:** Z puts the selected splif in the mouth, and the smoking's own 5 s
  dwell then drives the puffs as in VR ([[weed#Eating and smoking]]).
- **The tablet** keeps its own design language, point and click. The apps did not exist when 2D was tried, so where
  the 2D tablet is and what it is called is found by probe first: `TabletProbe.Search()` logs, for Vulps only, every
  object anywhere named like a tablet, every 3 s until the map is found (compiles clean, not yet read in-world).
- **The Developer Market** drops what it delivers in front of a 2D player.
- **In-world buttons** (`SettingsButton`) take the same prompt.

**Out of the plan** (Vulps, 2026-11-11): teammates (the system is dead), the Grow Wand (it does not work), and
ending a trip early (it did not work, and is gone from the game: [[trip]]).

## The plan

**Status (2026-11-11):** order Vulps's; one phase per upload ([[testing]]).

1. **Flight and climbing** (built): [[flight#2D players]] and [[climbing#2D players]], on the foundation:
   - `PlayerBridge.IsVR`, read once from `XRSettings.enabled`; `PlayerRig.IsUsable` true for a 2D rig without hands.
   - `DesktopControls`: the keys, listened to.
   - `PlayerBridge.TryGetLocalBody` and `PlaceLocalBody` (the body is placed through its Rigidbody in 2D).

   With it, the tablet search above.
2. **Interacting:** the prompt and highlight, the two hands and their HUD, the head sockets, K, 1 and 2. Grab, buy,
   drop, harvest, plant, sell, claim; hands full; notifications as HUD toasts.
3. **Eat and smoke** on Z; the Developer Market's drop in front.
4. **The tablet apps**, wherever the probe finds the 2D tablet.
5. **The tutorial for 2D players.** Each step gets its own 2D instruction and voice line where the action differs.
   Then mark "2D players are not supported" gone from [[tutorial]].

Each feature's own doc takes its 2D half as that phase lands, and this doc keeps only what is still to do.
