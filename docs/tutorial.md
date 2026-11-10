---
aliases:
  - "Tutorial"
tags:
  - game
  - interaction
---
# Tutorial

A guided tutorial in a white void above the world. Each step shows text, highlights the next thing to touch and
waits for the player to do it. It replaces the voice narrator, whose instructions nothing in the world helped anyone
follow. The recorded **tips** stay, under *Tips* below.

> [!info] Networking
> Entirely local. The tutorial's seed, scroll, carrot, hut, barrel and sell point are copies that only draw: no
> NetworkObject, no RPC, no master. Buying and selling are pretend, and the player's real balance never changes.
> Two players can be in the tutorial at once: they see each other's avatars, as anywhere, but not each other's pieces.

## Who does it, and leaving it

**Status (2026-11-09):** built, compiles clean, untested in-world. Designed by Vulps 2026-11-09.

- **Everyone does it once,** regulars included (Vulps): on joining, a player whose `tutorial_mk2` save does not say
  *done* is taken up to it. Finishing or skipping saves *done*.
- **Settings → World → Tutorial** is **Skip** while it runs (straight to the world spawn, saved as done) and
  **Repeat** after ([[world-settings]]).
- **Flight is locked** from the moment it starts until its send-off (`TutorialManager.FlightLocked`), so nobody flies
  off around the void. Skipping unlocks it too.
- **Open to everyone** (Vulps, 2026-11-10), after Vulps alone had it while it was refined.
- **2D players are not supported** (Vulps): they are taken up like everyone else, but the send-off needs a T pose, so
  Skip (which the welcome tells them about) is their way out. A pass over controls and gameplay for 2D
  players follows the tutorial's first pass.

## The platform

**Status (2026-11-09):** built, compiles clean, untested in-world.

- **5 km above the world** (`TutorialPlatform` at y 5000), far past the furthest draw distance (2 km), so
  nobody below sees it.
- **One sky per player** (Vulps: ten players in one spot would clash). When a tutorial starts, the whole platform moves
  to a spot on a 600 m grid at its height, centred over the origin, where no other player within ±50 m of that height
  (jumping or falling) is closer than 500 m across the ground (`StandClearOfOthers`; other players' heads from
  `PlayerManager.OtherPlayerHeads`). It picks at random among the nearest free spots, so two players starting together
  rarely pick the same one. A player stays inside the walls, 14 m from their spot, so neighbouring tutorials are always
  over 570 m apart. With no free spot in 6 rings (169 spots) it takes the clearest and logs a warning. Sectors are
  worked out across the ground only, so a tutorial player counts as being in the sector under their spot, 5 km down
  ([[procgen-islands]]): watch whether that hands them anything to simulate there. Coordinates that far out still have sub-millimetre precision. The Gardens' bubbles use 3D
  distance, so the dome, night and level of detail leave the player alone up there ([[detail]]).
- **The look** is Vulps's Animus (Assassin's Creed): a white void with glowing blue lines. A sky sphere 80 m across
  and the floor draw `GrowAGarden/TutorialVoid`, near-white with a world-space grid and lines that sweep along it.
- **The game's own pieces, standing out against it:** each is a copy of the real prefab with everything but its meshes,
  text, fire and (on carryables) its grab stripped off. The hut's show materials are swapped for their `_Plain`
  versions so nothing dances.
- **An invisible floor and a ring of 24 invisible walls** 14 m out keep the player on it.
- **Transitions are slow and whole** (Vulps, after the first run: "like whiplash"). A piece dissolves into smoke
  over a second, shrinking while smoke keeps pouring over it, and is completely gone before the next piece builds
  out of smoke over another second. Every change is at least two seconds; the text and highlight come only once the
  new piece is whole (`TutorialStage.SECONDS`, `SmokePuff`).
