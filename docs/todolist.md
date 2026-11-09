---
aliases:
  - "To do"
tags:
  - tracking
---
# To do

One line per piece of work, pointing to the doc that holds everything about it. Notes and design live in the topic
docs, never here; an item is removed once it is done. Milestones are in [[roadmap]].

## Before any non-test upload

- [ ] **Remove `ScreenStandIn`** once screens near you only works: it hides every screen but world spawn's behind a notice. [[popcorn]]
- [ ] **Screens near you only:** ask Dene how Base Reality keeps far video players from playing locally, then close far and sunk screens' media. [[towns]]
- [ ] **Towns:** fly the three towns and claim, plant and sell in an outer Garden; 14 video players; popcorn screen and Moonflower dome there; music switching between screens. [[towns]]
- [ ] `EconomyManager._startingBalance` is 10. [[economy]]
- [ ] `TutorialManager._rememberProgress` and `TipNarrator._rememberProgress` are on (each logs a WARN at startup when off). [[tutorial]]

## Cleanup
- [ ] **Event driven, phase 1 (built, untested):** `PlayerTracker` samples position once and raises events; sectors, islands, the Gardens' bubbles (drawing is now Unity's LODGroups, rescaled by Draw Distance), the screen ARS listens to, earshot, the dome's night, the world floor, authority by sector (message id 14) and the stranded check now run on events. Test: move items between sectors with two players, fall out of the world, drop something into the void, walk in and out of a dome. [[principles]] [[networking]]
- [ ] **Event driven, phase 2 (built, untested):** the master owns the stock table (ids 16–19); stalls listen to their own stall; purchases decide on the grab event; seeds and produce ask for their state on spawn; `ReportLooseState` gone. Test with two players: buy at a pinned and an unpinned stall, a late joiner sees every stall stocked, a restock cycle with a peer watching, a Developer Market fruit arrives ripe on the other client, no "already stocked" or "not its stall's stock" lines in anyone's log. [[shop]]
- [ ] **Event driven, phase 3:** the tablet (lookups, new-tablet checks, `MapOverlay`). [[principles]]
- [ ] **Event driven, phase 4:** a second audit of the rest (flight/climb device scans, `ButtonProbe`, `TripManager` re-melt, `SettingsPanel`, `ScreenStandIn`, per-frame state checks without timers). [[principles]]

