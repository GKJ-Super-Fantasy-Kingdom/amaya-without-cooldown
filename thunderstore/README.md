# Amaya Without Cooldown

Once you unlock Amaya's Demon Shop, you can buy a relic and visit her again immediately. On a same-day return, the purchased slot reads **Sold** and the other relics are still available at their usual coin prices.

## Features

- Returning to the shop is free. Each purchase marks just its own slot as sold.
- Stock changes daily. Use **Reroll** for a fresh selection sooner, at the displayed Faith price or with an earned free reroll.
- Continue restores the offers and sold slots saved for that day's shop.

## How to Use

1. Click Amaya on the map, or move the controller cursor over her and press **A**.
2. Click the price beneath a relic to buy it, or focus its buy button and press Confirm (**Enter** or controller **A** by default). After the purchase, select Amaya again to buy another offer.
3. To change the selection, click **Reroll**, or focus it and press Confirm. To leave without buying, click **No, thanks!** or press **Escape** or controller **B**.

## Screenshots

These in-game captures show the shop after a same-day return in 0.1.0, using simulated controller input. The shop description still mentions the base game's weekly limit.

![Reopened Demon Shop with a sold left slot and focus on Bait's buy button](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/amaya-without-cooldown/d3c6338b5ad73d71e17f743d43be767d84940fa5/docs/screenshots/0.1.0/reopened-shop-1x.png)

**1× UI scale.** The left slot is sold. Bait costs 7 coins and Cookies with Milk costs 9. The white outline marks the selected buy button.

![Reopened Demon Shop with the Reroll button selected below two available offers](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/amaya-without-cooldown/d3c6338b5ad73d71e17f743d43be767d84940fa5/docs/screenshots/0.1.0/reopened-shop-1p5x.png)

**1.5× UI scale.** In this run, Bush and Deforestation remain available. Reroll has focus and shows a cost of 3 Faith.

## Compatibility

- With [Weekly Contracts](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/WeeklyContracts/) and [Daybook](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/Daybook/) installed, a live playtest on Amaya 0.1.0 confirmed that claiming a contract reward kept the purchased slot sold and that the ledger recorded shop spending.
- Amaya 0.1.1 passed offline purchase and reload tests with [MoreRelics 1.1.0](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/MoreRelics/), both alone and alongside [BakeryUsesWheat 1.2.0](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/BakeryUsesWheat/). If a content mod removes a saved offer, its slot shows **Sold** and the other offers keep their positions.
- Older saves can lack the identity of a previously purchased relic. On the first visit, Amaya uses the game's saved reroll state to generate eligible offers.
- A free reroll spends zero Faith at the shop. When Continue replays one recorded reroll from a checkpoint, the base game can deduct one Faith even if that reroll was free.

## Installation

1. Choose **Install with App** on the [Thunderstore listing](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/AmayaWithoutCooldown/) and select your **Super Fantasy Kingdom** profile.
2. When prompted, install [BepInExPack](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/BepInExPack/) and [HideManagerGameObject_Config](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/HideManagerGameObject_Config/).
3. Choose **Start modded** and visit Amaya in a run where her shop is unlocked.