- **Pieces stand where the Garden stands them.** The Garden's ground is 0.5 m above its pieces' roots, so the plot is
  sunk by that (1 cm proud of the void floor so the floor does not draw over its grass), and the barrel's root sits
  0.35 m up. The barrel keeps its own labels (name, tagline, buy and sell price, filled as `StallDisplay` fills
  them) and is turned so they face the player. The seed sits at the barrel's socket with its rotation, and the carrot
  grows at the dirt pile's anchor, half in the soil, as a real plant does.
- **Pieces build in front of the player**, not at fixed spots (Vulps: the barrel built behind them on the second
  run). Each build takes where the player stands and looks, and lays its pieces out as the builder laid them out from
  the arrival point, turned to that facing. The carrot grows in the frame its plot was built in; the seed-types show
  is placed the same way, and the send-off disc builds under the player's feet. A layout that would reach past 12 m
  from the void's centre is pulled back towards the player, so nothing builds outside the walls.
- **The highlight:** a pulsing blue shell on a copy of the target's meshes (`TutorialGlow`, `GrowAGarden/TutorialGlow`).
- **The text:** `InstructionPanel`, a panel fixed below the middle of the view, drawn over everything. It shows what
  the thing is, what to do now (bold, blue), and a passing aside under it. The Notifier's toasts are separate
  ([[notifier]]).
- **Built by a menu, not by hand:** *Grow A Garden → Build Tutorial Platform* (`TutorialPlatformBuilder`, Editor
  only) deletes the platform and builds it again from the current prefabs, wiring every reference. Rerun it after the
  hut, barrel, plot, scoreboard, sell point, scroll, carrot seed or carrot changes. Then save the scene.

## The steps

**Status (2026-11-09):** built, compiles clean, untested in-world.

| # | Shows | Text | Waits for |
|---|---|---|---|
| 0 | the void, nothing in it yet | "Welcome! Before we can let you loose on the Fractured Isles, I want to give you a rundown of the basics of growing your garden." How to skip from Settings → World, and to come back to it there later. | the lines |
| 1 | the plot, the scroll hovering over it (lit) | "This is a plot. You must claim a plot before you can plant in it." **Grab the highlighted scroll.** | the scroll grabbed |
| 2 | the plot goes, the hut comes; its door lit | **Take the scroll into your hut to claim the plot.** | the scroll inside the hut's claim zone |
| 2b | the scroll goes; the deed shows the player's name, the fire lights | "This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming." | reading time |
| 3 | the hut goes, the barrel comes with its price; the seed lit | "This is a buy point. You will find these dotted throughout the Fractured Isles. If there is a seed inside it, you can buy the seed for the price listed on the barrel." **Buy the seed.** | the seed grabbed |
| 4 | the barrel goes, the scoreboard comes: the player's name and 0 | "This is your scoreboard. This is how you see how much Thatch you currently own. Thatch is the currency we use in the Fractured Isles." | reading time |
| 5 | the scoreboard goes, the plot comes; the dirt pile lit | **Plant the seed by pushing it into the dirt pile on the ground.** | the seed in the dirt pile |
| 5b | the seed goes; a carrot grows out of the pile | **Wait for the seed to turn into a carrot. Carrots take 10 seconds to grow.** Aside, 5 s: "Yes, I know it looks like an anus. But it's a free asset." | the carrot's growing time |
| 6 | the carrot lit; a "Something is ready to harvest" toast | **Grab the carrot out of the ground.** | the carrot grabbed |
| 7 | the plot goes, the sell point comes (lit) | **Bring the carrot to the sell point to sell it.** | the carrot in the sell point |
| 8 | the carrot and the sell point go | **Open your tablet.** | the tablet out |
| 8b | | "You can use the almanac to see all the seeds available in the game. It shows how often they spawn, and how many there are in the world. If you haven't found one yet, you will see a silhouette." | the lines |
| 9 | every buffing and upgrade seed, in two labelled rows, in and out of smoke on a loop | "There are three types of seed in the world." Crops, buffing seeds, upgrade seeds (below) | reading time |
| 10 | the seeds go; then the void floor goes, a solid disc comes | "That's it! That's the basics, the rest you can find out for yourself! You're a grown-up (I hope), so I'm sure you're smart enough." Then "It is now time to spread your wings and fly." **Make a T pose…** then **Press jump.** | taking off from the disc |

