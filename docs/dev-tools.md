---
aliases:
  - "Dev tools"
tags:
  - tech
---
# Developer tools

Tools for testing in-world without paying for it: Vulps's, and anyone Vulps grants developer access to for the session
(see *Developer access*). Nobody else's client builds them, and the master refuses them to anyone else.

## Who the developer is

**Status (2026-11-02):** built, compiles clean.

`DevTools/Developer.cs`: Vulps22's Somnium user id, `effa171e-b6a2-43c2-8b71-ee1a312e706d`, as Somnium's player
container reports it (the same id in every world and session; it is also the link in the tablet's user name).
`Developer.Has(id)` is the owner or anyone granted access; `Developer.IsLocal` is that for the local player;
`Developer.IsLocalOwner` is Vulps alone, and `Developer.IsLocalMaster` also needs Vulps to be the master client.
`TabletProbe` runs only for `IsLocalMaster`.

## Developer access

**Status (2026-11-03):** built, compiles clean, untested in-world. Asked for by Vulps 2026-11-03.

The Developer Market's **Access** button (Vulps's tablet only) opens a list of everyone in the world in place of the market; *Done* brings the market back. Tapping a name
gives or takes **developer access** for this session: the Dev Market and every developer-only tool (the Developer panel
in Settings, the master accepting their market requests). The owner's own row cannot be pressed. Lit rows have access.

- **The master keeps the list** (`DevTools/DeveloperAccess`, on SceneManager): a grant is a fact. The owner's client
  asks (message 9 on SceneManager's messenger: the asker, the player, give or take); the master refuses an ask from
  anyone but the owner, updates the list and announces all of it (message 10) to everyone, and again to each late
  joiner. Every client holds the list, so a new master already has it.
- **For the session.** A grant outlives a dropout (someone who rejoins keeps it) and ends with the instance.
- **The app comes and goes with it.** The Dev Market's home button and page hide the moment access is taken, and the
  Developer panel in Settings follows within a frame.


## Notify everyone

**Status (2026-11-03):** built, compiles clean, untested in-world. Asked for by Vulps 2026-11-03.

Settings → World → the **Developer** section (right-hand column, developers only) → **Notify Everyone**: a text field, a keyboard of our own (Settings has none: digits,
letters, `, . ! ? '`, Space, Del, Clear, Aa for capitals) and **Send**, up to 120 characters. Everyone in the world sees it
as a [[notifier]] toast, the sender included; a player with Show Notifications off does not.

- **`DevTools/DeveloperBroadcast`** (on SceneManager, message ids 12–13 on its messenger): the sender asks with their
  id and the text (12, to all); the master checks `Developer.Has(sender)` and sends the text to all (13), and every
  client, the sender's included, shows it with `Notifier.Push`.
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
seeds, the seven fruits, the splif, the SPS Receiver, the Moonflower Seed, the Spotlight Tulip Bulb, then the Grow Wand.
A new item is appended to all three, and its prefab appended to `SceneNetworking._networkPrefabs` together with a new
GUID in `_networkPrefabsGuid` in one change (if the lengths differ for even one `OnValidate`, it regenerates every
GUID; appending through two `SerializedProperty` writes does exactly that).

**The SPS Receiver** (2026-11-02, for debugging SPS, Vulps): `prefab_items/SpsReceiver`, a pink ring (`Models/SpsReceiver/Ring`,
10 cm across) built from the splif with its smoke taken off, so it is a plain grabbable. At the ring's centre is a child
`SPS_Target` tagged `Player`, which the SPS plugin finds and fits with its receiver (a small visible sphere and a
trigger), so an eggplant brought to it bends toward it ([[crops#The eggplant easter egg|crops → The eggplant easter egg]]). Market only, never in the shops.

**The Grow Wand** (2026-11-03, Vulps): `prefab_items/GrowWand`, a 36 cm wooden stick with a glowing green tip, built
from the SPS Receiver's prefab (`GrowWand_Stick`, `GrowWand_Tip` materials; Unity's built-in cylinder and sphere).
Status: built, compiles clean, untested in-world.

- **Touch a planted slot, a plant or an unripe fruit with it** and it grows: a plant is fully grown at once and what it
  bears comes out ripe; already grown, what hangs on it ripens; a fruit ripens. A rooted crop in the ground ripens.
- **Event driven:** the tip's trigger (`OnTriggerEnter`, about 7 cm across) fires on the holder's client only, which
  asks the master (`DeveloperMarket.RequestGrow`, message id 15 on SceneManager's messenger: the requester, the Plot's
  network id and slot, or the plant's or produce's). A second touch of the same thing within a second is one ask.
- **The master decides**: it checks `Developer.Has(requester)`, then `Plant.GrowNow()` moves the planted time back by
  the growth duration and broadcasts the plant's state as usual, so every client and late joiner derives the rest;
  `Produce.RipenNow()` does the same for ripening. A touched plant bears ripe (`Plant.RipenStart`).

**Pictures** are textures, shown with `RawImage`, and the app icon is a texture made a sprite at runtime: the first
upload's `Sprite` references all arrived empty ([[runtime-spawn]] §4m). They are `Textures/Market/*.png`, rendered in the Editor from each prefab in a preview scene (256 × 256,
transparent, two directional lights), so the open scene is never touched. Re-render after changing a model.

**Not yet seen in-world, and worth watching**:

- Pull trigger and hold: the item should arrive in the hand; squeeze grip, let the trigger go, and it stays held.
- Whether Somnium's Home button brings the home grid back while our page is open.
