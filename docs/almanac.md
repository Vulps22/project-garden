---
aliases:
  - "Almanac"
tags:
  - design
  - interaction
---
# The Almanac

A tablet app for every player: a page for every crop, what it does and how to find it. It answers the question the
popcorn seed raised, "how would anyone know a popcorn grows a cinema screen?", and gives rare seeds a reason to be
hunted. Named for a farmer's almanac, the thing a farmer checks for what to plant and when. It is shaped like a
Pokédex (Vulps): a numbered entry per crop, and a silhouette for the ones you have not caught.

## The design

**Status (2026-11-03):** decided by Vulps; built, compiles clean, untested in-world.

- **Every page is open from the start**, so a player can read what a crop does before buying it. **Its picture stays a
  silhouette until the player has bought that seed from a stall**: the silhouette is the sign it is not collected yet.
  A seed from the Developer Market does not count (Vulps).
- **Every crop has a page.** Crops not in the stalls yet say **coming soon** (popcorn, for now). Developer-only items, the SPS Receiver among them, are not crops and have no page.
- **A page shows:** the picture, number and name; the kind (crop, root crop, regrowing crop and how many times it
  fruits, or upgrade); the stall sign's tagline and a description; **rarity as a tier with its share**, e.g.
  "Rare · 4.2% of stall rolls"; the seed's price and what the produce sells for; grow and ripen times; **how many stalls
  stock it right now**; and **the player's own record**: planted, harvested, sold, best single sale.
- Not in the first version: map pins for the stalls stocking it, and hints.

## Rarity

**Status (2026-11-03):** built.

A crop's share is its `spawnWeight` over the total of every crop in the stall pool (`WorldManager._buyables`). Tiers:
**Common** from 15%, **Uncommon** from 7.5%, **Rare** from 3%, **Very Rare** below.

| No. | Crop | Share | Tier |
|---|---|---|---|
| 01 | Carrot | 25.6% | Common |
| 02 | Turnip | 15.4% | Common |
| 03 | Pumpkin | 6.4% | Rare |
| 04 | Weed | 3.8% | Rare |
| 05 | Tomato | 21.8% | Common |
| 06 | Corn | 11.5% | Uncommon |
| 07 | Eggplant | 7.7% | Uncommon |
| 08 | Moonflower | 2.6% | Very Rare |
| 09 | Spotlight Tulip | 5.1% | Rare |
| 10 | Popcorn | — | coming soon |

The share is the base roll. Island stalls also weigh rare crops up by their own rarity ([[shop]]), so far stalls find
the Rare ones more often than the table says.

## How it is built

**Status (2026-11-03):** built, compiles clean, untested in-world.

- **`Almanac/AlmanacBook`** (on SceneManager): the entries and the record.
  - **Entries**, numbered in order: the stall pool in `WorldManager`'s order, then `_comingSoon`, the definitions of
    crops not in the stalls (`Buyable_Popcorn` under SceneManager; the Moonflower's and Spotlight Tulip's live on their
    seed prefabs). A crop added to the pool takes its place there by itself.
  - **The kind** comes from the plant its seed grows: `StandingPlant` an upgrade, `RootedPlant` a root crop,
    `MultiHarvestPlant` a regrowing crop.
  - **The record**, per crop: collected, planted, harvested, sold and best sale, saved on this device under `almanac`
    through `StorageBridge`, like World Settings, two seconds after a change. It listens to:
    - `Seed.AnyPurchased` (new, every client): collected when the buyer is this player and the seed came from a stall
      (`HasStall`);
    - `Seed.AnyPlanted` (new, every client, on the master's yes): planted, for the seed's buyer;
    - `Produce.AnyLifecycleChanged`: harvested, once a produce is harvested and this player holds it, counted once per
      produce (as the tutorial does);
    - `SellPoint.SaleOfCrop` (new): a sale with the crop's id, which `SaleCompleted` lacked.
  - The record counts **planted**, not grown: the master confirms a planting to every client, but nothing says who a
    grown plant belongs to.
- **`Almanac/AlmanacApp`** (on SceneManager): the home-screen app, built in code onto Somnium's tablet like the
  Developer Market ([[dev-tools]]): a grid of numbered cells, four across, and a page per crop, shown in place of the grid. A silhouette is the
  picture tinted black; the market's pictures have transparent backgrounds. An open page redraws when the record
  changes and every two seconds for the stall count. Icon: `Textures/Almanac.png`, a book with a leaf.
- **`SeedDefinition`** gained a `description`, set in each crop's `Init()` like its tagline, and a `picture`, the seed's
  market picture (`Textures/Market/<Crop>Seed.png`), set on each definition.
- **The stall count** is `ShopKeeper.Stocked(seedId)`: unsold seeds in the stock table this client keeps.

> [!info] Networking
> Nothing new is sent. The crops are the scene's, the stall count is the stock table every client already keeps, and
> the record is this player's, from events every client already receives.

> [!warning] Untested
> Whether the stall count matches what is out there: the stock table holds the seeds this client has loaded, so far
> islands' stalls may be missing from it. Worth comparing with a fly-around.