- **The carrot's numbers are the real carrot's:** its price on the barrel, and its growing time in step 5b and in the
  growing itself, come from `Seed_Carrot`'s `CarrotSeed`.
- **Step 9's seeds are found, not listed:** any seed in the stalls (`WorldManager.Buyables`) or the Developer Market
  with a `buffMultiplier` over 1 is a buffing seed, and any with an `upgradeBonus` is an upgrade seed. A new one joins
  the loop with no change here. Crops get no visual (Vulps). Today that is Yggdrasil (buffing) and the Moonflower, Spotlight Tulip and popcorn (upgrades).
- **Step 9's copy:** crops grow into things you harvest and sell; buffing seeds give you multipliers to the size and
  value of your crop, and always last for a limited time; upgrade seeds raise the base value of everything you grow,
  without applying a multiplier, for as long as you're in the world (Vulps's recorded wording).
- **Step 8 watches the tablet** through `TabletManager.IsOut`: Somnium hides the tablet by fading its `Canvases`
  CanvasGroup to 0 ([[tablet]]).
- **Step 10:** flight unlocks when the disc appears. The instruction reads *Press jump* while both arms are out
  (`FlightController.ArmsOutNow`, the pose flight itself needs) and goes back to the T pose when they drop. With no
  pose 5 s after the send-off lines (20, 21) finish, and never over a line still playing, an aside stays up until there is one: "If your avatar is too small, the game will not detect your
  T pose. Try scaling up your avatar." Jumping with arms out takes off (`FlightController.Launched`). That is the
  send-off: flight locks again at once (ending the take-off), the player is teleported to the world spawn
  (`GardenReturnPoint`, the same point `WorldFloor` uses), and flight stays locked until they have stood on the
  island for 5 s (`FlightController.Grounded`, or 10 s waiting for it at most, then `_flightLockAfterLanding`; Vulps).
  Only then does the tutorial end, save as done and unlock flight. Skipping lands the same way.

## Voice lines

**Status (2026-11-09):** all 27 recorded by Vulps (24 and 28 were empty takes, so there are none by those numbers), trimmed and wired; untested in-world. Asked for by Vulps after the
first run: "that was a lot to try and read".

Each step plays its recorded line if there is one, `sounds/Tutorial/tutorial_<number>` (Vulps: it replaces the old tutorial, so
no "2"), matched by the number in the table below; the panel keeps the text as a caption. A step with several lines says them
one after another, 0.3 s apart (`_betweenLinesSeconds`). `tutorial-<number>`, as
`~/Documents/record-tutorial.sh` names its takes, matches too. A passage with a line waits for it to finish plus a
second; without one it waits for reading time. The builder wires every numbered clip it finds and skips the old
narrator's named ones (`tutorial_Welcome`, `tutorial_hut`…), so recording one is: drop the file in `sounds/`, rerun
*Build Tutorial Platform*, save the scene. The lines to record, with the text each step shows:

