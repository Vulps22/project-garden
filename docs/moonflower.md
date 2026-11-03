---
aliases:
  - "Moonflower"
tags:
  - crop
---
# Moonflower

An upgrade, not a crop to sell ([[fun-seeds]]): grown, it beams into the sky and closes a black dome over its Garden,
the stage for the light show ([[light-show]]). Two or more and their beams arc in to meet overhead.

## The design

**Status (2026-11-03):** built; played in-world. In the stalls since 2026-11-03 (`Buyable_Moonflower`, weight 0.1, Very Rare); untested there.

- **Vulps's brief**: a ripe plant "creates a beam of light up to a point in the sky, and creates a black sphere around
  the island", with light show effects on it tied to ARS. The rest was filled in by Claude and accepted.
- **Convergence** (Vulps, after the Spirit World's harmonic convergence in *Legend of Korra*): every grown Moonflower in
  the Garden beams while its dome is up, and with two or more the beams arc in to one star at the top.
- **Night inside, but still a gardening game** (Vulps): the dome darkens the Garden to a moonlit night you can still
  garden by, with no daylight shadows, and the day's nature sounds hush.

## The plant and the beam

**Status (2026-11-03):** built; played in-world.

- **Seed** `Seed_Moonflower`: a large dark seed. Buy 150, nothing to sell, grows in 60 s, `spawnWeight` 0.1.
- **Plant** `Plant_Moonflower` (`MoonflowerPlant`, `Show/`): a 1.1 m stem with pointed leaves and a closed white bud;
  grown, its five petals open over 3 s around a glowing centre. Each petal tilts out about the line across its base,
  worked out from where it sits, so the model's export axes do not matter.
- **The beam**: when the dome goes up, a beam (`ShowBeam` on its own tube mesh, 32 rings of 10 sides, rebuilt along its
  curve each frame) climbs from the flower for 2 s to where it meets the dome, about 30 m up, and a star forms there.
- **Convergence**: with two or more beaming, the star glides to the top of the dome over about 3 s and every beam bends
  from straight into an arc, rising out of its flower and curving in to the star, which grows a third bigger for each
  beam past the first. Back to one, the star glides back and the beam straightens. A Moonflower that grows while the
  dome is up climbs to the star; a late joiner sees the beams already there.
- **To the music**: the beams swell on the bass, and each bass hit sends a bright pulse up every beam to the star
  (`_Pulses`, up to four at once); the mids make them writhe, a wave rolling along each beam, pinned at both ends; the
  star swells on the highs.

## The dome

**Status (2026-11-03):** built; played in-world.

- **One per Garden** (`Dome` in `GardenIsland`: `DomeReveal`, `ShowStage`, `ShowSounds`): a black sphere of radius
  45 m, centred 15 m below the plots so it takes in the rock underneath; its top is the beam's 30 m. Gardens are 120 m
  apart, so domes never touch ([[towns]]).
- **The holder**: the oldest grown Moonflower in the Garden holds the dome, which decides when it is raised and carries
  the star; the others beam into it, and the next oldest takes over when the holder goes. Permanent for the session.
- **Raising**: once the beam arrives, night spreads from the star over 6 s, a glowing front rolling down the sides.
  With no holder, night draws back into the star over 3 s. From another island it is a black orb with a beam out of
  its top.
- **The light show on it**: `ShowDome` reads `ARS_Texture1` itself, with levels and kicks fed in by `DomeReveal`:
  aurora curtains climbing from the horizon, green to magenta (frequency around the horizon, mirrored; the spectrum's
  history up the curtain); stars on a 3D grid of cells that twinkle on the highs; a ring running out from the star on
  each kick, up to four at once; an idle drift so it is never dead.
- **It draws in the transparent queue** (Transparent-1, writing depth): in the opaque queue URP's depth priming draws
  only where a shader's own `DepthOnly` pass marked, and `ShowDome` has none.

## Night inside

**Status (2026-11-03):** built; played in-world.

While this client's head (`Camera.main`) is inside a dome, `DomeReveal` follows the dome's spread:

- the sun (`RenderSettings.sun`) becomes **moonlight**: a quarter of its intensity, pale blue (0.6, 0.7, 1), no shadows;
- the Trilight ambient becomes a **moonlit night** (0.2, 0.22, 0.32), lifted by the bass in a colour that turns every
  eight beats;
- the **nature ambience hushes**: `DomeReveal` reports its night to `ShowManager.ReportNight`, which scales the sources
  on `_quietAtNight` (`Environment/nature`) from the volumes they woke with. The hut's fire keeps crackling.

Leaving the sphere, or the dome going, puts the day's values back exactly as saved. Each client's own lighting: players
outside keep their daylight. At most one dome holds the camera, since they never overlap.

## Sounds

**Status (2026-11-03):** built; played in-world.

Synthesised by `sounds/Show/show_sounds.py` (numpy and scipy; rerun to retune), played by `ShowSounds` on the Dome:

| Sound | When | What | Where |
|---|---|---|---|
| `beam_rise` | a beam climbs | a bright tone gliding up two octaves under a shimmer, easing out as it reaches the dome | the flower, fully positional |
| `dome_drop` | night spreads (after the beam) | a deep detuned drone bending down an octave, its warble and throb slowing, darkening as it closes | the star, half positional |
| `dome_lift` | night draws back | the drone rising and opening | the star, half positional |
| `converge` | a second Moonflower joins | an A major ninth pad swelling, with a rising shimmer | the top of the dome |

- **The sources are children of the Dome**, so they exist from load: Somnium puts load-time sources on its Ambience
  volume and leaves runtime ones on no group, so a source on the spawned plant would ignore the player's volume
  ([[tablet#Settings|tablet → Settings]]). Each source is moved to where its sound happens.
- **No impact when the dome closes**: synthesised booms never sounded like the bass drum hit Vulps wanted, so there is
  none. If one comes back, a recorded sample is the way.

## Pieces

- **`ShowStage`** (`Show/`, on the Dome): the holder, the star, the convergence and their events (`Raising`,
  `VisibilityChanged`, `BeamClimbing`, `Converging`). A Moonflower finds its Garden's with `ShowStage.For(position)`.
- **Model**: made in Blender by `Models/Moonflower/Moonflower.blender.py` (plant and seed; each petal its own object,
  the Head an empty). Materials `Moonflower_*`, the lit ones with trip twins on `TripManager`.
- **Meshes made in the Editor**: `Models/Show/UnitSphere` (the dome and the star) and `BeamTube` (the prefab's
  placeholder), uv.y along the length for `ShowBeam`.
- **Shaders**: `ShowDome`, and `ShowBeam` (additive, bright down the middle, soft at the silhouette, fading along it).

> [!info] Networking
> Nothing is sent. Plants grow on one clock everywhere, so every client works out the holder, the beams, the star and
> the reveal for itself; the music each client hears drives its own show.
