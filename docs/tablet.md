---
aliases:
  - "Tablet"
tags:
  - reference
  - tech
---
# Somnium's tablet

Everything Unity can see of Somnium's tablet from inside a world, so that a tablet app, a Thatch bank, an admin
page or anything else built on it starts here rather than with another probe. All of it is Somnium's, not ours:
names can change in any client update, so anything that touches it goes through one bridge and fails quietly back
to Somnium's own behaviour ([[layers]]; `TabletMapBridge` is the model).

**Status (2026-11-02):** captured by `TabletProbe` in two sessions on 2026-11-01 (client SDK 3.1.9), as a peer and as
master. Structure and behaviour as logged; nothing here has been changed by us except the map
([[procgen-islands#The tablet map|procgen-islands → The tablet map]]). Raw capture: `Probe() 'TabletProbe'` in the
client log; snapshot #1 is the whole tree, later numbers are what switched on and off.

## Where it is

- **Under the local player's rig:** `Original Player: [<name>] [<n>]/Player.XR.Body(Clone)/XR.Body/Tablet.XR(Clone)`.
  `TabletMapBridge.FindMapContent` reaches it from the rig root (`rigRoot.Find("Tablet.XR(Clone)/…")`, falling back
  to `XR.Body/…` from the root's parent). Only the local player's tablet is there to find; other players' tablets
  are not ours to touch.
- **It is one object for the whole session**, built when the player is, and kept while they are in the world.
- **Scale:** the root is about 1.09; `Canvases` holds world-space canvases at a world scale of 0.0005, so one UI
  unit is half a millimetre and a 960 × 1080 page is about 48 × 54 cm.
- **Hidden, not destroyed:** while the tablet is put away, `Canvases` has a `CanvasGroup` at alpha 0, not
  interactable, not blocking. Everything under it stays active and keeps its state.

## The tree

```
Tablet.XR(Clone)                    TabletControl, TabletTransitionControl, TabletCast, TabletResize,
│                                   TabletSFXControl, TabletButtonsControl, TabletGrabInteractable,
│                                   NetworkControl, TabletPropertiesNetworkDriver, TransformNetworkDriver, Rigidbody, Animator
├── Tablet.XR.Apps                  TabletAppsControl: apps that are not tabs
│   ├── Desktop - App               remote desktop (keyboard and mouse controllers)
│   ├── Mirror - App
│   └── VideoCalls - App            Agora
├── Tablet.XR.QuickActions          TabletQuickActionsControl: what the sidebar buttons do
│   └── AFKMode, POVMode, GoToSpawnPoint, FlyMode, SitStandMode, FBTCalibration
├── Canvases                        world-space canvases, scale 0.0005; CanvasGroup alpha 0 while put away
│   ├── Tablet.XR.PopUp             960×1080, sort 3: Somnium's own pop-ups
│   ├── Tablet.XR.Notifications     980×145 above the screen, sort 2: pop-up notifications, incoming calls
│   ├── Tablet.XR.StatusBar         960×75 along the top, sort 1
│   ├── Tablet.XR.SideBar           390×1190 to the left of the screen, sort 0
│   ├── Tablet.XR.Tabs              TabsContainer: one canvas per app
│   ├── Tablet.XR.BottomPanel       270×80 below: the Reconnect button
│   └── Tablet.XR.Tooltip           960×180 below: tooltips
├── Model/Tablet_v2                 the 3D tablet; BottomFrame has the physical buttons
├── GrabCollider (off)              on while the tablet can be grabbed
├── SFXHolder                       MainSpeaker, NotificationSpeaker, TabletStaticSFX
├── Tablet.XR.Pedestal (off)        the hologram pedestal that previews avatars and inventory items
├── CastGrabCollider (off)
└── KeyboardAttachPoint             where Somnium's virtual keyboard docks
```

## The home screen

`Canvases/Tablet.XR.Tabs/Tablet.XR.HomeTab` (`HomeTab`), a 960 × 1080 canvas. Its `ContentView` is a
**`GridLayoutGroup`**, cells 235 × 250, no spacing, four to a row, inside a mask. Order in the hierarchy is order on
screen, and switched-off buttons take no cell.

Every button is the same shape:

```
Tablet.XR.SwitchTabBtn - <App>      Image (SwitchTab_Default), Button → SwitchTabButton.OnClick,
│                                   SwitchTabButton, TabletTooltipForUIObjects, TabletSFXTriggerForUIObject
├── Icon                            Image 150×150 at (0, 20), sprite TabletButtonIcon_<App>
└── Text                            TextMeshPro, Exo-Medium SDF 2, 24 pt, white, centred, 185×32 at (0, 30);
                                    LocalizeStringEvent sets it from Somnium's localisation tables
```

| Button | Opens | Shown |
|---|---|---|
| My Avatar | `AvatarsTab` | yes |
| Social | `SocialTab` (chat; a `SocialSystem.Indicator` child lights for unread) | yes |
| Map | `MapTab` | yes |
| Browser | `BrowserTab_v2` | yes |
| Calibrate | `CalibrationsApp` | yes |
| Wearables | `WearablesTab` | yes |
| TS (Teslasuit) | — | off |
| Camera | `CameraTab` | yes |
| Mirror | `Tablet.XR.Apps/Mirror - App` (an `AppBtn`, `TabletAppButton`) | yes |
| Inventory | `InventoryTab` | yes |
| Worlds | `WorldsTab` | yes |
| Tip Jar | `TipJarTab` | yes |
| Desktop | `Tablet.XR.Apps/Desktop - App` (an `AppBtn`) | yes |
| Glimmr | `GlimmrTab` | yes |
| Settings | `OptionsTab` | yes |
| AppStore, Emotes, Gallery, Annihilator | — | off |

`SwitchTab` buttons open a canvas under `Tabs`; `AppBtn` buttons (`TabletAppButton`) start something under
`Tablet.XR.Apps` instead. The button's `onClick` has one persistent call, to its own `SwitchTabButton` or
`TabletAppButton`, which knows its target internally: nothing in the hierarchy names it.

## How an app opens

**An app is a canvas under `Tabs` whose `ContentView` is switched on.** Opening one switches `HomeTab/ContentView`
off and the app's `ContentView` on; going back reverses it. Every app canvas stays active throughout; only its
`ContentView` changes. Seen for Map, Inventory, Worlds, Settings, Social, Camera and Glimmr.

- **The Back button** (`StatusBar/LeftSideGroup/Tablet.XR.StatusBar.Back`) appears whenever an app is open and goes
  when Home is.
- **Wide apps resize the tablet.** Most app canvases are 1920 × 1080 offset 490 to the right (Browser, Avatars,
  Camera, Inventory, Social, Map, Settings, Wearables, Worlds, Calibrate); Tip Jar, Glimmr and the exit stats are
  940 × 1112, about the home screen's size. `TabletResize` widens the tablet for the wide ones, and the status bar's
  Resize button switches off while one is open.
- **The 3D frame's buttons** (`Model/Tablet_v2/BottomFrame`): Home, Chat, Map, Camera, Audio and Settings are
  `SwitchTabButton`s too (as `XRInteractableButton` + `MeshUIButton`), and Mute is a toggle.
- **Tab canvases:** `HomeTab` and most apps sort at 2, `OptionsTab` at 3; all use the event camera "Camera".

| Canvas | Component | Notes |
|---|---|---|
| `HomeTab` | `HomeTab` | the grid above |
| `BrowserTab_v2` | `BrowserTab` | |
| `AvatarsTab` | `AvatarsTab` | |
| `CameraTab` | `CameraTab`, `CameraAppManager`, `VCamManager`, `DroneInteractionControl` | also `WaterTrigger`, `ShutterSFX` |
| `InventoryTab` | `InventoryTab` | `SpawningSystem` (Spawn / Remove), spawned items list, filters; uses the pedestal |
| `SocialTab` | `SocialTab`, `SocialAppController` | global chat |
| `MapTab` | `MapTab` | `SomniumMap` (`MapControl`, `MapZoomControl`, `MapUIControl`, players and teleports modules) → `MapView/Viewport/Content`, `MapSideMenu`, `ParcelSearchView` |
| `OptionsTab` | `OptionsTab` | Settings; see below |
| `TipJarTab` | `TipJarTab`, `TipJarTabView`, `TipJarItem` | |
| `WearablesTab` | `WearablesTab` | `PreviewController` |
| `WorldsTab` | `WorldsTab`, `WorldsTabPedestal` | world list, detail view, instances |
| `CalibrationsApp` | `CalibrationTab` | |
| `GlimmrTab` | `GlimmrTab` | Somnium's social feed |
| `ExitStatsTab` | `ExitStatsTab`, `ExitStatsCaptureTab` | shown on leaving a world |

## Settings

`OptionsTab/ContentView` is a `CanvasGroupManager`: a `SelectorPanel` of General, Controls, Audio, Visual, Graphics
and Avatars (plus a `DebugButton`) switches between `OptionsTab.General`, `.Controls`, `.Audio`, `.Visual`,
`.Graphics`, `.Avatars` and `.DebugMenue`. Options are `OptionViewBase` + `FloatSlider` rows named `Option_<Name>`.
Section titles follow the player's language (the capture had Spanish ones: "Mundo", "Tableta").

**Audio** (`OptionsTab.Audio/Section - AudioOutput`), the volumes a player can set: `Option_MainVolume`,
`Option_VolumeVoice`, `Option_VolumeMusic`, `Option_VolumeAmbience`, `Option_VolumeTablet`, `Option_VolumeCars`;
present but switched off: `Option_VolumeEmvironment` (sic), `Option_VolumeAnnouncement`, `Option_VolumeParcels`.
**Where sounds go** (`AudioProbe`, 2026-11-02, since removed): one mixer, `MainMixer`, with groups Master,
OtherPlayersVoice, Outer Voice, Parcels, Vehicles, Music, **Ambience**, Environment, Tablet, Announcements, LoadIn,
Notifications and ARS. Every `AudioSource` in the world at load is put on **Ambience** (the Settings slider
*Ambience*); the video player's sound goes to **Music**; the tablet's own sounds to Tablet and Environment.
**A source made at runtime has no group** and bypasses the volume settings, but copying `outputAudioMixerGroup` from a
source that was there at load works: it then follows Ambience. The recipe for runtime sounds (SpaceScape asked): keep
one silent `AudioSource` in the scene from load and give every new source its group.

## World Settings

Our own section in Settings, built onto this app: [[world-settings]].

## Keeping our additions on the tablet

**Status (2026-11-03):** built, compiles clean, untested in-world.

Changing instance from inside the world left none of our apps on the tablet (Vulps, 2026-11-03); no log of it yet.
Everything we add (the Developer Market, the Almanac, the restock timer, World Settings) therefore:

- **looks for the tablet again every 2 s** and builds itself again on a new one (`TabletReplaced()`, logged), not only
  when the old one is destroyed;
- **keeps trying when a lookup fails** (every 5 s, logged) instead of switching itself off, since a tablet being
  rebuilt can be found before its home screen or Settings are.

The next instance change's log will say which of the two it was.

## The sidebar

`Canvases/Tablet.XR.SideBar` (`DynamicView`, `TabletSideBarAction`), 390 × 1190, left of the screen, under
`Mask/Content`:

- **`Tablet.XR.SideDrawer.QuickStatsPanel`** (112 tall): FPS, PING and Online count on top; below, a **World** panel
  (current world, world users) and an **Instance** panel (current instance, instance users) that take turns every few
  seconds (the probe saw them swap over 200 times).
- **`Tablet.XR.SideDrawer.ProfilePanel`** (370 × 334): profile picture with karma ring, `OpenMyProfileQuickAction`,
  level ("Lvl 16"), achievements (a 2-wide grid of 50 × 50), the user name as a TMP link whose id is the Somnium user
  id (`<link="effa171e-…">Vulps22`), and the Cubes balance.
- **`Tablet.XR.SideDrawer.QuickButtonsPanel`** (716 tall): a vertical layout of 370 × 110 quick-action buttons, top
  to bottom:

| Button | Kind | Label | Icon |
|---|---|---|---|
| `Tablet.XR.TimerQuickActionBtn - Go To` | `QuickActionButtonWithTimer` | "World Spawn" | `TabletButtonIcon_CityCenter`; has a `Timer` child (text "00" over a `progress_spinner_x1`), off until pressed |
| `Tablet.XR.BoolQuickActionBtn - Sit/Stand Mode` | `QuickActionButtonBoolStates` | "Standing VR" | `TabletButtonIcon_Stand` |
| `Tablet.XR.QuikActionBtn - FBT Calibration` (sic) | `FBTCalibrationQuickActionButton` | "Calibrate" | off without full-body tracking |
| `Tablet.XR.BoolQuickActionBtn - POV Mode` | `QuickActionButtonBoolStates` | "3rd Person POV" | |
| `Tablet.XR.QuickActionBtn - AFK Mode` | `QuickActionButton` | "Toggles AFK mode" | |
| `Tablet.XR.BoolQuickActionBtn - Fly Mode` | `QuickActionButtonBoolStates` | "Enable Fly Mode" | off: the world does not allow Somnium's flight |
| `NotificationBtn` | → `TabletSidePanelNotificationsController.OpenWindow` | "Notifications" | 370 × 82; an `Indicator` dot lights for new ones |

  Each is `Icon` (80 × 80 at x 30) and `Text` (225 wide), on sprite `QuickAction_Default`. The buttons' actions live
  in `Tablet.XR.QuickActions` (`GoToSpawnPoint`, `SitStandMode` …).
- **`Tablet.XR.SideBar.Notifications`** (off until opened): the notification list, a 6-row grid of 362 × 169 cards.

## The status bar

`Canvases/Tablet.XR.StatusBar/StatusBar`, 75 tall across the top:

- **Left** (`LeftSideGroup`): Back (only while an app is open), then the time and date ("22:20", "Sun, 01 Nov").
- **Right** (`Btns`, right-aligned, spacing 14): Refresh Worlds (off), **Exit World**, Logout (off, Steam only),
  **Exit Somnium**, Cast (off), **Resize**. All are `QuickActionButton`s, 180 × 50 (Resize and Cast 100 × 50).

## What changes while it is used

From every on/off the probe logged across both sessions:

- **Opening an app:** `HomeTab/ContentView` off, the app's `ContentView` on, Back on; Resize off for a wide app.
- **The stats panel's World/Instance swap**, constantly.
- **Tooltips:** `Tablet.XR.Tooltip/Content` on while hovering a button.
- **`GrabCollider`** on and off as the tablet is taken out and put away.
- **The pedestal** on while Inventory or Avatars previews an item, with the item spawned on its `SpawnPoint`.
- **Inside apps:** list items appear as `(Clone)`s (inventory items, worlds, Glimmr posts, achievements).
- **Notifications:** the sidebar button's indicator, and pop-ups with a count.

## Building on it

What the capture says is possible, for a tablet app, a Thatch bank or admin tools. The first home-screen app is built: the Developer Market ([[dev-tools]]), through `TabletBridge`.

- **Our own home-screen app.** Clone a `SwitchTabBtn` into `HomeTab/ContentView` (the grid places it), then on the
  clone: destroy `SwitchTabButton` (or it opens the original's app) and `LocalizeStringEvent` (or it resets the label),
  set `Icon` and `Text`, and add our own click listener. For the page, add our own canvas beside the others under
  `Tabs` and copy the switch: `HomeTab/ContentView` off, ours on.
  - **The catch:** Somnium's `TabsContainer` will not know our page is open. Its Home and Back may think they are
    already home and do nothing, so our page needs its own Back that switches `HomeTab/ContentView` on again, and
    should hide itself whenever any Somnium `ContentView` switches on.
  - **One player only:** the tablet is local, so a button built only on the developer's client (`Developer.IsLocal`)
    exists for nobody else.
- **A sidebar quick action.** Clone a quick-action button into `QuickButtonsPanel` (the vertical layout places it),
  strip its `QuickAction…Button`, and add our listener. This touches no tab state, so it is the safer way to open a
  panel of our own drawn over the tablet. Built: `TabletManager.AddQuickAction` clones World Spawn, strips its
  scripts, places the clone under it and grows `QuickButtonsPanel` by one button; the restock timer uses it
  ([[shop]]).
- **Reading, not writing:** the time, FPS, ping and online counts, the world and instance names and the user's name,
  level and Cubes are all plain `TextMeshProUGUI` texts, readable by path.
- **Matching the look:** Somnium's font is Exo-Medium SDF 2 (`TabletMapBridge.MapFont` borrows it), button sprites
  are `SwitchTab_Default` and `QuickAction_Default`, and icons are `TabletButtonIcon_*`, all reachable from the
  existing buttons' `Image`s.
- **Keep to one bridge.** Every path above belongs in one `TabletBridge`, beside `TabletMapBridge`, which checks each
  lookup and leaves the tablet alone when one fails.
