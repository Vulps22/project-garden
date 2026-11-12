---
aliases:
  - "The trip"
  - "Trip"
tags:
  - mechanic
  - interaction
---
# The trip

One local trip with a strength, for the player who has eaten a weed bloom or smoked a splif ([[weed]] starts it).
Colours, a melting world, things in the corner of the eye, and on the intense trip, flipped controls. Everything
here is on the tripping player's client only. Code: `Trip/`.

## The design

**Status (2026-11-01):** proposed by Vulps 2026-10-31; built (see *How it is built*).

- **One trip system with an intensity value**, not two: the weed's mellow and intense trips are two strengths of it
  ([[weed#The design|weed → The design]]).
- **Colour:** a URP Volume enabled only on the tripping client, slowly cycling hue shift, saturation and colour
  filter. No flashing: Somnium forbids seizure-risk effects, so changes stay slow and smooth.
- **Droop and wobble:** a full-screen distortion needs a renderer feature or the opaque texture, both owned by
  Somnium's client, and lens distortion is generally unsupported in XR. What the world can do is make **our own**
  materials droop and wobble from one global shader value: islands, crops and the Garden melt while Somnium's avatars
  and UI do not.
- **Things in the corner of your eye:** no eye-tracking API, so head direction stands in for gaze. Things spawn
  locally 40–60° off head-forward; when one comes within about 15° of centre it dissolves into a swirl of colour.
- **Comfort:** eased in and out, and *Allow Stoned Effects* turns it off. There is no way to end one early (Vulps,
  2026-11-11: covering the eyes did not work, and it was dropped). Whole-view warping is the
  quickest route to VR sickness, which is what the intense trip is for and the mellow one must avoid.
- **Somnium's content rules do not object** (Vulps, 2026-11-01). Players are all ID-verified adults.

## How it is built

**Status (2026-11-01):** built and played in-world. The logs show the colour trip, the world melting and coming back,
and control flips (28 on foot, 7 flying). The visions log nothing and are not confirmed by the log.

- **`TripManager`** (on SceneManager): one local trip with a strength. A global `TripVolume` (priority 10, weight 0
  at rest) whose profile `Models/Weed/Trip.asset` is the trip at full strength: `ColorAdjustments` (hue cycling ±180°
  over 40 s, saturation +45, contrast +15), `ChromaticAberration` 0.6, `Vignette` 0.3. The trip eases in over 12 s and
  out over 20 s, and its strength is the volume's weight.
  Needs `Unity.RenderPipelines.Core.Runtime`, which is on Somnium's allowed list.
- **Melting** (`Vulps/TripLit`, `Shaders/TripLit.shader` + `TripWobble.hlsl`): URP Lit, copied, with every vertex
  pass that draws the world wrapped to sag and wobble by the global `_GAG_Trip`, which only the tripping player's
  `TripManager` sets (to the trip's level squared, so the mild high barely stirs and the intense one melts). At 0 it
  is identical to Lit. `Materials/Trip/` holds a twin of each world material (ground, rocks, crops, fences, weed);
  `TripManager` swaps renderers to their twins only while tripping, every 2 s to catch new spawns, and swaps them all
  back when it ends (`TripManager.MaterialsSwapped`). Prefabs are untouched, and nobody pays for the shader at rest.
  Amounts are in object space, so a carrot wobbles a little and a great rock a lot. A new crop's materials need trip
  twins ([[crops#Seed definitions|crops → Seed definitions]]).
- **The hut** (`ShowLit`, which melts the same way, [[light-show]]) is modelled tiny and scaled ×109, so it does not
  wobble but **twists and smears like an apparition**: a bug Vulps kept as a feature (2026-11-03). Do not "fix" it.
- **Visions** (`TripVisions` on SceneManager): from strength 0.5. A carrot, pumpkin, turnip, bloom, sprout or splif
  appears 40–60° off where the head points, 2.5–4 m away, every 9 s at the threshold down to 3 s at full strength, at
  most 6 at once. It fades in, spins and drifts; looked at (within 15° of where the head points) it dissolves into a
  rainbow swirl (`TripSwirl`). Unseen ones fade after 20 s.
- **Flipped controls** (Vulps, 2026-11-01; played in-world): only an **eaten** trip, at strength 0.8 and up, flips
  the controls for 0.5 s every 6–15 s. Flight is ours, so `FlightController` negates pitch and turning while
  `TripManager.ControlsFlipped`. Walking is Somnium's and the bridge only scales its speed, so walking is flipped by
  setting the speed to −1 for the moment. Somnium accepts the call; whether walking actually reverses is for Vulps to
  say, since the log only shows the speed being set. The `Flip()` line says which was flipped. Flight and climbing own
  the speed while they run, so walking is only flipped on the ground and only handed back if neither has taken it
  since.
- **A player can turn it all off**: World Settings → *Allow Stoned Effects*
  ([[world-settings#The switches|world-settings → The switches]]). Off, no trip starts and one running ends (over
  4 s); eating and smoking still happen, they just do nothing to this player.

> [!info] Networking
> The trip is local and sends nothing. What starts it (eating, a puff) is the weed's ([[weed]]).