| Clip | Step | Says |
|---|---|---|
| `tutorial_1` | 1 | This is a plot. You must claim a plot before you can plant in it. Grab the highlighted scroll. |
| `tutorial_2` | 2 | Take the scroll into your hut to claim the plot. |
| `tutorial_3` | 2b | This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming. |
| `tutorial_4` | 3 | This is a buy point. You will find these dotted throughout the Fractured Isles. If there is a seed inside it, you can buy the seed for the price listed on the barrel. |
| `tutorial_5` | 3 | Buy the seed. |
| `tutorial_6` | 4 | This is your scoreboard. This is how you see how much Thatch you currently own. Thatch is the currency we use in the Fractured Isles. |
| `tutorial_7` | 5 | Plant the seed by pushing it into the dirt pile on the ground. |
| `tutorial_8` | 5b | Yes, I know it looks like an anus. But it's a free asset. |
| `tutorial_9` | 5b | Wait for the seed to turn into a carrot. Carrots take 10 seconds to grow. |
| `tutorial_10` | 6 | Grab the carrot out of the ground. |
| `tutorial_11` | 7 | Bring the carrot to the sell point to sell it. |
| `tutorial_12` | 8 | Open your tablet. |
| `tutorial_13` | 8b | You can use the almanac to see all the seeds available in the game. |
| `tutorial_14` | 8b | It shows how often they spawn, and how many there are in the world. |
| `tutorial_15` | 8b | If you haven't found one yet, you will see a silhouette. |
| `tutorial_16` | 9 | There are three types of seed in the world. |
| `tutorial_17` | 9 | Crops grow into things you harvest and sell. |
| `tutorial_18` | 9 | Buffing seeds give you multipliers to the size and value of your crop. Buffs always last for a limited time. |
| `tutorial_19` | 9 | Upgrade seeds raise the base value of everything you grow, without applying a multiplier. They last for as long as you're in the world. |
| `tutorial_20` | 10 | It is now time to spread your wings and fly. |
| `tutorial_21` | 10 | Make a T pose. |
| `tutorial_22` | 10 | If your avatar is too small, the game will not detect your T pose. Try scaling up your avatar. (After 5 s with no T pose.) |
| `tutorial_23` | 10 | Press jump. (Once the T pose is held.) |
| `tutorial_25` | 0 | Welcome! Before we can let you loose on the Fractured Isles, I want to give you a rundown of the basics of growing your garden. |
| `tutorial_26` | 0 | You can skip the tutorial at any time by opening the settings app on your tablet and choosing the “world” tab at the bottom. There’s a skip option in there. |
| `tutorial_27` | 0 | You will also be able to use the Settings app to return to the tutorial at any time in the future. |
| `tutorial_29` | 10 | That’s it! That’s the basics, the rest you can find out for yourself! You’re a grown-up (I hope), so I’m sure you’re smart enough. (Before line 20.) |