- [ ] **Next:** work through every report from the 2026-11-04 playtest in one pass. [[playtest-2026-11-04]]
- [ ] **Rebalance (built, untested):** pumpkin sells 75, island finds pay more the rarer they are, rare seeds cost more, island stalls lean common. Test: an island trip turns up mostly carrot, tomato, turnip and pumpkin; the Almanac's tiers read sensibly; a grown tomato and corn pay back well. [[crops#Balance|crops → Balance]]
- [ ] **Upgrade bonus (built, untested):** grow a popcorn in your Plot and sell a carrot from that Plot: 15 → 16 (prices round up); a corn cob 55 → 58. With all three upgrades a pumpkin sells for 82, a corn cob for 74, a weed bloom for 190. [[economy#Upgrade bonus|economy → Upgrade bonus]]
- [ ] **Thatch persists between sessions:** backend prototype built (untested deployed); next deploy it, then the world side: account prompt, reporting approved changes, loading a joiner's balance. [[persistence]]
- [ ] **Thatch sinks:** choose from the ranked list. [[thatch-sinks]]
- [ ] Deeds are part of the Garden, hidden while claimed (built, untested). Test in [[plot-ownership#The deed is part of the Garden|plot-ownership → The deed is part of the Garden]].
- [ ] **Left-over seeds end themselves; item ids** (built, untested): with the host at the Garden, wait out a restock cycle on an island, then buy from its stalls: every sign matches its seed, every seed bought plants, and `CheckSuperseded … ending it` lines appear in the log of whoever was on the island. [[shop]]
- [ ] **Large messages are split** (built, untested): a joiner's stalls show their stock and no "payload is too large" line appears in the master's log; the economy's ids moved off 1–2, which clashed with the world seed and sectors (no "seed 0 ignored" warning, no exception on a join). [[networking#Messaging|networking → Messaging]]
- [ ] Handover gaps built, untested. Test with three players: the second leaves and rejoins (board filled); the master leaves with a tomato half-picked and a carrot in the ground (the plant re-bears, pulling the carrot frees its slot, balances kept, pinned stalls stay stocked); a seed left on an empty island is still there for a joiner; the master leaves while someone holds a seed just taken from a stall (it is bought or refused, not stuck) and while someone sells (they are paid once). [[networking#Handover gaps|networking → Handover gaps]]
- [ ] One seed grew two plants in neighbouring slots: the master now accepts each seed once (built, untested). [[plot-ownership#One seed, one plant|plot-ownership → One seed, one plant]]
- [ ] Flight with a seed in each hand (built, untested): fly home from an island carrying two seeds; flight starts from standing and does not land mid-air; grabbing, planting and selling still work with carryables on `PhysicalObject`. [[flight#What counts as ground|flight → What counts as ground]]
- [ ] Held items bounced for other players: proxies are now kinematic and a held item is left to XRI (built, untested). Test: two players, each watches the other hold and drop a seed. [[networking#Fusion 2.1|networking → Fusion 2.1]]
- [ ] Missing script in CM's `Networked Script Enable Object Example.prefab`: Vulps's call. [[community-modules]]
- [ ] The scroll dispenser (`DispensingEntity`) re-broadcasts three times after spawning: replace with the spawn-time state request seeds use (event-driven phase 4). [[networking#Late-join sync|networking → Late-join sync]]
- [ ] 13 materials converted to URP by hand live in untracked asset packages; a fresh clone gets magenta. [[packaging]]

## Crops

- [ ] Growth pivots: a `ScalePivot` per crop prefab. [[crops#Growth pivots|crops → Growth pivots]]
- [ ] Roll a plant's stats (`HarvestCount`, value, size), perhaps from the stall's rarity. [[crops]]
- [ ] **Fun seeds in the stalls (untested there):** buy popcorn, a Moonflower and a Spotlight Tulip from a stall and grow each. [[fun-seeds]]
- [ ] **Yggdrasil (built, untested):** from the Developer Market, plant it and watch the rune fountain swell as the sapling grows (not with the wand, which skips it), then watch the stream build the tree to the rumble; the slot frees; climb it and stand on the canopy; grow a carrot under it and sell (20× for a whole growing); a second won't plant; claim the Plot again after its owner leaves, or wait out the 30 minutes, and watch it fall. Then set the sapling's growth time and the multiplier. [[yggdrasil]] [[economy#Crop scale|economy → Crop scale]]
- [ ] Seed ideas: GitHub issues #44, #47. A `PlantableEntity` (planting a sword to grow an auto-harvester) is an idea, not a plan.

## Tablet

- [ ] The Almanac: try it in-world (silhouettes, collecting from a stall, the record, the stall count). [[almanac]]
- [ ] Developer access: grant and take it from someone in-world. [[dev-tools]]
- [x] Video controls were a sliver because the player was squashed from load; fixed by squashing only the picture (2026-11-02), confirmed in-world. [[popcorn]]
- [x] Developer Market tried in-world and working (2026-11-02). [[dev-tools]]
- [x] AudioProbe read and removed 2026-11-02: world sounds go to Ambience in MainMixer; copying the group onto a runtime source works ([[tablet#Settings|tablet → Settings]]).
- [ ] Remove `TabletProbe` when Vulps says (it now runs only for the developer as master). The tablet is documented in [[tablet]].
- [ ] Restock timer: check it sits under World Spawn without pushing the sidebar out of shape, and agrees between two players and after the master leaves. [[shop]]
- [ ] Notifier: a toast and chime when someone joins; silent within 5 s; Show Notifications off silences it.; "Something is ready to harvest" when your crop ripens; "You have been given Developer Access" when granted; toasts now last 3 s. [[notifier]]
- [ ] Change instance from inside the world: do the apps, the timer and World Settings come back? Send the log either way. [[tablet]]
- [ ] Plant a seed someone else bought (handed over, or from the Developer Market) in your own Plot. [[plot-ownership]]
- [ ] Notify Everyone from Settings → World → Developer: reaches another player and yourself; the keyboard fits the panel. [[dev-tools]]
- [ ] Frame time benchmark: does a PERF line appear every minute, does the first say whether CPU/GPU timing is available, and record a day and a night baseline in the same spot. [[testing]]
- [ ] Grow Wand: touch a seedling's soil, a grown bearing plant, a carrot in the ground; does a second player see it grow? [[dev-tools]]
- [ ] Tutorial: join as a new player and walk all ten steps (smoke, glow, panel, the carrot growing, the tablet step, the seed loop, T pose then jump to the spawn); Skip and Repeat from Settings → World; does the hut's fire crackle follow your volume? [[tutorial]]
- [ ] Open the tutorial to everyone once it is refined and tested: drop the owner-only condition in `TutorialManager.Available`. [[tutorial]]
- [ ] Record the tutorial's voice lines (`sounds/tutorial2_*`, list in tutorial → Voice lines), then rerun Build Tutorial Platform. [[tutorial]]
- [ ] Flight rework (not yet): a jump to take off unless already falling, and thresholds that follow the player's size; log the size signals first. [[flight]]
- [ ] 2D (desktop) players: a pass over controls and gameplay once the tutorial's first pass is done; the tutorial ignores them until then (Vulps). [[tutorial]]

## Changelog

- [ ] A changelog on the hut: it fetches the repo's `latest` GitHub release and shows that release's description. Not designed yet.

## Parked

- [ ] Flight sinks on some launches; cancelling the pull made it rubber-band. Waiting on the Somnium team.
  [[flight#Falling with grace, 2026-11-01|flight → Falling with grace]]

- [ ] Procgen islands phase 2: more island prefabs. [[procgen-islands]]
