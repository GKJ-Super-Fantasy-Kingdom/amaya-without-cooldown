using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace AmayaWithoutCooldown
{
    // UICard.Callback dispatches the reward, shop callback and payment in sequence.
    // Reject a stale card before that list is entered, including after the select animation.
    [HarmonyPatch]
    internal sealed class PurchaseGuard
    {
        private static readonly ConditionalWeakTable<UICard, PurchaseGuard> Cards =
            new ConditionalWeakTable<UICard, PurchaseGuard>();
        private static readonly AccessTools.FieldRef<UICardRelic, string> CardIdentifier =
            AccessTools.FieldRefAccess<UICardRelic, string>("m_RelicIdentifier");
        private static readonly AccessTools.FieldRef<UICard, bool> Clickable =
            AccessTools.FieldRefAccess<UICard, bool>("m_CanBeClicked");

        private readonly BuildingAmayaShop shop;
        private readonly UIOverlayMerchant merchant;
        private readonly OfferStock stock;
        private readonly int view;
        private readonly int slot;
        private readonly string identifier;
        private readonly Relic relic;
        private readonly ResourceAmount[] costs;
        private readonly ResourceAmount[] originalQuote;
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Func<Relic, ResourceAmount[]>> Quote =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Func<Relic, ResourceAmount[]>>("m_GetRelicPriceCallback");
        private bool consumed;

        private PurchaseGuard(BuildingAmayaShop shop, UIOverlayMerchant merchant, OfferStock stock,
            int slot, Relic relic, ResourceAmount[] costs, ResourceAmount[] originalQuote)
        {
            this.shop = shop;
            this.merchant = merchant;
            this.stock = stock;
            this.view = stock.View;
            this.slot = slot;
            this.relic = relic;
            this.identifier = relic.GetIdentifier();
            this.costs = costs;
            this.originalQuote = originalQuote;
        }

        // Capture the provider quote before render-only price modifiers touch its array.
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(UIOverlayMerchant), "AddRelic")]
        private static void CaptureQuote(UIOverlayMerchant __instance, ResourceAmount[] costs,
            out ResourceAmount[] __state)
        {
            __state = ShopPatches.Owners.TryGetValue(__instance, out _) ? (ResourceAmount[])costs?.Clone() : null;
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(UIOverlayMerchant), "AddRelic")]
        private static void Attach(UIOverlayMerchant __instance, GameObject __result, int id, Relic relic,
            ResourceAmount[] costs, ResourceAmount[] __state)
        {
            if (!ShopPatches.Owners.TryGetValue(__instance, out var owner) || __result == null ||
                !ShopPatches.Stocks.TryGetValue(owner, out var stock))
                return;

            var card = __result.GetComponent<UICardRelic>();
            if (card == null) return; // The sold placeholder has no relic card.
            Track(card, owner, __instance, stock, id, relic, costs, __state);
        }

        internal static void Track(UICardRelic card, BuildingAmayaShop owner, UIOverlayMerchant merchant,
            OfferStock stock, int id, Relic relic, ResourceAmount[] costs, ResourceAmount[] originalQuote)
        {
            Cards.Remove(card);
            Cards.Add(card, new PurchaseGuard(owner, merchant, stock, id, relic, costs, originalQuote));
        }

        private bool SameView() => merchant != null && shop != null && stock.View == view && ShopPatches.Owners.TryGetValue(merchant, out var owner) && ReferenceEquals(owner, shop)
            && ShopPatches.Stocks.TryGetValue(shop, out var current) && ReferenceEquals(current, stock)
            && merchant.isActiveAndEnabled;

        private bool Available(UICardRelic card) => !consumed && SameView() && Clickable(card)
            && stock.Matches(slot, identifier) && CardIdentifier(card) == identifier
            && stock.IsCurrent(ShopPatches.StockKey(shop), ShopPatches.RerollCount(shop))
            && ReferenceEquals(ShopPatches.Available(shop, merchant, stock, slot, identifier), relic)
            && QuoteMatches(Quote(merchant)(relic))
            && (costs == null || ResourceManager.Instance.CanBeBuilt(costs));

        private bool QuoteMatches(ResourceAmount[] current)
        {
            if (current == null || originalQuote == null) return current == originalQuote;
            if (current.Length != originalQuote.Length) return false;
            for (int i = 0; i < current.Length; i++)
                if (current[i].type != originalQuote[i].type || current[i].amount != originalQuote[i].amount)
                    return false;
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "Select", new[] { typeof(UICard) })]
        private static bool BeforeAnimation(UICard __0) =>
            !Cards.TryGetValue(__0, out var guard) || guard.Available((UICardRelic)__0);

        [HarmonyPrefix, HarmonyPatch(typeof(UICard), "Callback")]
        private static bool BeforeTransaction(UICard __instance)
        {
            if (!Cards.TryGetValue(__instance, out var guard)) return true;
            if (!guard.Available((UICardRelic)__instance))
            {
                // If the item disappeared during the animation, leave the disabled selection UI.
                if (!guard.consumed && guard.SameView()) guard.merchant.Cancel();
                return false;
            }

            guard.consumed = true;
            return true;
        }
    }
}
