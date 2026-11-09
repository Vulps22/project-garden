# Grow a Garden (Somnium Space): New Player Tutorial Redesign

## Problem
The current tutorial relies on a voiceover telling players to grab a scroll, walk it into the hut, plant, earn money and take flight. Nothing in the world guides them to do any of it, so new players can't follow it.

## Goals
- Replace the voiceover-only tutorial with a guided, step-by-step interactive one.
- Each step shows HUD text, highlights the next object, and waits for the player to complete the action before moving on.
- Keep tutorials fully separate from the shared multiplayer world.

## Constraints
- Limited to one scene.
- Tutorial must not collide with multiplayer.

## Approach
- Build a tutorial platform in the same scene, placed near the world height integer limit so it never overlaps the shared world.
- First-time players are teleported to the platform on spawn. Returning players skip the tutorial.
- Visual style: Animus (Assassin's Creed). Clean white void with glowing blue and white geometric lines, holographic feel.
- Use the game's real assets (scroll, hut, plot, barrel, etc.) inside the void so they stand out.
- Scene transitions use smoke: the current stage puffs out into smoke, the next stage comes in from smoke.

## Flow

### 1. Plot and scroll
- Player spawns on the platform and sees a plot with a highlighted scroll above it.
- HUD: "This is a plot. You must claim a plot before you can plant in it. Grab the highlighted scroll."
- Wait for the player to grab the scroll.
- Garden disappears in smoke, hut appears from smoke.

### 2. Claim the plot in the hut
- HUD: "Take the scroll into your hut to claim the plot."
- Wait for the player to walk through the door.
- Scroll disappears, hut behaves as it normally would, the deed scroll appears with the player's name on it, and the fire lights.
- HUD: "This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming."
- Hut melts into smoke.

### 3. Buy point
- Only a barrel (buy point) is shown.
- HUD: "This is a buy point. You will find these dotted throughout the Fractured Isles. If there is a seed inside it, you can buy the seed for the price listed on the barrel."
- HUD: "Buy the seed."
- Wait for the player to grab and buy the seed.
- Barrel disappears, scoreboard takes its place showing the player has no money.

### 4. Scoreboard
- The purchase succeeds despite the scoreboard showing 0.
- HUD: "This is your scoreboard. This is how you see how much thatch you currently own. Thatch is the currency we use in the Fractured Isles."
- Scoreboard melts away, plot reappears.

### 5. Plant
- HUD: "Plant the seed by pushing it into the dirt pile on the ground."
- When planted, briefly show: "Yes, I know it looks like an anus. But it's a free asset."
- HUD: "Wait for the seed to turn into a carrot. Carrots take 10 seconds to grow."

### 6. Harvest
- When ready, notify the player that something is ready to harvest.
- HUD: "Grab the carrot out of the ground."
- Plot melts away, a sell point appears.

### 7. Sell
- HUD: "Bring the carrot to the sell point to sell it."
- Wait for the sale.

### 8. Tablet and almanac
- HUD: "Open your tablet."
- Wait for the player to open the Somnium tablet.
- Message: "You can use the almanac to see all the seeds available in the game. It shows how often they spawn, how many there are in the world, and if you haven't found one yet, you will see a silhouette."

### 9. Seed types
- Message: there are three types of seed in the world.
  - **Crops**: grow into things you harvest and sell.
  - **Buffing seeds**: give multipliers to the size and value of your crop. Buffs always exist on a timer.
  - **Upgrade seeds**: raise the base value of everything you grow, without applying a multiplier. Permanent for as long as you're in the world.
- Example numbers for the copy: a buff might give 3x base value; an upgrade might raise base value by 5%.
- Visuals: the buffing and upgrade seeds loop in place, emerging from smoke and returning to smoke while the text is shown. Visuals for crops are not decided yet.

### 10. Send-off
- HUD: "It is now time to spread your wings and fly."
- The ground becomes a solid disc platform.
- Instruct the player to make a T pose.
- If no T pose is detected within 5 seconds, show: "If your avatar is too small, the game will not detect your T pose. Try scaling up your avatar."
- Once the T pose is detected, HUD: "Press jump."
- On jump, teleport the player to the spawn point island in the main world. Tutorial is complete.

## Open items
- Visual treatment for the three seed types (crops especially) is undecided.
- Naming: confirm "Fractured Isles" vs "Fractured Islands" for all copy.
- Exact buff and upgrade numbers above are examples only.
