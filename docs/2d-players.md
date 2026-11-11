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

**Status (2026-11-11):** phase 1 (flight and climbing) built, compiles clean, untested in-world; the rest planned. The
tutorial ignores 2D players until this pass is done ([[tutorial]]).

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
  asset is allowed, bound for both modes (§9).
- `SetHeight` ignores its PC height, so the 2D body is a fixed size (§6).

## Every VR interaction, and its 2D equivalent

**Status (2026-11-09):** proposed. "Unknown" rows wait on the probe. Each proposed control is a question for
Vulps, not a decision.

| Feature | In VR | In 2D today | Proposed 2D equivalent |
|---|---|---|---|
| Pick up, carry, drop (seeds, produce, deed, application, splif) | XRI grab, near or far | Unknown: XRI grab should work, but where the item sits is not known | Somnium's own grab if the probe says it works |
| Buy from a stall, take from a dispenser | Grab the seed out of its socket | Unknown: the same grab | Nothing new, if grabbing works |
| Plant | Carry the seed into a slot's trigger (`Seed.OnTriggerEnter`) | Probably broken: the held item may never reach the soil | Holding a seed, click a free slot to plant it: the same `PlantSlot.RequestPlant` |
| Harvest | Grab the produce off the plant | Unknown: the same grab | Nothing new, if grabbing works |
| Sell | Carry the produce into the barrels' trigger (`SellPoint.OnTriggerEnter`) | Unknown: depends where the held item sits | Holding produce, click the sell point: the same sell request |
| Claim a plot | Carry the deed into the hut (`DeedClaimZone`) | Probably works: the player walks in | Click the hut while holding the deed, if walking in does not reach the zone |
| Teammate application, accept, revoke | Carry the scroll into a zone (`PlotApplicationZone`, `ApplicationAcceptZone`, `PlotTeammateRevokeZone`) | As for the deed | As for the deed |
| Eat a bloom | Hold it at the mouth 0.12 m for a moment (`EdibleModule`) | Broken: there is no hand to raise | Hold a key while holding it, for the same dwell |
| Smoke a splif | Hold it at the mouth, a puff each dwell (`SmokeableEntity`) | Broken, as eating | The same key, a puff each dwell |
| End a trip early | Both hands over the eyes (`TripManager`) | No way out: the check needs both hands, so a trip runs its full length | Hold a key for the same moment |
| Trip's flipped controls | Walking speed made negative | Should work: it is the speed | Nothing new |
| Fly | Arms out to take off, wrist pitch, hands in opposition to yaw, a flap, a boost button ([[flight]]) | Built, untested: a double jump ([[flight#2D players]]) | Done |
| Climb | Grip near rock, pull down to rise ([[climbing]]) | Built, untested: walking into rock grabs it ([[climbing#2D players]]) | Done |
| Tablet apps: Almanac, World Settings, Developer Market, map overlay | The tablet, pointed at with a hand ray | Unknown: we find the tablet as `Tablet.XR(Clone)`, which may not exist in PC mode | Wherever PC mode's tablet is, if it has one; otherwise a panel of our own |
| Developer Market delivery | Trigger on an item puts it in that hand (`MarketApp.PutInHand`) | Broken: no hand interactor to put it in | Drop it in front of the player, or into PC mode's grab if it has one |
| In-world buttons (`SettingsButton`) | XRI select | Unknown | `IPointerClickHandler` as well, if XRI select does not reach them |
| Toasts, instruction panel | Head-locked in front of the headset | Unknown: third person puts the camera away from the head | Anchor to `Camera.main` in PC mode |
| Grow Wand (developer only) | Touch a plant with the wand | Unknown: the same grab, then a touch that may not reach | Click a plant while holding it; low priority |
| Haptics (climbing, flight) | Controller buzz | Nothing | Nothing |
| Tutorial | Every step above, the tablet step, and a T pose to fly off | Taken up like everyone; Skip is the only way out ([[tutorial]]) | Each step's own 2D line and check; the send-off is the double jump |

## The plan

**Status (2026-11-11):** order set by Vulps; phase 1 built, untested in-world. One phase per upload ([[testing]]).

1. **2D flight and climbing** (built): [[flight#2D players]] and [[climbing#2D players]]. With them, the parts of the
   foundation they use:
   - `PlayerBridge.IsVR`, read once from `XRSettings.enabled`, and logged with the body's name the first time.
   - `PlayerRig.IsUsable` split: a 2D rig is usable without hands, and `HasHands` is what hand readers ask
     (the trip's covered eyes).
   - `DesktopControls`: the jump and walking keys, read straight off the keyboard and gamepad without binding them.
   - `PlayerBridge.TryGetLocalBody`: the body capsule's centre, radius and height.

   The VR flight rework's jump to take off ([[flight#The rework]]) is still not built.
2. **Look at the state of 2D as it stands** (Vulps): play in PC mode with flight and climbing in, and log what the
   rest does:
   - which XRI interactors the PC rig has, and their interaction layers
   - where a held seed sits, relative to `Camera.main` and `Body.Head`
   - whether walking with a held item reaches a plant slot, the sell point and the hut
   - whether the tablet exists and what it is called
   - whether `SettingsButton` and our tablet canvases take the mouse
   - the camera's distance from the head, in first and third person.

   The logging is gated to Vulps, as the tablet probe was. What it shows decides the phases after it.
3. **The rest of the foundation:**
   - an Input Actions asset of our own on the free keys, if any action still needs one
   - an aim helper: one ray from `Camera.main`, saying what is aimed at. It fires an event when that changes; it
     does not poll each consumer.
4. **The core loop in 2D:** buy, plant, harvest, sell, claim, teammates. Only the paths phase 2 shows broken.
5. **Eat, smoke, end a trip.**
6. **The tablet apps** and the Market's delivery.
7. **The tutorial for 2D players.** Each step gets its own 2D instruction and voice line where the action differs.
   The send-off is the double jump. Then mark "2D players are not supported" gone from [[tutorial]].

Each feature's own doc takes its 2D half as that phase lands, and this doc keeps only what is still to do.

## Open questions

**Status (2026-11-09):** open, Vulps's to answer, after phase 2.

- Which keys, where a key is still needed: the free ones are Z, Y, U, O, P, K, L, N and the digits.
- Whether a gamepad is in scope now, or keyboard and mouse only.
- Whether a click on a slot, the sell point or the hut is right for 2D, or whether walking the held item in must
  work as in VR.
