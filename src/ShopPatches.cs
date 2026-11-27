using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AmayaWithoutCooldown
{
    [HarmonyPatch]
    internal static class ShopPatches
    {
        internal static readonly ConditionalWeakTable<BuildingAmayaShop, OfferStock> Stocks =
            new ConditionalWeakTable<BuildingAmayaShop, OfferStock>();

        private static readonly ConditionalWeakTable<OfferStock, Dictionary<int, ResourceAmount>> SoldPrices =
            new ConditionalWeakTable<OfferStock, Dictionary<int, ResourceAmount>>();

        internal static readonly AccessTools.FieldRef<BuildingObjectShop, int> RerollCount =
            AccessTools.FieldRefAccess<BuildingObjectShop, int>("m_Rerolls");

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingAmayaShop), "OnSetData")]
        private static void Load(BuildingAmayaShop __instance, ref List<int> data)
        {
            Stocks.Remove(__instance);
            Stocks.Add(__instance, OfferStock.Read(data, StockKey(__instance)));

            // Let vanilla restore reroll pricing and its profile accounting. Omit the
            // purchase closure/slot flags from this call only; the setup-day gate runs separately.
            if (data.Count > 1)
            {
                data = new List<int>(data);
                for (int i = 1; i < Math.Min(5, data.Count); i++) data[i] = 0;
                while (data.Count < 5) data.Add(0);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingAmayaShop), "OnSetData")]
        private static void AfterLoad(BuildingAmayaShop __instance)
        {
            if (Stocks.TryGetValue(__instance, out var stock)) stock.LoadedCounter(RerollCount(__instance));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingObjectShop), "SaveData")]
        private static void Save(BuildingObjectShop __instance, List<int> __result)
        {
            if (__instance is BuildingAmayaShop shop && Stocks.TryGetValue(shop, out var stock))
                stock.Write(__result);
        }

        // A normal opening and an explicit stock regeneration both call GenerateRelics(true).
        // Only the opening should replay the saved stock.
        [ThreadStatic] private static BuildingAmayaShop opening;

        internal static readonly ConditionalWeakTable<UIOverlayMerchant, BuildingAmayaShop> Owners =
            new ConditionalWeakTable<UIOverlayMerchant, BuildingAmayaShop>();

        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(BuildingAmayaShop), "Merchant")]
        private static void Open(BuildingAmayaShop __instance, UIOverlayMerchant __0, out BuildingAmayaShop __state)
        {
            Owners.Remove(__0);
            Owners.Add(__0, __instance);
            __state = opening;
            opening = __instance;
        }

        [HarmonyFinalizer, HarmonyPatch(typeof(BuildingAmayaShop), "Merchant")]
        private static void EndOpen(BuildingAmayaShop __state) => opening = __state;

        private static readonly AccessTools.FieldRef<BuildingObjectShop, List<string>> Pool =
            AccessTools.FieldRefAccess<BuildingObjectShop, List<string>>("m_Options");
        private static readonly System.Reflection.MethodInfo Eligibility =
            AccessTools.Method(typeof(BuildingAmayaShop), "CanAddRelic");
        private static readonly System.Reflection.MethodInfo Seed =
            AccessTools.Method(typeof(BuildingAmayaShop), "GetRandomSeedModifier");

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingObjectShop), "GetDescription")]
        private static void ShopDescription(BuildingObjectShop __instance, ref string __result)
        {
            const string original = "Make a deal with Amaya to get a powerful relic!\n" +
                "Stock changes daily. You can only get one per week though, and her price changes weekly as well.";
            if (__instance is BuildingAmayaShop && __result?.Trim().Replace("\r\n", "\n").Replace(" \n", "\n") == original)
                __result = "Buy a relic, then return for the remaining offers.\nStock changes daily. Prices change weekly.";
        }

        internal static int StockKey(BuildingAmayaShop shop) => (int)Seed.Invoke(shop, null);

        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Func<Relic>> Generator =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Func<Relic>>("m_GetRelicCallback");

        private static string Supplier(UIOverlayMerchant merchant)
        {
            var method = Generator(merchant)?.Method;
            if (method?.DeclaringType == null) return null;
            // A supply mod may replace the delegate or patch the original generator.
            // Persist the method and patch owners so a withdrawn provider cannot authorize its old offer.
            string identity = method.DeclaringType.Assembly.GetName().Name + "/" + method.DeclaringType.FullName + "/" + method.Name;
            var patches = Harmony.GetPatchInfo(method);
            return patches == null ? identity : identity + "/" + string.Join("/", patches.Owners.OrderBy(owner => owner, StringComparer.Ordinal));
        }

        internal static Relic Available(BuildingAmayaShop shop, UIOverlayMerchant merchant, OfferStock stock, int slot, string id)
        {
            if (string.IsNullOrEmpty(id) || (!Pool(shop).Contains(id) && !stock.SuppliedBy(slot, Supplier(merchant)))) return null;
            var relic = AddressablesManager.Instance.GetRelic(id);
            if (relic == null || relic.GetIdentifier() != id) return null;
            return (bool)Eligibility.Invoke(shop, new object[] { relic }) && RelicManager.Instance.CanAddRelic(id)
                ? relic : null;
        }

        [HarmonyPrefix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(BuildingRelicShop), "GetRelic")]
        private static bool HasOptions(BuildingRelicShop __instance, List<string> ___m_Options) =>
            !(__instance is BuildingAmayaShop) || ___m_Options.Count != 0;

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "GenerateRelics")]
        private static bool BeforeCards(UIOverlayMerchant __instance, ref bool reroll,
            Func<Relic, ResourceAmount[]> ___m_GetRelicPriceCallback,
            List<string> ___m_ChosenRelics, out OfferStock __state)
        {
            __state = null;
            if (!Owners.TryGetValue(__instance, out var shop))
                return true;

            var stock = Stocks.GetValue(shop, _ => new OfferStock());
            __state = stock;
            bool refresh = reroll && !ReferenceEquals(opening, shop);
            if (!stock.SelectStock(StockKey(shop), RerollCount(shop), refresh))
            {
                SoldPrices.Remove(stock);
                reroll = true;
                return true;
            }

            stock.RestoreOffers(___m_ChosenRelics);
            reroll = false;
            // Native rendering prices the lookup result before AddRelic. Leave a hole in the
            // original slot when a content pack disappeared or the active rules reject its ID.
            for (int slot = 0; slot < ___m_ChosenRelics.Count; slot++)
            {
                string id = ___m_ChosenRelics[slot];
                if (stock.IsSold(slot))
                {
                    var card = __instance.AddSold(-1, false);
                    if (card != null)
                    {
                        ResourceAmount? paid = SoldPrices.TryGetValue(stock, out var prices) &&
                            prices.TryGetValue(slot, out var price) ? price : (ResourceAmount?)null;
                        AmayaShopView.ShowPurchased(__instance, card, id, paid, ___m_GetRelicPriceCallback);
                    }
                    continue;
                }
                var relic = Available(shop, __instance, stock, slot, id);
                if (relic == null)
                    __instance.AddSold(-1, false);
                else
                    __instance.AddRelic(relic, ___m_GetRelicPriceCallback(relic), slot, true, true);
            }
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIOverlayMerchant), "GenerateRelics")]
        private static void AfterCards(OfferStock __state, List<string> ___m_ChosenRelics)
        {
            __state?.Remember(___m_ChosenRelics);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIOverlayMerchant), "AddBuyButton")]
        private static void PriceContrast(UIOverlayMerchant __instance, UICard spawned, ResourceAmount[] costs, Transform __result)
        {
            if (Owners.TryGetValue(__instance, out _) && __result != null)
                AmayaShopView.ShowUnavailable(spawned as UICardRelic, costs, __result);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "AddRelic")]
        private static bool BindSlot(UIOverlayMerchant __instance, Relic relic, ref int id,
            List<string> ___m_ChosenRelics, ref GameObject __result)
        {
            if (!Owners.TryGetValue(__instance, out var shop)
                || !Stocks.TryGetValue(shop, out var stock))
                return true;

            // Generated relic buttons normally capture -1. Give the native buy callback its slot.
            string identifier = relic.GetIdentifier();
            if (id == -1) id = ___m_ChosenRelics.IndexOf(identifier);
            else if (id < 0 || id >= ___m_ChosenRelics.Count || ___m_ChosenRelics[id] != identifier) return true;
            if (id >= 0 && !Pool(shop).Contains(identifier))
                stock.RecordSupplier(id, Supplier(__instance));
            if (id >= 0 && !stock.IsSold(id) && ReferenceEquals(Available(shop, __instance, stock, id, identifier), relic))
                return true;

            __result = __instance.AddSold(-1, false);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingObjectShop), "Callback")]
        private static bool Purchased(BuildingObjectShop __instance, int __0, ResourceAmount[] __1)
        {
            if (!(__instance is BuildingAmayaShop shop) || !Stocks.TryGetValue(shop, out var stock))
                return true;
            if (stock.IsSold(__0)) return false;
            if (!stock.MarkSold(__0)) return true;
            if (__1?.Length == 1) SoldPrices.GetOrCreateValue(stock)[__0] = __1[0];

            // The card owns the transaction. This callback only records the sale and, in vanilla,
            // disables the world shop. Keep the native world target available after the sale.
            shop.SetInteractable(true, false);
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingObjectShop), "AfterDialogue")]
        private static void MerchantFocus(BuildingObjectShop __instance)
        {
            if (!(__instance is BuildingAmayaShop))
                return;

            var merchant = OverlayManager.Instance.GetMerchant();
            var view = merchant.GetComponent<AmayaShopView>() ?? merchant.gameObject.AddComponent<AmayaShopView>();
            view.Open();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIOverlayMerchant), "Reroll",
            new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
        private static void RerollFocus(UIOverlayMerchant __instance, bool doChangeInventory, bool fromSave)
        {
            if (doChangeInventory && !fromSave && Owners.TryGetValue(__instance, out _))
                __instance.GetComponent<AmayaShopView>()?.ChooseFocusAfterRefresh(true);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "Init")]
        private static void ResetView(UIOverlayMerchant __instance)
        {
            Owners.Remove(__instance);
            var view = __instance.GetComponent<AmayaShopView>();
            if (view != null) view.enabled = false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(OverlayManager), "Cancel")]
        private static bool Back(OverlayManager __instance, bool force)
        {
            // The native Back action requests a non-forced close. For Amaya it should
            // activate the same cancellation path as the visible "No, thanks!" button.
            if (force || !(AmayaShopView.CurrentOverlay(__instance) is UIOverlayMerchant merchant) ||
                merchant.GetComponent<AmayaShopView>()?.CanBackOut() != true) return true;
            merchant.Cancel();
            return false;
        }
    }
}
