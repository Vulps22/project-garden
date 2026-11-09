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

> [!warning] Pinned
> **Only Vulps gets it for now** (`TutorialManager.Available`: `Developer.IsLocalOwner`, Vulps's Somnium id), so nobody
> else knows it exists until it is refined and tested. For everyone else nothing starts, flight is never locked and
> Settings → World has no Tutorial row. Opening it to everyone is deleting that one condition.

- **Everyone does it once,** regulars included (Vulps): on joining, a player whose `tutorial_mk2` save does not say
  *done* is taken up to it. Finishing or skipping saves *done*.
- **Settings → World → Tutorial** is **Skip** while it runs (straight to the world spawn, saved as done) and
  **Repeat** after ([[world-settings]]).
- **Flight is locked** from the moment it starts until its send-off (`TutorialManager.FlightLocked`), so nobody flies
  off around the void. Skipping unlocks it too.
- **2D players are not supported** (Vulps): the send-off needs a T pose. A pass over controls and gameplay for 2D
  players follows the tutorial's first pass.

## The platform

**Status (2026-11-09):** built, compiles clean, untested in-world.

- **5 km above the world's origin** (`TutorialPlatform` at y 5000), far past the furthest draw distance (2 km), so
  nobody below sees it. Coordinates that far out still have sub-millimetre precision. The Gardens' bubbles use 3D
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
| 8b | | "You can use the almanac to see all the seeds available in the game. It shows how often they spawn, how many there are in the world, and if you haven't found one yet, you will see a silhouette." | reading time |
| 9 | every buffing and upgrade seed, in two labelled rows, in and out of smoke on a loop | "There are three types of seed in the world." Crops, buffing seeds, upgrade seeds (below) | reading time |
| 10 | the void floor goes, a solid disc comes | "It is now time to spread your wings and fly." **Make a T pose…** then **Press jump.** | taking off from the disc |

- **The carrot's numbers are the real carrot's:** its price on the barrel, and its growing time in step 5b and in the
  growing itself, come from `Seed_Carrot`'s `CarrotSeed`.
- **Step 9's seeds are found, not listed:** any seed in the stalls (`WorldManager.Buyables`) or the Developer Market
  with a `buffMultiplier` over 1 is a buffing seed, and any with an `upgradeBonus` is an upgrade seed. A new one joins
  the loop with no change here. Crops get no visual (Vulps). Today that is Yggdrasil (buffing, Developer Market
  only) and the Moonflower, Spotlight Tulip and popcorn (upgrades).
- **Step 9's copy:** crops grow into things you harvest and sell; buffing seeds give multipliers to the size and
  value of your crop, for example 3× its base value, and always run on a timer; upgrade seeds raise the base value
  of everything you grow, for example by 5%, without a multiplier, for as long as you're in the world. Both numbers
  are examples (Vulps).
- **Step 8 watches the tablet** through `TabletManager.IsOut`: Somnium hides the tablet by fading its `Canvases`
  CanvasGroup to 0 ([[tablet]]).
- **Step 10:** flight unlocks when the disc appears. The instruction reads *Press jump* while both arms are out
  (`FlightController.ArmsOutNow`, the pose flight itself needs) and goes back to the T pose when they drop. With no
  pose after 5 s, an aside stays up until there is one: "If your avatar is too small, the game will not detect your
  T pose. Try scaling up your avatar." Jumping with arms out takes off (`FlightController.Launched`). That is the
  send-off: flight locks again at once (ending the take-off), the player is teleported to the world spawn
  (`GardenReturnPoint`, the same point `WorldFloor` uses), and flight stays locked until they are standing
  (`FlightController.Grounded`, or 10 s at most). Only then does the tutorial end and save as done. Skipping lands the
  same way.

## Voice lines

**Status (2026-11-09):** wired, nothing recorded. Asked for by Vulps after the first run: "that was a lot to try and
read".

Each step plays a recorded line if there is one, `sounds/tutorial2_<line>`, matched by name; the panel keeps the text
as a caption. A passage with a line waits for it to finish plus a second; without one it waits for reading time. The
builder wires every `tutorial2_*` clip it finds, so recording one is: drop the file in `sounds/`, rerun *Build
Tutorial Platform*, save the scene. The lines to record, with the text each step shows:

| Clip | Step | Says |
|---|---|---|
| `tutorial2_plot` | 1 | This is a plot. You must claim a plot before you can plant in it. Grab the highlighted scroll. |
| `tutorial2_hut` | 2 | Take the scroll into your hut to claim the plot. |
| `tutorial2_claimed` | 2b | This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming. |
| `tutorial2_barrel` | 3 | This is a buy point… Buy the seed. |
| `tutorial2_scoreboard` | 4 | This is your scoreboard… Thatch is the currency we use in the Fractured Isles. |
| `tutorial2_plant` | 5 | Plant the seed by pushing it into the dirt pile on the ground. |
| `tutorial2_growing` | 5b | Wait for the seed to turn into a carrot. Carrots take 10 seconds to grow. (The anus line, if wanted.) |
| `tutorial2_harvest` | 6 | Grab the carrot out of the ground. |
| `tutorial2_sell` | 7 | Bring the carrot to the sell point to sell it. |
| `tutorial2_tablet` | 8 | Open your tablet. |
| `tutorial2_almanac` | 8b | You can use the almanac… you will see a silhouette. |
| `tutorial2_seedtypes` | 9 | There are three types of seed… for as long as you're in the world. |
| `tutorial2_sendoff` | 10 | It is now time to spread your wings and fly. Make a T pose. |
| `tutorial2_jump` | 10 | Press jump. |
| `tutorial2_tposehint` | 10 | If your avatar is too small, the game will not detect your T pose. Try scaling up your avatar. |

> [!question] Open
> - **Reading time** for passages with no recorded line yet (steps 2b, 4, 8b, 9) is 0.4 s a word, never under 4 s
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
| `Teams` | `tutorial_teams` | parked: teammates exist on the plot, but nothing announces joining one |

Wiring one is a fact announced by whatever system owns it and one `TipNarrator.Play` call. Do it as part of building
that feature. The narrator's own lines (`tutorial_*.mp3`) are no longer played; their script and timings are kept in
[[fractured-isles-tutorial-script]] and [[tutorial-transcript-mapping]].

Related: [[flight]] (the send-off), [[plot-ownership]] (the real deed the tutorial's scroll stands in for).
