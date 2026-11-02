---
aliases:
  - "Almanac"
tags:
  - design
---
# The Almanac

**Status (2026-11-02):** an idea, agreed worth building (Vulps); not designed in full, nothing built.

A tablet app for every player: a page for every crop, what it does and how to find it. It answers the question the
popcorn seed raised, "how would anyone know a popcorn grows a cinema screen?", and gives rare seeds a reason to be
hunted. Named for a farmer's almanac, the thing a farmer checks for what to plant and when.

## What a page could show

- **Picture, name, kind**: crop, regrowing crop, or upgrade (popcorn: "grows the Garden's cinema screen").
- **What it does**: grow time, ripening, harvests per season, buy and sell prices, anything special (weed: "eat it
  raw, if you dare").
- **Spawn chance** (Vulps): from the crop's `spawnWeight`, as a share of all rolls, or a tier (common to very rare).
  Island stalls favour rare crops by their rarity ([[shop]]), so "rarer stalls favour this" says more than one number.
- **How many are out there now** (Vulps): every client already keeps the stall stock table (`ShopKeeper.StockAt`),
  so "3 in stalls right now" is a local count, live, with nothing sent.
- **Where**: which islands have it in stock, perhaps a pin on the tablet map ([[procgen-islands#The tablet map|procgen-islands → The tablet map]]).
- **Your record**: grown, harvested, sold, best sale.
- **Hints before discovery**: "found only on rare islands".

## Discovery

A page unlocks the first time the player sees the seed in a stall (or buys, plants, or is handed one). Unknown
crops show as a "?" silhouette, so a player knows how much is left to find. Stored per player in local storage, like
World Settings ([[tablet#World Settings|tablet → World Settings]]).

## How it would be built

- **A home-screen app** like the Developer Market ([[dev-tools]]), but for everyone: `TabletBridge` already adds a
  button and a page. The market's grid and pictures (`Textures/Market/`, textures not sprites) carry over.
- **Its data is the crops' definitions** (`SeedDefinition` and subclasses), plus a description and kind each, which
  they do not have yet.
- **Nothing new on the wire**: stock counts come from the table every client keeps; discovery and records are the
  player's own.

## Open

- Discovery by sight, or only by buying?
- Spawn chance as a percentage, a tier, or both?
- Which of the rest belong in the first version?
- The stall sign now carries each crop's one-line `tagline` (2026-11-02, [[shop]]); the Almanac's description can
  start from it.
- It is shaping up like a Pokédex (Vulps): a numbered entry per crop, seen versus caught.
