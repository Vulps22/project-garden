---
aliases:
  - "Fun seeds"
tags:
  - crop
---
# Fun seeds

Seeds whose plant is an upgrade to the Garden rather than a crop to sell: the popcorn seed, which brings up the video
screen, and the ARS seeds, the Moonflower's dome and the Spotlight Tulip, whose light show reacts to the screen's audio. Weed came first and
is its own doc ([[weed]]).

## The video player

**Status (2026-11-02):** placed, untested in-world.

**Somnium's video player cannot be spawned.** `SomniumVideoPlayer` is one of Somnium's built-in components: the client
attaches its real implementation (AVPro) when the world loads, and numbers these components by hierarchy order for
syncing, so one must be in the scene from load and never created, deleted or reordered (`WorldCreatorContext.md` →
Built-in runtime components). Somnium's climbable surface failing on spawned islands was the same rule ([[climbing]]).

So the Garden has one player that always exists, and the seed **reveals** it. It is Community Modules'
`ARS Somnium Video Player` in the `GardenIsland` prefab, positioned by Vulps at the Garden's east edge facing the
plots, (10.4, 2.9, 5.0). Autoplay is off; it shipped looping a YouTube link for everyone on load.

- **Who sets the link is Somnium's own rule** (Vulps, 2026-11-02): the native controls let anyone take or release
  control, and we leave that alone.
- **One Garden, one player** for now; several Gardens are a later problem.

## The popcorn seed

**Status (2026-11-02):** first version played in-world (the seed was in the market and stalls; the screen faced away
from the plots). Reworked the same day as below: compiles clean, untested in-world.

Designed by Vulps 2026-11-02, revised the same day:

- **The seed is a kernel**, the unpopped bit (`Seed_Popcorn`).
- **The plant is a real piece of popcorn** growing out of the ground, about 30% buried like a carrot, swelling to
  50 cm while popcorn sprays out of its top, livelier as it grows.
- **The popcorn builds the screen.** When a grown plant takes the screen, its spray stops and a stream of popcorn
  flies from it to the screen, each piece joining the screen's building edge, which rises from the bottom as they
  arrive: nanobots, or programmable matter (Vulps). This replaced a rain of popcorn over the Garden.
- **Permanent for the session.** No lifespan: it holds its PlantSlot until the Plot is cleaned up after its owner
  leaves.
- **A second one may be planted.** It grows and keeps spraying while it waits; only the oldest grown popcorn plant
  holds the screen. When that one goes, the next takes over and its popcorn rebuilds the screen from nothing.

**Pieces:**

- **`StandingPlant`** (`Crops/`): a plant that grows and then stands, bearing nothing and never withering. The fourth
  plant shape, for upgrades ([[crops]]).
- **`PopcornSeed`**: buy 120, nothing to sell, grows in 60 s, `spawnWeight` 0.12. **Not in the shops yet** (Vulps,
  2026-11-02): only in the Developer Market until it is ready; `Buyable_Popcorn` stays under SceneManager, out of
  `WorldManager._buyables`, so listing it is adding it back.
- **`ScreenManager`** (on SceneManager, `Video/`): which grown popcorn plant holds the screen, the oldest by planting
  time (`Holder`). Raises `Assembling(plant)` when a plant takes it in view, and `VisibilityChanged` when it is simply
  up (a late joiner) or gone. Keeps the screen's building edge for the popcorn to aim at; a build takes
  `_buildSeconds` (6 s).
