---
aliases:
  - "Dev tools"
tags:
  - tech
---
# Developer tools

Tools that exist for Vulps alone, for testing in-world without paying for it. Nobody else's client builds them, and
the master refuses them to anyone else.

## Who the developer is

**Status (2026-11-02):** built, compiles clean.

`DevTools/Developer.cs`: Vulps22's Somnium user id, `effa171e-b6a2-43c2-8b71-ee1a312e706d`, as Somnium's player
container reports it (the same id in every world and session; it is also the link in the tablet's user name).
`Developer.IsLocal` is true on Vulps's client; `Developer.IsLocalMaster` also needs Vulps to be the master client.
`TabletProbe` runs only for `IsLocalMaster`.

## Native flight

**Status (2026-11-02):** built, compiles clean, untested in-world.

Settings → World → **Developer** panel → *Enable Native Flight*, a panel only the developer's tablet gets
([[tablet#World Settings|tablet → World Settings]]). On, Somnium's own fly mode is allowed again for the developer
(the sidebar's Fly Mode toggle comes back); glide stays off. Stored, and re-applied each time our flight takes hold of
the player. Ours is still running alongside it: switching Somnium's fly mode on while ours is in the air is untried.

## The Developer Market

**Status (2026-11-02):** built, compiles clean, untested in-world.

An app on Vulps's tablet (Vulps, 2026-11-02): a searchable, scrolling grid of every seed, fruit and the splif, each
with a picture. **Point a hand's ray at one and press grip**: it is pulled out of the tablet into that hand, free. A
seed comes already bought, so it can be planted; a fruit comes ripe, and grabbing it harvests it as usual.

**Played 2026-11-02** (first try): the app built, the page opened, a Weed Seed was asked for and spawned by the master.
The pictures were missing (sprites, above) and grip also grabbed the tablet, so it is now the trigger.

**On the tablet** (`MarketApp`, on SceneManager): built in code onto Somnium's tablet through `TabletManager` →
`TabletBridge` ([[tablet]]), only when `Developer.IsLocal`.

- A **"Dev Market" button** at the end of the home grid, cloned from Somnium's Map button with Somnium's tab switch,
  localisation and tooltip stripped off (cloned under an inactive holder, so none of them ever wakes).
- A **page of our own** beside Somnium's apps (`Tablet.XR.DeveloperMarketTab`): a canvas set up like the home
  screen's, with XRI's `TrackedDeviceGraphicRaycaster` so the rays can point at it. Opening it hides the home grid,
  as Somnium's own apps do.
- **Search** by an on-page keyboard (QWERTY, delete, clear) rather than Somnium's virtual keyboard: `SomniumKeyboard`
  is one of Somnium's built-in components, which must be in the scene from load, and docking it to a page made at
  runtime is untested. Matching is by name, anywhere in it.
- **The grid**: four across, 205 × 235 cells, scrolled by dragging with a ray. A cell lights while a ray is on it.
- **Back** on the page returns to the home grid. Somnium's tab system does not know our page exists, so its own
  Home may do nothing while the page is open; if any of Somnium's pages switches on (the frame's buttons, a
  notification), our page steps aside.

**Taking an item**: `MarketCell` learns which hand's ray is on it from XRI's `TrackedDeviceEventData`; `MarketApp`
reads each hand's **trigger** and, on a pull while that hand's ray is on a cell, asks for the item 12 cm out of the
tablet towards the head. Not grip, which grabs the tablet itself (Vulps, 2026-11-02). When the item arrives and the
trigger is still held, the hand holds it by script (`XRBaseInteractor.StartManualInteraction`), since XRI drops a
selection whose grip is not held; on letting the trigger go it is handed back to XRI (`EndManualInteraction`), which
keeps it if grip is held and drops it if not.

**Spawning** (`DeveloperMarket`, on SceneManager), messages 7 and 8 on SceneManager's messenger:

```
developer's client                         master
  Request(item, place, hand)   ── 7 ──▶    is the asker Developer.ID? spawn it there,
                                            a seed Purchase()d by the developer, a fruit Init()ed ripe,
                                            state re-sent at 0.25 / 0.75 / 1 s
  PutInHand: wait for it here  ◀── 8 ──    which NetworkId, which hand
  (up to 3 s), then XRInteractionManager.SelectEnter with that hand's Near-Far Interactor
```

> [!info] Networking
> Spawning is a fact, so the master does it, as for every runtime spawn; the developer need not be master. The
> master checks the asker's id against `Developer.ID` and refuses anyone else, logged. The id travels in the message
> and is not authenticated: a modified client could claim it, which is acceptable for a debug tool in a world where
> every client runs our assembly. The hand grab is local: grabbing takes authority as any grab does.

**The stock** is three matching arrays on `DeveloperMarket` (items, names, pictures), not an array of a custom class,
because arrays of custom `[Serializable]` classes arrive empty from the bundle export ([[weed]]). Order: the eight
seeds, the seven fruits, the splif, the SPS Receiver, then the Moonflower Seed and Spotlight Tulip Bulb. A new item is appended to all three, and its prefab appended to
`SceneNetworking._networkPrefabs` together with a new GUID in `_networkPrefabsGuid` (both at once: if the lengths
differ, `OnValidate` regenerates every GUID).

**The SPS Receiver** (2026-11-02, for debugging SPS, Vulps): `prefab_items/SpsReceiver`, a pink ring (`Models/SpsReceiver/Ring`,
10 cm across) built from the splif with its smoke taken off, so it is a plain grabbable. At the ring's centre is a child
`SPS_Target` tagged `Player`, which the SPS plugin finds and fits with its receiver (a small visible sphere and a
trigger), so an eggplant brought to it bends toward it ([[crops#The eggplant easter egg|crops → The eggplant easter egg]]). Market only, never in the shops.

**Pictures** are textures, shown with `RawImage`, and the app icon is a texture made a sprite at runtime: the first
upload's `Sprite` references all arrived empty ([[runtime-spawn]] §4m). They are `Textures/Market/*.png`, rendered in the Editor from each prefab in a preview scene (256 × 256,
transparent, two directional lights), so the open scene is never touched. Re-render after changing a model.

**Not yet seen in-world, and worth watching**:

- Pull trigger and hold: the item should arrive in the hand; squeeze grip, let the trigger go, and it stays held.
- Whether Somnium's Home button brings the home grid back while our page is open.
