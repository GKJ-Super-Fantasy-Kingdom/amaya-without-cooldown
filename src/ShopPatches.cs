using System;
using System.Collections.Generic;
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
        private static readonly ConditionalWeakTable<BuildingAmayaShop, OfferStock> Stocks =
            new ConditionalWeakTable<BuildingAmayaShop, OfferStock>();

        private static readonly AccessTools.FieldRef<BuildingObjectShop, int> RerollCount =
            AccessTools.FieldRefAccess<BuildingObjectShop, int>("m_Rerolls");

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingAmayaShop), "OnSetData")]
        private static void Load(BuildingAmayaShop __instance, ref List<int> data)
        {
            Stocks.Remove(__instance);
            Stocks.Add(__instance, OfferStock.Read(data, DaytimeManager.Instance.CurrentDay()));

            // Let vanilla restore reroll pricing and its profile accounting. Omit the
            // purchase closure/slot flags from this call only; the setup-day gate runs separately.
            if (data.Count > 1)
                data = new List<int> { data[0] };
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingObjectShop), "SaveData")]
        private static void Save(BuildingObjectShop __instance, List<int> __result)
        {
            if (__instance is BuildingAmayaShop shop && Stocks.TryGetValue(shop, out var stock))
                stock.Write(__result);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "GenerateRelics")]
        private static void BeforeCards(ref bool reroll, Action<int, ResourceAmount[]> ___m_Callback,
            List<string> ___m_ChosenRelics, out OfferStock __state)
        {
            __state = null;
            if (!(___m_Callback?.Target is BuildingAmayaShop shop))
                return;

            __state = Stocks.GetValue(shop, _ => new OfferStock());
            if (__state.SelectStock(DaytimeManager.Instance.CurrentDay(), RerollCount(shop)))
            {
                __state.RestoreOffers(___m_ChosenRelics);
                reroll = false; // Native renderer can display an existing selection.
            }
            else
            {
                reroll = true;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIOverlayMerchant), "GenerateRelics")]
        private static void AfterCards(OfferStock __state, List<string> ___m_ChosenRelics)
        {
            __state?.Remember(___m_ChosenRelics);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIOverlayMerchant), "AddRelic")]
        private static bool BindSlot(UIOverlayMerchant __instance, Relic relic, ref int id,
            Action<int, ResourceAmount[]> ___m_Callback, List<string> ___m_ChosenRelics, ref GameObject __result)
        {
            if (id != -1 || !(___m_Callback?.Target is BuildingAmayaShop shop)
                || !Stocks.TryGetValue(shop, out var stock))
                return true;

            // Generated relic buttons normally capture -1. Give the native buy callback its slot.
            id = ___m_ChosenRelics.IndexOf(relic.GetIdentifier());
            if (!stock.IsSold(id) && RelicManager.Instance.CanAddRelic(relic.GetIdentifier()))
                return true;

            __result = __instance.AddSold(-1, false);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingObjectShop), "Callback")]
        private static bool Purchased(BuildingObjectShop __instance, int __0)
        {
            if (!(__instance is BuildingAmayaShop shop) || !Stocks.TryGetValue(shop, out var stock)
                || !stock.MarkSold(__0))
                return true;

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

            RestoreFocus(OverlayManager.Instance.GetMerchant());
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIOverlayMerchant), "Reroll",
            new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
        private static void RerollFocus(UIOverlayMerchant __instance, Action<int, ResourceAmount[]> ___m_Callback)
        {
            if (___m_Callback?.Target is BuildingAmayaShop)
                RestoreFocus(__instance);
        }

        private static void RestoreFocus(UIOverlayMerchant merchant)
        {
            // Affordable purchases normally set the focus target during card construction.
            // When all offers are sold or unaffordable, provide a native action for the pad.
            var gamepad = GamepadManager.Instance;
            var target = gamepad.GetGameObjectToSelect();
            if (target != null && target.activeInHierarchy && target.transform.IsChildOf(merchant.transform)
                && (target.transform.parent == merchant.transform.Find("Panel/BuyButtons")
                    || target.GetComponent<Button>()?.IsInteractable() == true))
                return;

            foreach (string path in new[] { "Panel/Buttons/Reroll/Button", "Panel/Buttons/Cancel/Button" })
            {
                var button = merchant.transform.Find(path)?.GetComponent<Button>();
                if (button != null && button.isActiveAndEnabled && button.IsInteractable())
                {
                    gamepad.SetGameObjectToSelect(button.gameObject, true);
                    return;
                }
            }
        }
    }
}
