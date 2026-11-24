# Demon Shop compatibility

The audit used the Windows game assemblies from Steam build `26793117`, BepInEx `5.4.23.5`, and the local Amaya Without Cooldown `0.1.0` Release DLL. Each configuration ran in a separate Mono process with in-memory preferences and a synthetic day-8 shop. The external packages use their shipped settings. No game session, player profile or save file was opened.

| Assembly configuration | Passed assertions | Content exercised |
| --- | ---: | --- |
| Amaya alone | 31 | Reference shop with fixture relics |
| Amaya + ownlyme/MoreRelics 1.1.0 | 83 | All four shop-eligible additions, including Master Key's effect on eligibility |
| Amaya + ownlyme/MoreRelics 1.1.0 + ownlyme/BakeryUsesWheat 1.2.0 | 73 | Saved with both Master Key and Millstone on the shelf, then enabled BakeryUsesWheat before Continue |

## 0.1.1 audit regressions

Steam build `26872138` passed 23 stock checks and the following isolated assembly runs with the Release DLL.

| Configuration | Passed assertions |
| --- | ---: |
| Amaya alone | 43 |
| Amaya with MoreRelics 1.1.0 | 95 |
| Amaya with MoreRelics 1.1.0 and BakeryUsesWheat 1.2.0 | 85 |

The checkpoint case saves a purchased slot after a reroll and resets the in-memory preference as the game does after writing the building. Continue restores the shelf on the same stock day. An explicit reroll or a new stock day refreshes it.

The provider fixtures wrap the purchase callback, widen the native generator to four offers, and supply eligible relics outside its cached pool through a replacement delegate or a Harmony patch. They check one grant and one payment, the fourth sold position after reordering on Continue, and rejection after the item or its provider disappears. The stock checks cover 40 offers, truncated data and both prior save formats. A provider that changes private inventory rules without changing its generator, registry or shop eligibility exposes no change for Amaya to verify.

The fullscreen and controller results below were recorded on 0.1.0. The 0.1.1 verification used the game's managed assemblies and simulated providers.

## MoreRelics offers

