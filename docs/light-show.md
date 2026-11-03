---
aliases:
  - "Light show"
tags:
  - mechanic
---
# Light show

The Garden dancing to whatever its screen plays ([[popcorn]]): the Moonflower's dome ([[moonflower]]), the Spotlight
Tulips ([[spotlight-tulip]]), and the Garden's own props. Everything is local, from the music each client hears;
nothing is sent.

## ARS, as it is wired

**Status (2026-11-03):** built; played in-world.

Community Modules' audio-reactive system (`Scripts/ARS Plugin/`, manual `Documentation/ARS Documentation.pdf`):

- **One `ARSController`** (CM's `ARS Controller` prefab, at the scene root) listens to one `AudioSource`. Fifty times a
  second it splits the spectrum into four bands (bass 0 to 150 Hz, low-mid to 500, mid to 4 kHz, high to 16 kHz) and
  publishes it to shaders as the global `ARS_Texture1`: a 64 × 64 `CustomRenderTexture`, **x frequency (low to high),
  y history (row 0 now, older rows above), r raw, g smoothed**.
- **`ARSVideoPlayer`**, on each Garden's screen, looks for the `AudioSource` Somnium attaches to the player when the
  player says it is ready or playing (`SomniumVideoPlayer.OnEventReceived`: Initialized, ReadyToPlay, FirstFrameReady,
  Started) and raises `OnAudioSourceChanged`.
- **`ArsBridge`** (on SceneManager) is the only code of ours that names ARS. Whenever the player moves 10 m
  (`PlayerTracker`) it picks the screen nearest this client's head, hands that screen's source to the controller and
  raises `ScreenChanged`; it reads the four bands. When the screen it listens to starts playing (its `Started` or
  `Unpaused` event) without having given a source, it waits 60 s and warns (`WarnIfDeaf()`), and says it was a false
  alarm if the source turns up after all.
- **`ShowManager`** (`Show/`, on SceneManager) turns the raw bands into 0..1 levels against the loudest of the last few
  seconds (quick up, slower down, with a silence floor so hiss is not a show) and raises `Kick` on a bass hit. From the
  gaps between the last nine kicks it keeps a `Tempo`, folded into 75 to 200 bpm so a missed or doubled kick reads the
  same, easing to 0 when the kicks stop for 3 s. `Surge` is the last kick's raw bass over that of the kicks before it;
  `QuietFor` is how long every band has been silent. It also keeps the **night**: how far this client is inside a
  closed dome.
- **`ShowManager.InEarshot(point)`**: within 75 m of the screen this client listens to. Beyond it, the grooves stay
  still: there are hundreds of stalls across the islands. Earshot changes only when the screen does, so a stall works
  it out on `ArsBridge.ScreenChanged` and switches its own `Update` off while out of it.
- **Shader globals**, set every frame so a shader can dance with no script: `Show_Levels` (the four levels),
  `Show_Beat` (a beat count stepping with the tempo, creeping with none), `Show_Kick` (a flash fading over 0.25 s) and
  `Show_Night`.

## The Garden dances

**Status (2026-11-03):** built; the props' shaders untested in-world.

The props carry the show on their own surfaces, so a dark Garden under the dome stays lit enough to garden in.

- **Under the dome only**: the fences, hut, scoreboard and the sell point's tarp follow `Show_Night`; by day,
  Moonflower or not, they are as they were, and the hut's and scoreboard's scripts do not update at all: they wake on
  `ShowManager.NightChanged`. The stalls and the clouds dance whenever the screen plays within earshot.
- **Fences: the equaliser** (`Fence_Eq`, `ShowFenceEq`, an additive second material on all 28 fence panels): each panel
  is a whole equaliser in its own object space, bass at its left end, highs at its right; its pickets light up in ten
  rows of LED cells from the bottom to the spectrum's level, green through yellow to magenta, with peak caps held from
  the spectrum's history. `_Along` and `_Rise` are the `fence_4` mesh's extent. The trip's material swap leaves it
  alone, since it swaps only materials it knows.
- **`ShowLit`** (`Shaders/ShowLit.shader` + `ShowLit.hlsl`): lit like URP Lit, replacing the hut's, board's and tarp's
  materials outright; one keyword per dance, nothing by day. Every vertex melts with the trip, so no trip twins; the hut,
  scaled ×109, smears like an apparition while tripping, kept on purpose ([[weed]]).
  - **Board** (`Scoreboard_Panel` on `Board_Main`, `_SHOW_PLANKS`): the panel's five planks each take a colour and the
    colours step one plank along every beat; each plank glows with one band and flashes on the kick. The panel is a box
    in the board's object space (`_PanelMin`/`_PanelMax`); the frame and roof keep `M_WoodenBoard`.
  - **Sell point tarp** (`SellPoint_Tent` on `Fish_Market_Tent.001`, `_SHOW_TARP`): the tent is vertex-coloured, and
    only its bright vertices (the tarp and its trim) are recoloured: stripes in two opposite colours, turning a quarter
    of the way round the hues each beat.
  - **Hut** (`Hut_Logs`, `Hut_Door`, `Hut_Windows`, `_SHOW_PIECES`): each kick sends a pulse through the hut, like the
    dome's light pulsing down to the horizon: whole ahead of the front and behind it, broken into glowing cubes where it
    passes (turned, shrunk, pushed out 35 cm), crossing in 0.4 s. Kicks run up, then down, in turn; a kick 1.5× harder
    than the last 16 or so (`Surge`) sweeps sideways along the hut, alternating ends; the first kick after a second of
    silence (`QuietFor`) bursts out from the middle of the floor like a dome. `HutGroove` sets the pulse per renderer
    (`_Wave`: front, width, ring, strength; `_WaveFrom`, `_WaveTo` in world space, from the log house's collider). The
    meshes are cut up beforehand by `Models/Show/HutPieces.eval.txt` (run with `unity command eval`): every triangle
    clipped against a 0.55 m grid into `Models/Show/Pieces_*.asset`, each piece's centre and height in uv3, its way out
    and a random in uv4. Whole, it looks exactly as before; the collider keeps the original mesh.
- **Hut, besides the pulses** (`HutGroove`): the windows glow firelight, turning to party colours as the mids fill; the
  fire flares on the bass (`FireLightFlicker.Boost`, and the flames burn faster); the chimney puffs a ring of coloured
  smoke on every kick (`BeatRings`, 28 puffs). The hut's materials are our own copies (`Materials/Hut_*`): the vendor's
  are outside the repo's allowlist.
- **Scoreboard** (`ScoreboardGroove`): the ten name rows bob in a wave down the board, a beat long; the leader's row
  punches 18% bigger on each kick.
- **Stalls: speaker barrels** (`StallGroove` on the `BuyPoint` prefab): the barrel and sign squash on each kick like a
  speaker cone and pump with the stall's band; the crop's name lights up in the stall's colour; a pool of that colour
  (`GrooveGlow`, `ShowGlow`) glows on the ground under it, stronger at night. Colour and band come from where the stall
  stands.
- **Clouds** (`CloudGenerator`): each kick lights two random clouds from inside in a colour, fading over 0.6 s, while
  the player is in earshot. `CloudBlob.mat` has emission on (black) for it.

> [!info] Networking
> Nothing is sent. Each client analyses the sound it hears; playback is synced by Somnium's player, so the shows agree
> to within its sync.
