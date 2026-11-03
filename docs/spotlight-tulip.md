---
aliases:
  - "Spotlight Tulip"
tags:
  - crop
---
# Spotlight Tulip

An upgrade, not a crop to sell ([[fun-seeds]]): grown, a stage light rises out of the tulip and sweeps the Garden in
time with the music ([[light-show]]).

## The design

**Status (2026-11-03):** built; played in-world. In the Developer Market only, not the stalls.

- **Vulps's brief**: a light at about half the height of a Moonflower's beam ([[moonflower]]), pointing at the island
  and panning around it.
- **It pans with the tempo** (Vulps): a faster song pans faster.
- **Every tulip is a light; there is no holder.** A row of them is a rig, and no two are alike.

## How it works

**Status (2026-11-03):** built; played in-world.

- **Seed** `Seed_SpotlightTulip`: a tulip bulb. Buy 60, nothing to sell, grows in 40 s, `spawnWeight` 0.2.
- **Plant** `Plant_SpotlightTulip` (`SpotlightPlant`, `Show/`): a magenta tulip on a 0.5 m stem. Grown, its cup opens
  and a stage light (`Spotlight_Lamp`: a black can with fins and a glowing lens) rises out of it over 4 s, growing as it
  goes, to **half the height of its Garden's dome above the plant**, about 15 m. Rising, it looks up; risen, it turns
  down onto the Garden.
- **Panning**: a circle about its Garden's dome centre at 40% of the dome's radius, at the plant's height
  (`ShowStage.For(position)`). On a beat, one turn every 8 or 16 beats; with none, its own slow drift (16 to 30 s a
  turn). The angle accumulates, so a change of tempo never makes it jump. It flashes on every kick.
- **The light**: a real Spot Light (no shadows), a visible cone (`ShowBeam` on `Models/Show/LightCone`) and the lens,
  all one colour. Brightness follows one ARS band; quiet, it stays dimly lit.
- **Its own rig**: colour (one of six), band (one of four), beats per turn, idle speed and direction are picked from its
  planting time and place, so every client sees the same light.
- **Model**: made in Blender by `Models/SpotlightTulip/SpotlightTulip.blender.py` (plant, lamp and bulb; the Cup an
  empty). Materials `SpotlightTulip_*` and `Spotlight_*`, the lit ones with trip twins on `TripManager`.

> [!info] Networking
> Nothing is sent. Every client grows the same tulip on the same clock and picks the same rig from its planting time.

## Open

- What many real lights cost with lots planted.