> [!question] Open
> - **Reading time** for a passage whose lines are missing (all are recorded now, so only a fallback) is 0.4 s a word, never under 4 s
>   (`TutorialPlatform._secondsPerWord`, `_minimumReadSeconds`). That rule is mine, not Vulps's: check it in-world.
> - **The hut's fire crackle** is on an AudioSource that is inactive at load and switched on at step 2b. Whether
>   Somnium routes it through the player's volume like the Gardens' huts is untested ([[tablet#Settings|tablet → Settings]]).

## How it is built

**Status (2026-11-09):** built, compiles clean, untested in-world.

All under `Assets/#User/Community Modules/Scripts/GrowAGarden/Tutorial/`:

| File | Owns |
|---|---|
| `TutorialManager` | done or not (`tutorial_mk2` in local storage), running or not, the flight lock; `Started`, `SkipRequested`, `Ended` |
| `TutorialPlatform` | the steps: starts the tutorial on join, teleports up and down, shows and hides the pieces, listens to the items and zones |
| `TutorialStage` | one piece coming in from smoke and going out in it |
| `TutorialItem` | a carryable copy: grabbed with XRI, hovers where it is let go |
| `TutorialZone` | a trigger saying which item came into it (the hut's claim zone, the dirt pile, the sell point) |
| `InstructionPanel` | the text in the player's view |
| `TutorialGlow` | the highlight |
| `SmokePuff` | the one smoke particle system, burst to the size of what it covers |
| `SeedTypesShow`, `VisualCopy` | step 9's seeds, copied at runtime from their prefabs' meshes |
| `TipNarrator` | the tips (below) |

- **Starting:** `TutorialPlatform` hears `PlayerManager.LocalPlayerJoined` and calls `TutorialManager.Begin` unless
  done. The teleport waits until Somnium lets us drive the player (`PlayerManager.CanDriveLocalPlayer`), checking
  every half second. A **Repeat** first lets go of anything real the player is holding (`HeldObjects.ReleaseAll`, shared with `WorldFloor`), so no
  real seed rides up with them.
- **Every step is an event:** a grab, an item entering a zone, `FlightController.Launched`, or a timer. Only steps 8 and
  10 check every frame, the tablet and the T pose, because nothing announces either. The component's `Update` is off
  at every other step.
- **Skip mid-step:** each run has a number, and a timer or teleport from an earlier run does nothing when it lands.

## Tips

**Status (2026-11-09):** the falling tip is wired; the rest are parked. Kept from the narrator (Vulps).

`TipNarrator` plays a recorded line once ever, the first time the player meets what it describes. Heard tips are
saved under the narrator's old key, `tutorial_progress`. **Voice Tips** off silences them (hut panel,
[[world-settings#The hut panel|world-settings → The hut panel]]). A tip waits while the tutorial is running.

| Tip | Clip | State |
|---|---|---|
| `Falling` | `tip_falling` | **wired**: `WorldFloor` plays it after a rescue |
| `Climbing` | `tip_climbing` | parked: needs a "player is climbing" signal that nothing announces yet |
| `Upgrades` | `tip_upgrades` | parked |
| `Buffing` | `tip_buffing` | parked |
| `Teams` | `tip_teams` | parked: teammates exist on the plot, but nothing announces joining one |
| `Seed:<seedId>` | the seed's `approachVoice` | **wired**: a buy point holding that seed, walked up to (Seed voiceovers below) |

Wiring one is a fact announced by whatever system owns it and one `TipNarrator.Play` call. Do it as part of building
that feature. The narrator's own lines (`sounds/Tutorial/Old narrator/`) are no longer played; their script and timings
are kept in [[fractured-isles-tutorial-script]] and [[tutorial-transcript-mapping]].

**Where the clips are** (Vulps, 2026-11-09): tips in `sounds/Tips/` (`tip_<name>`), the tutorial's lines in
`sounds/Tutorial/` (`tutorial_<number>`), the old narrator's in `sounds/Tutorial/Old narrator/`.

### Seed voiceovers

**Status (2026-11-09):** built, compiles clean, untested in-world.

A seed can have a voiceover, `SeedDefinition.approachVoice`, set on its `Seed_` prefab (a clip cannot be set in
`Init()`). Most have none; the very special ones do (Vulps). Today only Yggdrasil: `tip_yggdrasil`, "Holy shit. That's
Yggdrasil! That's the rarest, most valuable seed in the game! It's a buffing seed, so use it wisely!"

Every buy point has a `VoiceZone` child: a trigger box 4 × 3 × 4 m round the barrel, on the Triggers layer so it meets
only players' head triggers (`ApproachVoiceZone`). It is **off unless the seed in the barrel has a voiceover**
(Vulps); `BuyPoint` switches it whenever its stock changes. The local player walking in plays the line as a tip
(`TipNarrator.PlaySeed`, id `Seed:<seedId>`): once ever per player, not while another tip plays, not while the tutorial
runs, not with Voice Tips off. Nothing is sent: each player hears it on their own approach.

> [!question] Open
> - **Once ever** is mine, from it being a tip; every approach, or once per session, are the alternatives.
> - **The zone's size** (4 × 3 × 4 m) is mine: check it in-world.
> - **Yggdrasil stocks about 1 island stall in 1,000** ([[yggdrasil]]), so hearing the line by chance is unlikely: pinning a
>   test stall to it (`BuyPoint._override`) is the way to check it.

Related: [[flight]] (the send-off), [[plot-ownership]] (the real deed the tutorial's scroll stands in for).