MoreRelics registers eligible relics with the shop availability flags. The fixture read the released DLL's definitions and availability predicate, constructed the corresponding managed relics and ran the game's shop filter before narrowing the pool to exercise each purchase. Actual prefab cloning and acquired-relic effects require a Unity scene. [Registration source](https://github.com/ownlyme/super-fantasy-kingdom-mods/blob/7ecfd603ea25d87796149bb0293a504293ea7b1f/MoreRelics/Plugin.cs)

| Relic | Base coins | Day-8 coins | Amaya eligibility |
| --- | ---: | ---: | --- |
| Cornucopia | 8 | 10 | Eligible |
| Sous Chef | 8 | 10 | Eligible |
| Master Key | 6 | 8 | Eligible. Its shipped patch also admitted a level-gated fixture relic when the key was held. |
| Millstone | 10 | 12 | Eligible while BakeryUsesWheat is absent. MoreRelics skips registration when the bakery plugin is present. |
| Phalanx | 12 | — | Boss-only. The shop filter excluded it. |

Each eligible entry received one grant and one debit after a repeated callback. Its slot stayed sold on same-day return and after loading saved building data with the options reversed. The paid reroll at index one cost 2 Faith. An earned free reroll consumed one allowance and zero Faith at the point of use.

The three-mod run bought Master Key with Millstone and Cornucopia still available. On Continue with the bakery plugin present, both the purchased slot and the missing Millstone slot showed the native sold placeholder. Cornucopia stayed in its original slot at 10 coins. Restoring the offer list produced zero grants and zero coin debits. [Millstone condition](https://github.com/ownlyme/super-fantasy-kingdom-mods/blob/7ecfd603ea25d87796149bb0293a504293ea7b1f/MoreRelics/Millstone.cs)

## Other compatibility cases

- **Removed or replaced content.** The fixture removed an unsold relic, redirected its identifier to a different relic, replaced its prefab under the original identifier, revoked eligibility and replayed an old card callback. The stale card could neither grant a relic nor charge coins. A valid replacement became purchasable at its current quote on reopening.
- **Prices and stock timing.** Fixture Harmony patches changed a live purchase quote from 5 to 9 coins and a paid reroll from 2 to 5 Faith. The old purchase quote was rejected, the fresh quote charged once and an earned free reroll still cost zero at use. A stock seed that persisted across midnight kept the sold slot. Changing the seed within a day or explicitly regenerating the offers produced fresh stock.
- **Checkpoint accounting.** The installed game's `BeforeReroll` stored a count of one after an earned free reroll. On restoring the shop, its `OnSetData` debited one Faith in both the patched run and the comparison with Amaya's patches removed. The base game calculates this restoration charge from the stored reroll count. A provider with a custom rate or free allowance must account for the load path as well as the displayed price and spend hooks.
- **Catalog inspection.** ILM/CheatMenu 1.0.3 modifies earnings for coins and Faith. Those hooks do not supply shelf prices or reroll fees. We identified no released general Demon Shop pricing or refresh-timing provider in the catalog review. Those scenarios used fixture patches. [Catalog](https://thunderstore.io/c/super-fantasy-kingdom/) · [CheatMenu](https://thunderstore.io/c/super-fantasy-kingdom/p/ILM/CheatMenu/)

## Reproduce

Run from the project root in PowerShell with 64-bit Python and a .NET SDK. The optional DLL paths below refer to extracted releases of [MoreRelics 1.1.0](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/MoreRelics/v/1.1.0/) and [BakeryUsesWheat 1.2.0](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/BakeryUsesWheat/v/1.2.0/).

```powershell
dotnet build -c Release
dotnet run --project tests/StockChecks -c Release
python tests/Compatibility/run.py
python tests/Compatibility/run.py .work/external/MoreRelics.dll
python tests/Compatibility/run.py .work/external/MoreRelics.dll .work/external/BakeryUsesWheat.dll
```

The runner accepts `--game` and `--profile` to override the game and read-only BepInEx reference locations. Isolated build output and test configuration go under `.work/compatibility`. The runner uses the game's Mono DLL without a Unity player, so it may log unresolved engine calls while compiling methods. Check its exit code and the individual `PASS` lines.

The fixture executes native shop selection, filtering, pricing, saving, loading, reroll accounting and card callback dispatch. It substitutes card rendering, wallet storage and the final reward sink. Physical controller focus, scene-level prefab registration, relic effects and the full save-menu round trip remain in-game checks. See the [controller playtest](../../README.md#controller-playtest).

## Fullscreen playtest on November 24, 2026

The local Amaya 0.1.0 build was also exercised in the running Unity player at 1920 × 1080 borderless fullscreen with Weekly Contracts 0.1.0 and Daybook 0.4.0. Each test used a copy of a Human checkpoint that continued into day 13. The probe supplied 40 coins and six bread. For the free-reroll case it set one available allowance. Purchases, rerolls, contract claims, prices and wallet changes ran through the game and the installed mods.

- At 1× and 1.5×, a purchase granted the quoted relic once. Returning through Amaya's world object preserved the sold position and the remaining offers. A Weekly Contracts delivery and claim granted Hungry Shield, and the earlier shop sale still showed as sold on return.
- At 1×, Daybook recorded five coins spent for Bush of Life and one Faith for the paid reroll. The free reroll consumed the seeded allowance for zero Faith. Daybook's detailed ledger opened on the map with Alt. Its compact strip remained visible in the shop.
- At 1.5×, the compact strip originally covered the Demon Shop title. The corrected frame has six pixels between the strip and title, and 9.4 pixels between the description and cards. At 1×, the corresponding clearances were 167.8 and 22.5 pixels. The cards, costs, Reroll and Cancel remained on screen at both scales.
- A virtual gamepad exercised navigation, confirmation and Back. On the tested player the native target could survive dialogue while EventSystem selection was empty. The corrected build restored a selectable button after reopening and rerolling. Gamepad Back and keyboard Escape both returned to an interactable Amaya.
- A second live combination added ownlyme/MoreRelics 1.1.0. The title menu completed prefab registration before Continue. Four additions entered the full 17-item pool. Sous Chef appeared naturally for 10 coins, was granted once and stayed sold. A one-Faith reroll offered Cornucopia for 10 coins. Daybook's saved ledger agreed with both debits.

For a repeat playtest, wait for the title menu before selecting Play and Continue. Calling Continue directly from startup skipped MoreRelics' registration callback in the initial probe. Physical controller feel, other languages and post-acquisition cooking effects remain unverified. The checkpoint accounting comparison above used the isolated assembly fixture.

## Shortcut audit on game 0.8.068

The private player used Steam build `26872138`, the ten DLLs and BepInEx configuration from **GKJ - Controller Test**, a copy of the Human save, and default input bindings. The probe selected the Forester entry in the Lumberyard panel before testing the shop. It exercised the game's input system with virtual devices and recorded the selected object, displayed action labels, resource balances and stock.

| Control | Live check |
| --- | --- |
| Reopen | After a purchase, clicking Amaya or pressing controller A with the native world cursor over Amaya reopened the same shelf. The purchased slot remained sold. |
| Back | Escape and controller B each closed the shop with a relic buy button focused. Escape also worked with Reroll focused. The world target became interactable again. |
| Reroll | The button showed its Faith price in both input schemes. Clicking it while a buy button had focus charged the quoted cost once. With Reroll focused, Enter and controller A each rerolled once. While Cancel had focus, R and controller Y left stock, balances and owned relics unchanged. |

Amaya adds no global reopen or reroll binding and displays no shortcut glyph for either action. The packaged Controls section describes the native button and focus behavior. Physical controller operation and custom bindings remain outside this test.