- **`ScreenReveal`** (on the player): **never touches the player's own transform.** It squashes only Somnium's picture
  and its backing (`FrameParent`, `BackQuad`, found once Somnium has attached them), pinned at the bottom edge, and
  switches the control canvases off until the screen is whole. Building, the edge rises linearly over the build time
  and is reported to `ScreenManager` each frame; a takeover rebuilds from nothing. With no holder it sinks over 3 s.
  The player stays active, as Somnium requires.
  - **Why not the player's transform** (2026-11-02): the first version squashed the whole player to 0.1% height from
    load. Somnium sizes its controls from the player around then, so they came out a sliver (canvas scale 0 × 0, bar
    0 wide: Vulps's screenshot, and the first probe) and never recovered, though they still took the laser.
- **`PopcornPlant`** (on `Plant_Popcorn`): the `Spray` particle system (gravity, bouncing, shrinking away), off while
  this plant holds the screen; and the `Stream`, no gravity, emitting for the build time less the flight, each
  particle steered in `LateUpdate` to its own spot along the building edge (its random seed picks the spot), with a
  sideways wander so it reads as a cloud, and removed on arrival.
- **The plant's body** is `Popcorn` (scaled by growth) holding `Mesh` 0.15 units up, so about 30% is buried at every
  size.
- **Models:** the kernel is built in the Editor, like the splif: `Models/Popcorn/Kernel.asset` (a lathed teardrop,
  1 unit tall, pivot at its base; its profile clamps sin at the tip, which once came out negative and made every
  vertex NaN). The **popped piece is made in Blender** (2026-11-02) by `Models/Popcorn/Popcorn.blender.py`: metaballs
  for the puffs (a core, nine lobes burst outward and up, some with a smaller puff), voxel-remeshed, a knobbly clouds
  displacement and finer Voronoi wrinkles, and a golden hull fragment at the base; scaled to 1 unit and exported
  twice, `Popcorn.fbx` (3000 triangles, the plant) and `Popcorn_Small.fbx` (308, every particle, since the stream can
  hold hundreds at once; puffs only, in one material, because a particle mesh draws its first sub-mesh only and Unity
  warns otherwise). Its two materials, `Popcorn` and `Popcorn_Hull`, are remapped on import to our `Popcorn`
  (cream) and `Popcorn_Kernel` (golden), each with a trip twin. Rerun:
  `blender --background --factory-startup --python Popcorn.blender.py -- <out_dir> <preview_dir>`, which also renders
  a preview.
- **The screen faces the plots: rotation 90.** Somnium's screen is a Unity quad and its controls a world-space canvas,
  and both face the player object's **−Z**: the picture shows, and the laser meets the controls, only from that side;
  from behind you see the black `BackQuad`. At 270 (as first placed, and again after a double flip on 2026-11-02) −Z
  points away from the plots. Turned to 90, checked: −Z points at the plots.

> [!info] Networking
> Nothing new is sent. The plant is planted, grown and uprooted like any crop, its growth derived from one timestamp.
> Every client sees the same plants grow on the same clock, so each works out the screen's holder, the build and the
> stream for itself. A late joiner who arrives after a plant grew gets the screen already up; a takeover, or a plant
> grown in view, builds it. Playback is synced by Somnium's player.

**Not a real dissolve, yet.** The reveal squashes the transform from the bottom edge. A probe (2026-11-02, since
removed) showed what Somnium builds under the player: `BridgeVideoPlayer(Clone)` → `VideoPlayer`
(`AVProPlayerController`), `BackQuad` (Simple Lit), `FrameParent/Frame` (a **unit quad at the player's origin**, AVPro's
`Unlit/Opaque` material driven by AVPro's `ApplyToMesh`, with a `BoxCollider`) and `Controls`, a world-space canvas
of Play/Pause, Reload, Live, Mute, Volume, Change link, Release control and a Link field. So the squash's assumption
holds. A true dissolve looks possible: give `Frame` a material of ours with a dissolve and the same `_MainTex`, and
`ApplyToMesh` should keep feeding it the video. Untried.

## ARS seeds

**Status (2026-11-02):** built, compiles clean, untested in-world. Both are Developer Market only ([[dev-tools]]),
out of the shops, like popcorn.

Designed 2026-11-02: Vulps asked for one seed whose ripe plant "creates a beam of light up to a point in the sky, and
creates a black sphere around the island" with "light show effects on it, tied in to the ARS system", and a spotlight
seed whose light sits at about half the beam's height, points at the island and pans around it. The gaps (names, the
show itself, the tulip's rig) were filled by Claude and accepted.

### ARS, as it is wired

Community Modules' audio-reactive system (`Scripts/ARS Plugin/`, manual `Documentation/ARS Documentation.pdf`):

- **One `ARSController`** (CM's `ARS Controller` prefab, at the scene root) listens to one `AudioSource`. Fifty times a
  second it splits the spectrum into four bands (bass 0 to 150 Hz, low-mid to 500, mid to 4 kHz, high to 16 kHz) and
  publishes the spectrum to shaders as the global `ARS_Texture1`: a 64 × 64 `CustomRenderTexture`, **x frequency (low
  to high), y history (row 0 now, older rows above), r raw, g smoothed**.
- **`ARSVideoPlayer`** was already on CM's `ARS Somnium Video Player` (in `GardenIsland`): it polls for the
  `AudioSource` Somnium attaches to the player and raises `OnAudioSourceChanged`.
- **`ArsBridge`** (on SceneManager) is the only code of ours that names ARS: it hands that source to the controller
  and reads the four bands. So every ARS effect reacts to whatever is playing on the screen. Each client analyses the
  sound it hears; nothing is sent.
- **`ShowManager`** (on SceneManager, `Show/`) turns the raw bands, which are small and depend on the volume, into
  0..1 levels against the loudest of the last few seconds (quick up, slower down, with a silence floor so hiss is
  not a show), and raises `Kick` on a bass hit. From the gaps between the last nine kicks it keeps a `Tempo` (the
  median gap, folded into 75 to 200 bpm so a missed or doubled kick reads the same; easing to 0 when the kicks stop
  for 3 s). It also holds the dome, as `ScreenManager` holds the screen.

### Moonflower: the dome

- **Seed** `Seed_Moonflower`: a large dark seed. Buy 150, nothing to sell, grows in 60 s, `spawnWeight` 0.1.
- **Plant** `Plant_Moonflower` (`MoonflowerPlant`): a 1.1 m stem with pointed leaves and a closed white bud; grown,
  its five petals open over 3 s around a glowing centre. Each petal pivots at the flower's base and tilts out about
  the line across its base, worked out from where it sits, so the model's export axes do not matter.
- **The beam**: when the dome goes up, a beam (`ShowBeam` on its own tube mesh, rebuilt along the beam's curve each
  frame) climbs from the flower for 2 s to where it meets the dome, about 30 m up, and a star forms there.
- **Convergence** (Vulps, 2026-11-02, after the Spirit World's harmonic convergence in *Legend of Korra*): **every**
  grown Moonflower beams while the dome is up. With two or more, the star glides to the top of the dome over about
  3 s and every beam bends from straight into an arc, rising out of its flower and curving in to meet the star; it
  grows a third bigger for each beam past the first. Back to one, the star glides back above it and the beam
  straightens. A Moonflower that grows while the dome is up climbs to the star; a late joiner sees the beams there.
- **The music on the beams**: they swell on the bass, and each bass hit sends a bright pulse up every beam to the
  star (`_Pulses` on `ShowBeam`, up to four at once); the mids make them writhe, a wave rolling along each beam,
  pinned at both ends, its size following the mids. The star swells on the highs.
- **The dome** (`Dome` in `GardenIsland`, `DomeReveal`): a black sphere of radius 45 m, centred under the island's
  middle 15 m below the plots, so it takes in the rock underneath too; its top is the beam's 30 m. Once the beam
  arrives, night spreads from the star over 6 s, a glowing front rolling down the sides, and closes around the
  island. From another island it is a black orb with a beam out of its top.
- **Night inside** (Vulps, 2026-11-02: "it's night, but i have a shadow"): while a client's head (`Camera.main`) is
  inside the sphere, `DomeReveal` turns the sun (`RenderSettings.sun`) to **moonlight**, a quarter of its intensity in
  pale blue with no shadows, and takes the Trilight ambient to a **moonlit night** (0.2, 0.22, 0.32) that the bass
  lifts in a colour turning every eight beats, following the dome's spread. The first version faded the sun to
  nothing and the ambient near black, and the Garden became invisible (Vulps: "it is still, first and foremost, a
  gardening game"); leaving the sphere, or the dome going, puts
  the day's values back exactly as they were saved. Each client's own lighting, so players outside keep their
  daylight. The props' own glow (below) and the tulips' spotlights do the rest. The **nature ambience hushes** with the same fade
  (Vulps, 2026-11-02): `Environment/nature` is a scene object, which a prefab cannot point at, so `DomeReveal` reports
  its night to `ShowManager.ReportNight`, which scales the sources on `_quietAtNight` from the volumes they woke with.
  The hut's fireplace keeps crackling.
- **Sounds** (Vulps, 2026-11-02: "a warb or a vvvvrrrrrrrrrmmmmmmm type sound when the sphere is dropping"),
  synthesised in Python by `sounds/Show/show_sounds.py` (numpy and scipy; rerun to retune):
  - `beam_rise`: a bright tone gliding up two octaves under a shimmer as a beam climbs, easing out as it reaches the
    dome (the bell it first ended on was removed, Vulps); at the flower, fully positional.
  - `dome_drop`: a deep detuned drone bending down an octave with a warble in its pitch and a throb in its loudness,
    both slowing, its tone darkening as the night closes; at the star.
  - **No boom when the dome closes** (2026-11-02): one was tried for a dome-wide impact from the bottom of the sphere,
    through several synthesised versions and a recipe of Vulps's, and dropped; none sounded like the bass drum hit
    Vulps wanted. The drop ends on its own warble. If it comes back, a recorded sample is the way.
  - `dome_lift`: the drone rising and opening as the night draws back; at the star.
  - `converge`: an A major ninth pad swelling as the star glides to the top, and a rising shimmer (the chime it
    first ended on was removed, Vulps); at the top of the dome.
  `ShowSounds` on the Dome plays them on `ShowManager`'s `BeamClimbing`, `Raising` (the drop waits for the beam),
  `VisibilityChanged(false)` and `Converging`. **Its sources are children of the Dome, in `GardenIsland`, so they
  exist from load**: Somnium puts load-time sources on its Ambience volume and leaves runtime ones on no group
  ([[tablet#Settings|tablet → Settings]]), so a source on the spawned plant would ignore the player's volume. Each
  source is moved to where its sound happens. The drop and lift are half positional, so they fill the dome.
- **The light show** is one shader, `ShowDome`, reading `ARS_Texture1` itself, with the levels and kicks fed in by
  `DomeReveal`:
  - aurora curtains climbing from the horizon, green to magenta: frequency runs around the horizon (mirrored, so the
    bass shows in four places) and the spectrum's history climbs the curtain;
  - stars that twinkle faster and brighter on the highs (a 3D grid of cells: a latitude grid pinched them into
    streaks at the top);
  - a ring running out from the star on each bass kick, up to four at once;
  - an idle drift, so it is never dead when nothing plays.
- **One dome**, as with the screen: the oldest grown Moonflower holds it, which decides when it is raised and carries
  the star; the others beam into it, and the next oldest takes over when the holder goes. Permanent for the session; it holds its PlantSlot until the Plot is
  cleaned up. With no holder, night draws back into the star over 3 s.
- **Why the dome draws in the transparent queue** (2026-11-02): in the opaque queue it never appeared. URP's depth
  priming draws an opaque only where its own depth pre-pass marked it, and a hand-written shader with no `DepthOnly`
  pass is never marked. Queued just before the transparents it still writes depth and covers the sky.

### Spotlight Tulip

- **Seed** `Seed_SpotlightTulip`: a tulip bulb. Buy 60, nothing to sell, grows in 40 s, `spawnWeight` 0.2.
- **Plant** `Plant_SpotlightTulip` (`SpotlightPlant`): a magenta tulip on a 0.5 m stem. Grown, its cup opens and a
  stage light (`Spotlight_Lamp`: a black can with fins and a glowing lens) rises out of it over 4 s, growing as it
  goes, to **half the height of the Moonflower's beam above the plant**, about 15 m. Rising, it looks up; risen, it
  turns down onto the Garden and **pans around it**: a circle about the dome's centre at 40% of its radius, at the
  plot's height. **On a beat it pans with the tempo** (Vulps, 2026-11-02: the first version "a bit boring"): one turn
  every 8 or 16 beats, so a faster song pans faster, and it **flashes on every kick**. With no beat it drifts at its
  own slow speed (16 to 30 s a turn). The pan angle accumulates, so a change of tempo never makes a light jump.
- **The light**: a real Spot Light (no shadows) plus a visible cone (`ShowBeam` on a cone) and the lens, all one
  colour. Brightness follows one ARS band; quiet, it stays dimly lit.
- **Every tulip is a light; there is no holder.** Each picks its colour (six), band (four), beats per turn, idle
  speed and direction from its planting time, so every client sees the same rig, and a row of them is not uniform.

### The Garden dances

**Status (2026-11-03):** built, compiles clean, untested in-world; the hut, board and tarp were redone on 2026-11-03.
Vulps, after the first ARS upload worked: "add ARS to the scoreboard, the stall, the fences, and the hut", then "and the clouds too", with total creative freedom.

**The fences, hut, scoreboard and sell point's tarp dance only under the dome** (Vulps, 2026-11-02): their shaders and scripts follow
`Show_Night`, which is this client's head inside a closed dome, fading in as it closes; by day, Moonflower or not,
they are as they were. The stalls and the clouds dance whenever the screen plays within earshot. Everything is local, from the music
each client hears; nothing is sent.

- **Shader globals** (`ShowManager`): the four levels (`Show_Levels`), a beat count that steps with the tempo and
  creeps with none (`Show_Beat`), a kick flash fading over 0.25 s (`Show_Kick`) and the night (`Show_Night`), set
  every frame, so a shader can dance with no script. `ShowManager.InEarshot(point)` (within 1.6 dome radii of the
  dome's centre) keeps everything beyond the Garden still: there are 207 stalls across the islands.
- **The point** (Vulps, 2026-11-02): under the dome the Garden went dark, and it is first a gardening game, so the
  **existing props become part of the light show** and help light it. A first pass added fairy lights along the
  fences instead; Vulps had them scrapped ("i was expecting you to like make the materials reactive"). So the props'
  own surfaces carry the show: the fences gain an **additive overlay**, a second material on the same renderer (the
  trip's material swap leaves it alone, since it swaps only materials it knows), and the hut, board and tarp get a lit
  shader of their own (below).
- **Fences: the equaliser** (`Fence_Eq`, `ShowFenceEq`, on all 28 fence panels in `GardenIsland`): each panel is a
  whole equaliser in its own object space, bass at its left end, highs at its right; its pickets light up in ten
  rows of LED cells from the bottom to the spectrum's level, green through yellow to magenta, with peak caps held
  from the spectrum's history. Object space, so it holds wherever the island sits; `_Along` and `_Rise` are the
  `fence_4` mesh's extent.
- **Hut, board and tarp: their own lit shader** (Vulps, 2026-11-03: the first pass's aurora overlays on the hut and
  board were "a bit naff"). `Shaders/ShowLit.shader` + `ShowLit.hlsl` is lit like URP Lit and replaces those props'
  materials outright; one keyword per dance, still by day (`Show_Night` 0), and every vertex melts with the trip, so no
  trip twins (the hut, scaled ×109, smears like an apparition while tripping: kept on purpose, see [[weed]]):
  - **Board** (`Scoreboard_Panel` on `Board_Main`, `_SHOW_PLANKS`): the panel's five planks each take a colour, and
    every beat the colours step one plank along; each plank glows with one band and flashes on the kick. The panel is
    a box in the board's object space (`_PanelMin`/`_PanelMax`); the frame and roof keep `M_WoodenBoard`. The vendor
    shader's ORM map is not used.
  - **Sell point tarp** (`SellPoint_Tent` on `Fish_Market_Tent.001`, `_SHOW_TARP`): the tent is vertex-coloured, so
    the shader takes vertex colour, and recolours only the bright vertices (the tarp and its trim; the wood is
    darker): stripes across it in two opposite colours, turning a quarter of the way round the hues each beat.
  - **Hut** (`Hut_Logs`, `Hut_Door`, `Hut_Windows` on the log house, door and windows, `_SHOW_PIECES`): each kick
    sends a pulse through the hut, like the dome's light pulsing down to the horizon (Vulps, 2026-11-03, replacing a
    hut that hung broken for beats at a time): whole ahead of the front and behind it, broken into glowing cubes
    where it passes (turned, shrunk and pushed out 35 cm), crossing in 0.4 s. Kicks run up, then down, in turn. Two
    triggers change the direction: a kick 1.5× harder than the last 16 or so (`ShowManager.Surge`,
    kick against kick: against the bass's plain average, which counts the gaps, nearly every kick swept sideways in
    the first upload) sweeps sideways along the hut's long side, alternating ends; the first kick after a second
    of silence (`ShowManager.QuietFor`) bursts out from the middle of the floor like a dome. `HutGroove` sets the
    pulse per renderer (`_Wave`: front, width, ring, strength; `_WaveFrom`, `_WaveTo` in world space, from the log
    house's collider bounds). It is a vertex effect, so the meshes are cut up beforehand:
    `Models/Show/HutPieces.eval.txt` (run with `unity command eval`) clips every triangle against a 0.55 m grid into
    `Models/Show/Pieces_*.asset`, carrying each piece's centre and height in uv3 and its way out and a random in uv4.
    Whole, it looks exactly as before; the collider keeps the original mesh.
- **Scoreboard, too** (`ScoreboardGroove`): the ten name rows bob in a wave down the board, a beat long; the
  leader's row punches 18% bigger on each kick. Rows move from where they rest and go back out of the dome. An LED
  equaliser crown above the board was removed before the first upload (Vulps); the fences carry the equaliser.
- **Stalls: speaker barrels** (`StallGroove` on the `BuyPoint` prefab): the barrel and sign squash on each kick like
  a speaker cone (wider, shorter) and pump with the stall's band (bass, low-mid or mid); the crop's name lights up
  in the stall's colour; a pool of that colour (`GrooveGlow`, `ShowGlow`) glows on the ground under it, stronger at
  night. Colour and band come from where the stall stands, so every client agrees.
- **Hut, too** (`HutGroove` on `DeedHut`): besides the pulses, the windows glow firelight, turning to party colours as the mids fill; the fire flares on the bass (`FireLightFlicker.Boost`, and the flames' particles
  burn faster); the chimney puffs a ring of coloured smoke on every kick (`BeatRings`, 28 puffs spreading and
  rising). The hut's materials are our own copies (`Materials/Hut_*`): the vendor's are outside the repo's allowlist,
  so a change to them would never reach a fresh clone.
- **Clouds** (`CloudGenerator`): each kick lights two random clouds from inside in a colour, fading over 0.6 s, a
  colourful storm to the beat, while the player is in earshot. `CloudBlob.mat` has emission on (black) for it.

### Shared pieces

- **Models** are made in Blender, the scripts beside them: `Models/Moonflower/Moonflower.blender.py` (plant and seed)
  and `Models/SpotlightTulip/SpotlightTulip.blender.py` (plant, lamp and bulb). Each petal is its own object; the
  flower's Head and the tulip's Cup are empties. Rerun as for popcorn. Their Blender materials are remapped on import
  to ours in `Materials/`, each lit one with a trip twin on `TripManager`.
- **Meshes made in the Editor**: `Models/Show/UnitSphere` (the dome and the star), `BeamTube` (the prefab's
  placeholder; each Moonflower builds its own curved tube at runtime) and `LightCone`, with uv.y along their length
  for `ShowBeam`.
- **Shaders**: `Shaders/ShowDome.shader` and `Shaders/ShowBeam.shader` (additive, bright down the middle, soft at the
  silhouette, fading along its length).

> [!info] Networking
> Nothing new is sent, as for popcorn. Plants grow on one clock everywhere, so every client works out the dome's
> holder, the beam, the reveal and the tulips' rig for itself; the music each client hears drives its own show.
> Playback is synced by Somnium's player, so the shows agree to within its sync.

**Not yet seen in-world, and worth watching:** whether the video player's `AudioSource` gives ARS a spectrum at all
in Somnium (if every level stays 0 the show idles; `Feed()` logs when ARS starts hearing a source); the levels'
feel; how far the dome and beam are visible from other islands; the tulips' real lights' cost with many planted.
