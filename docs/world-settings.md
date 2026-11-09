---
aliases:
  - "World Settings"
  - "Settings"
tags:
  - interaction
  - tech
---
# World Settings

A player's own preferences for this world: our **World** section in Somnium's tablet Settings app, and the
`GameSettings` that stores them. Entirely per player: nothing here is sent, and nobody else is affected. How Somnium's
Settings app is laid out: [[tablet#Settings|tablet → Settings]].

## The page

**Status (2026-11-02):** built, compiles clean, untested in-world.

Our own section in Somnium's Settings app (Vulps, 2026-11-02): **World**, holding a **World Settings** panel of this
world's per-player switches on every player's tablet, and beside it a **Developer** panel on the tablet of anyone with
developer access ([[dev-tools]]). `WorldSettingsPage` (on the `GameSettings` object) builds it through `TabletManager`
→ `TabletBridge`; the panels stack in a column like Settings' own left column. Like everything we add to the tablet,
it is built again on a new tablet ([[tablet#Keeping our additions on the tablet|tablet → Keeping our additions on the tablet]]).

- **A "World" button** at the bottom of the section column, cloned from Audio's with its localisation stripped and
  its click replaced (Somnium's persistent call to `CanvasGroupManager.SwitchActiveCanvasGroup` goes with the old
  event). Seven buttons overflow the column at Somnium's spacing of 40, so the spacing is closed up to fit.
- **How Settings switches sections** (seen 2026-11-02, every section clicked): plain `SetActive`. A section button's
  two persistent calls (`CanvasGroupManager.SwitchActiveCanvasGroup` on `ContentView` and on `SelectorPanel`) switch
  the previous `OptionsTab.<Name>` panel off and the new one on, and likewise the buttons' `<Name>_Highlight`
  children (Avatars' highlight is named `Graphics_Highlight`, a Somnium slip).
- **Ours is a real section**: `OptionsTab.World (Grow A Garden)`, a sibling of `OptionsTab.General` and the rest, with
  no background of its own, holding a `Left` column of panels in Settings' own dark sprite. Picking World switches
  every other section panel and highlight off and ours on, as Somnium does. Picking one of Somnium's sections hides
  ours (a listener added to each button, which runs after Somnium's own calls); Somnium's manager still thinks its
  last section is open, so if it did not bring the picked panel back, we switch it on ourselves.
- **Heights are stated, not fitted**: each panel's title (42) and rows (116.5 a line, two to a line, 11.6 apart)
  carry a `LayoutElement`, and the column spaces panels 22 apart. Content-size fitters nested in layout groups come
  out short and the rows overlap.
- **The switches are Somnium's on/off row**, cloned from `Option_FolowPlayer` with `OptionViewBase`, `On_OffToggle`
  and the localisation stripped, and its `Toggle` event replaced. We draw the state as Somnium does: the handle at
  x −21 off and +21 on, the fill's alpha 0 or 1.

## The switches

**Status (2026-11-02):** built, compiles clean, untested in-world.

**World Settings**, on every player's tablet:

- **Allow Stoned Effects**, on by default. Off, no trip starts and one running ends ([[trip]]).
- **Allow Force Feeding (soon)**: shown off and greyed, since the feature does not exist yet.
- **Show Notifications**, on by default. Off, the Notifier shows nothing ([[notifier]]).
- **Draw Distance**, a slider row cloned from Audio's Main volume, 300 m to 2 km ([[detail]]).
- **Tutorial → Skip / Repeat**: a row with a button rather than a switch. While the tutorial runs it reads **Skip**
  (`TutorialManager.Skip`: straight down to the world, saved as done); after, **Repeat** runs it again ([[tutorial]]).

**Developer**, in the right-hand column at x 890 as Somnium's own pages have one, shown only to anyone with developer
access (`Developer.IsLocal`):

- **Notify Everyone** ([[dev-tools#Notify everyone|dev-tools → Notify everyone]]).
- **Enable Native Flight** turns Somnium's own fly mode back on (`Motion.SetFlyModeDisableState(false)`), so the
  sidebar's Fly Mode button returns; glide stays off. Our flight switches both off when it takes hold of the player at
  spawn (`FlightController.Resolve` → `SuppressSomniumLocomotion`, [[flight]]); `Resolve` then gives fly mode back if
  the developer has this on. Our flight keeps running alongside it: switching Somnium's fly mode on while ours is in
  the air is untried.

## Storage

**Status (2026-11-02):** built, compiles clean, untested in-world.

**`GameSettings`** (on its own `GameSettings` object in the scene) holds every setting and stores them per player in
Somnium's local storage, through `StorageBridge`, under the key `player_settings`, so they survive between visits.
It raises `GameSettings.Changed` after any change.

| Field | Setting |
|---|---|
| `trim` | flight trim, in degrees ([[flight]]) |
| `disableVoiceTips` | the tips' switch ([[tutorial#Tips\|tutorial → Tips]]) |
| `noStonedEffects` | Allow Stoned Effects, stored inverted so a save from before it reads as allowed |
| `allowForceFeeding` | Allow Force Feeding |
| `devNativeFlight` | Enable Native Flight; only a developer's client acts on it |
| `noNotifications` | Show Notifications, stored inverted so a save from before it reads as on |
| `drawDistance` | Draw Distance in metres; 0 in a save from before it means the default, 1.2 km |

## The hut panel

**Status (2026-11-02):** built, out of the scene.

`SettingsPanel` (`Prefabs_world/SettingsPanel.prefab`) is a wall panel of `SettingsButton`s: **Set trim** (hold your
arms out the way you fly and press it; whatever your wrists are doing becomes level), **Reset trim**, and the
**Voice tips** switch. Its old **Skip tutorial** button still stands on the prefab but does nothing: the narrator it
silenced is gone ([[tutorial]]). It is not in the scene: with it in the hut, Somnium's own
tablet would not open, which is not understood. So nobody can set a trim or silence the tips in
the world today; a trim stored before the panel went still applies to flight.
