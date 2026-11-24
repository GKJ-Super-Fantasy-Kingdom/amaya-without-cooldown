# Amaya Without Cooldown

Buy a relic from Amaya, return to the map, and visit her again the same day. Each purchased slot displays as sold. Other offers retain their positions and normal coin prices. The selection changes with the game's day or an eligible native reroll. The mod uses Amaya's usual unlock, merchant screen, controls and prompts.

When you first open an older, unmodded save, Amaya generates eligible offers using the game's saved reroll state. Older saves can lack the identity of a previously purchased relic. Visits saved with this version retain the exact offer names and sold slots for the current stock.

A free reroll uses no Faith at the shop. When Continue replays one recorded reroll, the base game can deduct one Faith even if the original roll used a free allowance.

## Installation

Choose **Install with App** on the [Amaya Without Cooldown Thunderstore listing](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/AmayaWithoutCooldown/), select your **Super Fantasy Kingdom** profile, and launch with **Start modded**.

## Inside the Demon Shop

These in-game captures show the shop after a same-day return in 0.1.0, using simulated controller input. The shop description still mentions the base game's weekly limit.

![Reopened Demon Shop with a sold left slot and focus on Bait's buy button](docs/screenshots/0.1.0/reopened-shop-1x.png)

**1× UI scale.** The left slot is sold. Bait costs 7 coins and Cookies with Milk costs 9. The white outline marks the selected buy button.

![Reopened Demon Shop with the Reroll button selected below two available offers](docs/screenshots/0.1.0/reopened-shop-1p5x.png)

**1.5× UI scale.** In this run, Bush and Deforestation remain available. Reroll has focus and shows a cost of 3 Faith.

## Local import

1. In Thunderstore Mod Manager or r2modman, select **Super Fantasy Kingdom** and a dedicated test profile. Install the BepInEx dependencies listed in the manifest.
2. Open **Settings → Profile → Import local mod** and select `dist/AmayaWithoutCooldown-0.1.1.zip`. In manager versions with a search box, search Settings for **Import local mod**.
3. Choose **Start modded**. In the test profile's `BepInEx/LogOutput.log`, look for **Amaya Without Cooldown is ready.** Use a run where Amaya's shop has already opened.

## Controller playtest

- Record the day, relic names, coin and faith balances, and reroll price. With a relic's buy button focused, press Back and verify the shop closes. Reopen it, focus **Reroll**, and press Confirm. Repeat by clicking **Reroll** while another control has focus.
- Buy a relic. Hold Confirm across the transition, then release it. Verify one grant and one coin deduction. Reopen from the map without advancing time. The bought slot should read sold and the other relics should retain their positions and prices.
- Buy another slot. Try an unaffordable offer and back out. Check that balances only change on successful purchases, focus returns to Amaya on the map, and one Back press closes the shop once.
- Try a paid reroll and, when available, an earned free reroll. Check the displayed charge or free allowance exactly once, fresh eligible stock, and a valid selection afterward. With insufficient faith and no free reroll, the selection and balance should hold.
- Visit with all offers sold. Reach Reroll when eligible or Cancel with the pad. Repeat with keyboard and mouse, then switch back to the pad and check the prompt and focus.
- Save and Continue with partial stock, check the offers again at the game's restored day, then compare at the next natural day refresh. Check that a still-locked Amaya and another merchant behave as usual.

Version 0.1.1 passed offline stock and purchase tests against the game assemblies, including MoreRelics 1.1.0 with and without BakeryUsesWheat 1.2.0. The live shop and virtual-controller checks used 0.1.0. Physical controller testing and a full save-menu round trip on 0.1.1 remain open. [Compatibility results](tests/Compatibility/README.md)

## Build and package

Use the installed Windows game assemblies and the BepInEx 5 core DLLs from a mod manager profile. Run `dotnet build -c Release`. Set `-p:GameDir="..."` and `-p:ProfileDir="..."` to override the local defaults. Run `dotnet run --project tests/StockChecks -c Release` for the stock checks and `python package.py` to create the local-import archive under `dist/`. Packaging requires Python 3 and Pillow. The build reads the game and profile references and writes its output to the checkout.

## Contributing and license

Bug reports and pull requests are welcome. See the [contribution guide](CONTRIBUTING.md) for local testing and reporting instructions.

The original mod code is available under the [MIT License](LICENSE). Super Fantasy Kingdom, its game assets, and third-party dependencies retain their respective licenses and ownership.
