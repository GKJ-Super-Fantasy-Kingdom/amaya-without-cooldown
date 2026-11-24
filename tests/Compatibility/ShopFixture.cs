using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using UnityEngine.Events;

// The game assemblies execute under their bundled Mono. Replace scene rendering,
// resource balances and the final reward sink; never load a scene or an actual save.
internal static class ShopFixture
{
    private static readonly Harmony Fixture = new Harmony("fixture.amaya.scene");
    private static readonly Dictionary<string, Relic> Catalog = new Dictionary<string, Relic>();
    private static readonly List<Relic> Held = new List<Relic>();
    private static readonly List<UICardRelic> Cards = new List<UICardRelic>();
    private static readonly List<int> Quotes = new List<int>();
    private static readonly Dictionary<ResourceType, int> Wallet = new Dictionary<ResourceType, int>();
    private static Assembly plugin;
    private static BuildingAmayaShop shop;
    private static UIOverlayMerchant screen;
    private static int grants;

    private static T Bare<T>() where T : UnityEngine.Object
    {
        var obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
        Set(obj, "m_CachedPtr", new IntPtr(1)); // allow Unity's managed object lifetime check
        return obj;
    }

    private static void Set(object o, string field, object value) => AccessTools.Field(o.GetType(), field).SetValue(o, value);
    private static object Get(object o, string field) => AccessTools.Field(o.GetType(), field).GetValue(o);
    private static object Call(object o, string method, params object[] args) =>
        AccessTools.Method(o.GetType(), method).Invoke(o, args);
    private static void Check(bool ok, string text)
    {
        if (!ok) throw new InvalidOperationException(text);
        Console.WriteLine("PASS " + text);
    }

    private static void Patch(Type target, string method, string prefix, params Type[] args)
    {
        var m = AccessTools.Method(target, method, args.Length == 0 ? null : args);
        Fixture.Patch(m, prefix: new HarmonyMethod(typeof(ShopFixture), prefix) { priority = Priority.Last });
    }

    private static bool Skip() => false;
    private static bool PriceCovered(ResourceAmount[] __0, ref bool __result)
    {
        __result = __0.All(c => Wallet[c.type] >= c.amount);
        return false;
    }
    private static bool Debit(ResourceType __0, int __1, ref bool __result)
    {
        __result = Wallet[__0] >= __1;
        if (__result) Wallet[__0] -= __1;
        return false;
    }

    private static bool Sold()
    {
        Cards.Add(null);
        Quotes.Add(-1);
        return false;
    }

    private static bool Draw(Relic relic, ResourceAmount[] costs, int id)
    {
        var card = Bare<UICardRelic>();
        string key = relic.GetIdentifier();
        Set(card, "m_CanBeClicked", true);
        Set(card, "m_RelicIdentifier", key);
        // Same dispatch order as UICardRelic.Init and UIOverlayMerchant.AddBuyButton.
        var callback = (Action<int, ResourceAmount[]>)Get(screen, "m_Callback");
        var actions = new List<UnityAction>
        {
            () => { Held.Add(Catalog[key]); grants++; },
            () => callback(id, costs),
            () => ResourceManager.Instance.SpendResource(costs[0])
        };
        Set(card, "m_Callbacks", actions);
        var stocks = plugin.GetType("AmayaWithoutCooldown.ShopPatches").GetField("Stocks",
            BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        object[] lookup = { shop, null };
        stocks.GetType().GetMethod("TryGetValue").Invoke(stocks, lookup);
        AccessTools.Method(plugin.GetType("AmayaWithoutCooldown.PurchaseGuard"), "Track")
            .Invoke(null, new object[] { card, shop, screen, lookup[1], id, relic, costs, (ResourceAmount[])costs.Clone() });
        Cards.Add(card);
        Quotes.Add(costs[0].amount);
        return false;
    }

    private static void Initialize()
    {
        GameManager.Instance = Bare<GameManager>();
        DaytimeManager.Instance = Bare<DaytimeManager>();
        Set(DaytimeManager.Instance, "m_CurrentDay", 8);
        RaceManager.Instance = Bare<RaceManager>();
        RaceDataManager.Instance = Bare<RaceDataManager>();
        Helper.Instance = Bare<Helper>();
        SettingsManager.Instance = Bare<SettingsManager>();
        DialogueManager.Instance = Bare<DialogueManager>();
        ResourceManager.Instance = Bare<ResourceManager>();
        UnlockedUnitsManager.Instance = Bare<UnlockedUnitsManager>();
        AddressablesManager.Instance = Bare<AddressablesManager>();
        Set(AddressablesManager.Instance, "m_Relics", Catalog);
        RelicManager.Instance = Bare<RelicManager>();
        Set(RelicManager.Instance, "m_Relics", Held);
        Wallet[ResourceType.Coins] = Wallet[ResourceType.Faith] = 1000;
        Patch(typeof(DialogueManager), "GetActor", "Skip");
        Patch(typeof(UIOverlayMerchant), "SetActor", "Skip");
        Patch(typeof(UIOverlayMerchant), "AddRelic", "Draw");
        Patch(typeof(UIOverlayMerchant), "AddSold", "Sold");
        Patch(typeof(UIOverlayMerchant), "Cancel", "Skip");
        Patch(typeof(ResourceManager), "CanBeBuilt", "PriceCovered", typeof(ResourceAmount[]));
        Patch(typeof(ResourceManager), "SpendResource", "Debit", typeof(ResourceType), typeof(int));
    }

    private static Relic Define(string id, int cost, bool boss = false)
    {
        var relic = Bare<Relic>();
        Set(relic, "identifier", id);
        Set(relic, "cost", cost);
        Set(relic, "kingdom", Races.None);
        Set(relic, "requiredBuilding", BuildingType.None);
        Set(relic, "soldAtCartographer1", !boss);
        Catalog[id] = relic;
        return relic;
    }

    private static void NewShop(List<int> saved)
    {
        shop = Bare<BuildingAmayaShop>();
        Set(shop, "m_IsActive", true);
        Set(shop, "m_Options", new List<string>());
        Set(shop, "m_Random", new Random(10));
        Call(shop, "LoadOptions");
        shop.SetData(saved, true);
        screen = Bare<UIOverlayMerchant>();
        Set(screen, "m_ChosenRelics", new List<string>());
        var callback = AccessTools.Method(typeof(BuildingObjectShop), "Callback");
        screen.SetCallback((Action<int, ResourceAmount[]>)Delegate.CreateDelegate(
            typeof(Action<int, ResourceAmount[]>), shop, callback));
    }

    private static List<string> Pool => (List<string>)Get(shop, "m_Options");
    private static void Visit()
    {
        Cards.Clear();
        Quotes.Clear();
        Call(shop, "Merchant", screen);
    }
    private static void Buy(UICardRelic card) => card.Callback();

    private static void External(Assembly pack, bool bakery)
    {
        // Use the shipped definitions and optional availability predicate. The scene-free
        // fixture mirrors Patch_Register's field assignments instead of cloning a Unity prefab.
        var entries = (IEnumerable)AccessTools.Field(pack.GetType("MoreRelics.Registry"), "Entries").GetValue(null);
        foreach (var entry in entries)
        {
            if (!(bool)entry.GetType().GetProperty("Available").GetValue(entry)) continue;
            var def = entry.GetType().GetProperty("Def").GetValue(entry);
            var id = (string)Get(def, "id");
            var r = Define(id, (int)Get(def, "cost"), (bool)Get(def, "bossOnly"));
            Set(r, "requiredKingdomLevel", Get(def, "requiredKingdomLevel"));
        }
        NewShop(new List<int>());
        Check(!Pool.Contains("ownly_Phalanx") && Catalog.ContainsKey("ownly_Phalanx"), "Phalanx excluded by real shop filter");
        Check(Catalog.ContainsKey("ownly_Millstone") == !bakery, "Millstone registration follows BakeryUsesWheat presence");
        var gated = Define("fixture-gated", 7);
        Set(gated, "requiredKingdomLevel", 98);
        Check(!(bool)Call(shop, "CanAddRelic", gated), "level gate before Master Key");
        Held.Add(Catalog["ownly_MasterKey"]);
        Check((bool)Call(shop, "CanAddRelic", gated), "real MoreRelics Master Key patch lifts the level gate");
        Held.Clear();
        Catalog.Remove("fixture-gated");
    }

    private static void Exercise(string id, int basePrice)
    {
        Held.Clear();
        NewShop(new List<int>());
        Check(Pool.Contains(id), id + " eligible in the game-built Amaya pool");
        string first = "fixture-first", last = "fixture-last";
        Pool.Clear();
        Pool.AddRange(new[] { first, id, last });
        Visit();
        var ids = ((List<string>)Get(screen, "m_ChosenRelics")).ToArray();
        int slot = Array.IndexOf(ids, id);
        Check(slot >= 0 && Quotes[slot] == basePrice + 2, id + " native day-8 quote");
        var card = Cards[slot];
        int coins = Wallet[ResourceType.Coins], rewards = grants;
        Buy(card);
        Buy(card);
        Check(grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - basePrice - 2, id + " charged and granted once");
        Visit();
        Check(Cards[slot] == null && Cards.Count == 3, id + " sold on same-day return");
        var saved = shop.SaveData();
        NewShop(saved);
        Pool.Reverse();
        Visit();
        Check(Cards[slot] == null && ids.SequenceEqual((List<string>)Get(screen,"m_ChosenRelics")), id + " sold after reordered Continue");
        string missing = ids[(slot + 1) % 3];
        var removed = Catalog[missing];
        Catalog.Remove(missing);
        NewShop(saved);
        Visit();
        Check(Cards.Count == 3 && Cards[(slot + 1) % 3] == null && Cards[slot] == null, "missing unsold slot keeps position without pricing a null relic");
        Catalog[missing] = removed;
        NewShop(saved);
        Visit();
        int otherSlot = Array.IndexOf(ids, missing);
        var stale = Cards[otherSlot];
        Define(missing, 17);
        Buy(stale);
        Check(grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - basePrice - 2, "changed prefab cannot grant or charge from stale card");
        Visit();
        Check(Quotes[otherSlot] == 19, "reopen uses the live replacement price");
        stale = Cards[otherSlot];
        Pool.Remove(missing);
        Buy(stale);
        Check(grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - basePrice - 2, "pool removal after display prevents reward and debit");
        Pool.Add(missing);
        Catalog[missing] = removed;
        Catalog[missing] = Catalog[id];
        Visit();
        Check(Cards[otherSlot] == null, "registry alias cannot sell a different identifier from the saved slot");
        Catalog[missing] = removed;
        Visit();
        var nowOwned = Cards[otherSlot];
        Held.Add(removed);
        Buy(nowOwned);
        Check(grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - basePrice - 2,
            "newly owned and ineligible relic cannot grant or charge");
        Held.Remove(removed);
        Visit();
        var expired = Cards[otherSlot];
        Visit();
        Buy(expired);
        Check(grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - basePrice - 2,
            "a callback from a previous opening cannot spend again");
        int faith = Wallet[ResourceType.Faith];
        Set(screen,"m_Rerolls",1);
        int price = (int)Call(screen,"GetRerollCost");
        UnlockedUnitsManager.Instance.SpendReroll(1);
        Check(price == 2 && Wallet[ResourceType.Faith] == faith - price, "native paid reroll quote and spend");
        Set(UnlockedUnitsManager.Instance,"m_FreeDailyRerollsLeft",1);
        Check((int)Call(screen,"GetRerollCost")==0, "earned reroll has zero price");
        UnlockedUnitsManager.Instance.SpendReroll(1);
        Check(UnlockedUnitsManager.Instance.GetFreeRerollsLeft()==0 && Wallet[ResourceType.Faith]==faith-price, "free allowance consumed once without faith debit");
        Cards.Clear();
        Quotes.Clear();
        Call(screen, "GenerateRelics", true);
        Check(!Cards.Contains(null), "explicit refresh generates eligible stock even at unchanged reroll count");
    }


    private static void CheckpointShelf()
    {
        Held.Clear();
        UnityEngine.PlayerPrefs.SetInt(Preference, 0);
        NewShop(new List<int>());
        Call(shop, "BeforeReroll", screen);
        Visit();
        var ids = ((List<string>)Get(screen, "m_ChosenRelics")).ToArray();
        Buy(Cards[0]);
        var data = shop.SaveData();
        Held.Clear(); // Make a lost sold flag observable independently of ownership.
        UnityEngine.PlayerPrefs.SetInt(Preference, 0); // GameManager.Save clears this after writing building data.
        NewShop(data);
        Visit();
        Check(Cards[0] == null && ids.SequenceEqual((List<string>)Get(screen, "m_ChosenRelics")),
            "same-day checkpoint reset preserves the purchased slot and exact rerolled shelf");
        Call(shop, "BeforeReroll", screen);
        Visit();
        Check(!Cards.Contains(null), "a real reroll after checkpoint restoration clears the sold slot");
        UnityEngine.PlayerPrefs.SetInt(Preference, 0);
        NewShop(data);
        Set(DaytimeManager.Instance, "m_CurrentDay", 9);
        Visit();
        Check(!Cards.Contains(null), "next-day checkpoint still refreshes");
        Set(DaytimeManager.Instance, "m_CurrentDay", 8);
    }

    private static IEnumerable<CodeInstruction> FourSlots(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_3) instruction.opcode = OpCodes.Ldc_I4_4;
            yield return instruction;
        }
    }

    private static Relic SupplyDirect() => Catalog["fixture-direct"];
    private static void UseSupplier(ref Func<Relic> __0) => __0 = SupplyDirect;
    private static bool SupplyPatched(ref Relic __result)
    {
        __result = SupplyDirect();
        return false;
    }

    private static void ProviderShelves()
    {
        Held.Clear();
        Define("fixture-fourth", 4);
        Define("fixture-direct", 6);
        NewShop(new List<int>());
        Pool.Remove("fixture-direct");
        var supplier = new Harmony("fixture.amaya.supplier");
        supplier.Patch(AccessTools.Method(typeof(UIOverlayMerchant), "Relics"),
            prefix: new HarmonyMethod(typeof(ShopFixture), "UseSupplier"));
        Visit();
        Check(Cards.Count == 1 && Cards[0] != null && Quotes[0] == 8,
            "generator can supply an eligible registered offer outside the cached pool");
        var before = shop.SaveData();
        var stale = Cards[0];
        Catalog.Remove("fixture-direct");
        int coins = Wallet[ResourceType.Coins], rewards = grants;
        Buy(stale);
        Visit();
        Check(Cards[0] == null && Wallet[ResourceType.Coins] == coins && grants == rewards,
            "removed directly supplied offer cannot grant or charge");
        Define("fixture-direct", 6);
        NewShop(before);
        Pool.Remove("fixture-direct");
        Visit();
        var fresh = Cards[0];
        Buy(fresh);
        Buy(fresh);
        Visit();
        Check(Cards[0] == null && grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - 8,
            "direct offer survives Continue, charges once and stays sold");
        supplier.UnpatchSelf();
        Held.Clear();
        NewShop(before);
        Pool.Remove("fixture-direct");
        Visit();
        Check(Cards[0] == null, "withdrawn generator cannot sell its cached out-of-pool relic");

        var hooked = new Harmony("fixture.amaya.generator-hook");
        hooked.Patch(AccessTools.Method(typeof(BuildingRelicShop), "GetRelic"),
            prefix: new HarmonyMethod(typeof(ShopFixture), "SupplyPatched") { priority = Priority.First });
        NewShop(new List<int>());
        Pool.Remove("fixture-direct");
        Visit();
        Check(Cards.Count == 1 && Cards[0] != null, "patched native generator can supply a registered out-of-pool offer");
        var indirect = Cards[0];
        coins = Wallet[ResourceType.Coins];
        rewards = grants;
        hooked.UnpatchSelf();
        Buy(indirect);
        Visit();
        Check(Cards[0] == null && grants == rewards && Wallet[ResourceType.Coins] == coins,
            "withdrawing the native generator patch rejects its old card and hides its slot");

        var wider = new Harmony("fixture.amaya.four-slots");
        wider.Patch(AccessTools.Method(typeof(UIOverlayMerchant), "GenerateRelics"),
            transpiler: new HarmonyMethod(typeof(ShopFixture), "FourSlots"));
        NewShop(new List<int>());
        var original = (Action<int, ResourceAmount[]>)Get(screen, "m_Callback");
        int calls = 0, purchasedSlot = -1;
        screen.SetCallback((index, costs) => { calls++; purchasedSlot = index; original(index, costs); });
        Visit();
        var ids = ((List<string>)Get(screen, "m_ChosenRelics")).ToArray();
        Check(ids.Length == 4 && Cards.Count == 4, "expanded provider draws four native offers");
        var fourth = Cards[3];
        int quote = Quotes[3];
        coins = Wallet[ResourceType.Coins];
        rewards = grants;
        Buy(fourth);
        Buy(fourth);
        Visit();
        Check(calls == 1 && purchasedSlot == 3 && grants == rewards + 1 && Wallet[ResourceType.Coins] == coins - quote && Cards[3] == null,
            "wrapped callback buys slot four once and same-day return preserves sale");
        var saved = shop.SaveData();
        Held.Clear();
        NewShop(saved);
        Pool.Reverse();
        Visit();
        Check(Cards.Count == 4 && Cards[3] == null && ids.SequenceEqual((List<string>)Get(screen, "m_ChosenRelics")),
            "expanded shelf and fourth sold flag survive reordered Continue");
        wider.UnpatchSelf();
    }

    private static string Preference => "RerollAmaya" + Helper.Instance.GetRaceString(RaceManager.Instance.GetRace(), true)
        + SettingsManager.Instance.GetProfile();

    private static void CheckpointAccounting(Harmony own)
    {
        // Use native callbacks with an in-memory preference store. Compare the game's
        // dawn replay ledger both with our patch set and after it is removed.
        UnityEngine.PlayerPrefs.SetInt(Preference, 0);
        Held.Clear();
        NewShop(new List<int>());
        Set(UnlockedUnitsManager.Instance, "m_FreeDailyRerollsLeft", 1);
        int start = Wallet[ResourceType.Faith];
        UnlockedUnitsManager.Instance.SpendReroll(0);
        Call(shop, "BeforeReroll", screen);
        Check(Wallet[ResourceType.Faith] == start && UnityEngine.PlayerPrefs.GetInt(Preference, 0) == 1,
            "native accepted free reroll records the new stock index without immediate faith debit");
        var data = shop.SaveData();
        NewShop(data);
        int modReplay = start - Wallet[ResourceType.Faith];
        own.UnpatchSelf();
        Wallet[ResourceType.Faith] = start;
        NewShop(data);
        Check(modReplay == 1 && start - Wallet[ResourceType.Faith] == modReplay,
            "installed game replays one stored reroll as one faith on load with or without Amaya patches");
    }

    private static int schedule = 101;
    private static int surcharge;
    private static string blockedId;
    private static void ScheduledSeed(ref int __result) => __result = schedule;
    private static void Eligibility(Relic __0, ref bool __result) => __result &= __0.GetIdentifier() != blockedId;
    private static void LiveQuote(ref ResourceAmount[] __result) => __result[0].amount += surcharge;
    private static void RerollQuote(ref int __result) { if (__result > 0) __result += 3; }
    private static void RerollSpend(ref int __0)
    {
        if (UnlockedUnitsManager.Instance.GetFreeRerollsLeft() == 0) __0 += 3;
    }

    private static void ModifiedRules()
    {
        var pricing = new Harmony("fixture.amaya.modified-rules");
        Action<Type, string, string> postfix = (t, name, hook) =>
            pricing.Patch(AccessTools.Method(t, name), postfix: new HarmonyMethod(typeof(ShopFixture), hook));
        postfix(typeof(BuildingAmayaShop), "GetPrice", "LiveQuote");
        postfix(typeof(BuildingAmayaShop), "GetRandomSeedModifier", "ScheduledSeed");
        postfix(typeof(BuildingAmayaShop), "CanAddRelic", "Eligibility");
        postfix(typeof(UIOverlayMerchant), "GetRerollCost", "RerollQuote");
        pricing.Patch(AccessTools.Method(typeof(UnlockedUnitsManager), "SpendReroll"),
            prefix: new HarmonyMethod(typeof(ShopFixture), "RerollSpend"));
        Held.Clear();
        NewShop(new List<int>());
        Pool.Clear();
        Pool.AddRange(new[] { "fixture-first", "fixture-last" });
        Visit();
        var ids = ((List<string>)Get(screen, "m_ChosenRelics")).ToArray();
        int slot = Array.IndexOf(ids, "fixture-first");
        int initial = Wallet[ResourceType.Coins];
        int rewardCount = grants;
        Wallet[ResourceType.Coins] = 0;
        // A rejection must return before Select touches FMOD or the Unity animation.
        AccessTools.Method(typeof(UIOverlayMerchant), "Select", new[] { typeof(UICard) })
            .Invoke(screen, new object[] { Cards[slot] });
        Buy(Cards[slot]);
        Check(grants == rewardCount && Wallet[ResourceType.Coins] == 0,
            "a balance change blocks both selection and the delayed purchase callback");
        Wallet[ResourceType.Coins] = initial;
        surcharge = 4;
        Buy(Cards[slot]);
        Check(grants == rewardCount && Wallet[ResourceType.Coins] == initial, "changed live price rejects old quote before payment");
        Visit();
        Check(Quotes[slot] == 9, "modified purchase price reaches the reopened card");
        blockedId = "fixture-first";
        Buy(Cards[slot]);
        Check(grants == rewardCount && Wallet[ResourceType.Coins] == initial, "eligibility revoked after draw cancels the whole transaction");
        Visit();
        Check(Cards[slot] == null, "shop-specific eligibility is checked when stock is restored");
        blockedId = null;
        Visit();
        Buy(Cards[slot]);
        Check(grants == rewardCount + 1 && Wallet[ResourceType.Coins] == initial - 9, "active modified purchase price is charged once");
        Set(DaytimeManager.Instance, "m_CurrentDay", 9);
        Visit();
        Check(Cards[slot] == null && ids.SequenceEqual((List<string>)Get(screen,"m_ChosenRelics")),
            "a longer supplier period preserves sales across a day change");
        Held.Clear(); // make the relic eligible again so an incorrect sold mask is observable
        schedule++;
        Visit();
        Check(Cards.Count == 2 && !Cards.Contains(null), "intra-day supplier restock starts a fresh selection");
        int faith = Wallet[ResourceType.Faith];
        Set(screen, "m_Rerolls", 1);
        int paid = (int)Call(screen, "GetRerollCost");
        UnlockedUnitsManager.Instance.SpendReroll(1);
        Check(paid == 5 && Wallet[ResourceType.Faith] == faith - paid, "external price/spend hooks own the paid reroll");
        Set(UnlockedUnitsManager.Instance, "m_FreeDailyRerollsLeft", 1);
        Check((int)Call(screen, "GetRerollCost") == 0, "free reroll takes priority over paid surcharge");
        UnlockedUnitsManager.Instance.SpendReroll(1);
        Check(Wallet[ResourceType.Faith] == faith - paid && UnlockedUnitsManager.Instance.GetFreeRerollsLeft() == 0,
            "modified reroll rules still consume a single earned allowance");
        Pool.Clear();
        Cards.Clear();
        Call(screen, "GenerateRelics", true);
        Check(Cards.Count == 0, "empty generation pool produces no selectable cards");
        pricing.UnpatchSelf();
    }

    private static void BakeryTransition(Assembly more, string bakeryPath)
    {
        NewShop(new List<int>());
        Pool.Clear();
        Pool.AddRange(new[] { "ownly_Cornucopia", "ownly_Millstone", "ownly_MasterKey" });
        Visit();
        var ids = ((List<string>)Get(screen, "m_ChosenRelics")).ToArray();
        int master = Array.IndexOf(ids, "ownly_MasterKey");
        int mill = Array.IndexOf(ids, "ownly_Millstone");
        int horn = Array.IndexOf(ids, "ownly_Cornucopia");
        Buy(Cards[master]);
        var save = shop.SaveData();
        int coins = Wallet[ResourceType.Coins], rewardCount = grants;
        new Harmony("ownly.bakeryuseswheat").PatchAll(Assembly.LoadFrom(bakeryPath));
        Chainloader.PluginInfos.Add("ownly.bakeryuseswheat", null);
        foreach (string key in Catalog.Keys.Where(k => k.StartsWith("ownly_")).ToArray()) Catalog.Remove(key);
        Held.Clear();
        External(more, true);
        Held.Add(Catalog["ownly_MasterKey"]);
        NewShop(save);
        Pool.Reverse();
        Visit();
        Check(Cards[mill] == null && Cards[master] == null && Quotes[horn] == 10 &&
            ids.SequenceEqual((List<string>)Get(screen, "m_ChosenRelics")),
            "Continue with BakeryUsesWheat hides removed Millstone, preserves sold Master Key and quotes Cornucopia");
        Check(Wallet[ResourceType.Coins] == coins && grants == rewardCount, "changed-pack Continue spends and grants nothing");
    }

    public static int Main(string[] args)
    {
        try
        {
            string config = System.IO.Path.GetFullPath("lane-" + args.Length + "/BepInEx.cfg");
            AccessTools.PropertySetter(typeof(BepInEx.Paths), "BepInExConfigPath").Invoke(null, new object[] { config });
            plugin = Assembly.LoadFrom(args[0]);
            var own = new Harmony("fixture.amaya.mod");
            own.PatchAll(plugin);
            Initialize();
            Define("fixture-first",3);
            Define("fixture-last",5);
            if (args.Length > 1)
            {
                Assembly more = Assembly.LoadFrom(args[1]);
                new Harmony("ownly.morerelics").PatchAll(more);
                bool bakery = args.Length > 2;
                External(more, false);
                if (bakery) BakeryTransition(more, args[2]);
                Exercise("ownly_Cornucopia",8);
                Exercise("ownly_SousChef",8);
                Exercise("ownly_MasterKey",6);
                if (!bakery) Exercise("ownly_Millstone",10);
            }
            else
            {
                Define("fixture-offer",8);
                Exercise("fixture-offer",8);
            }
            var unrelated = Bare<UICardRelic>();
            bool delivered = false;
            Set(unrelated, "m_Callbacks", new List<UnityAction> { () => delivered = true });
            unrelated.Callback();
            Check(delivered && Bare<BuildingSiegeShop>().SaveData().SequenceEqual(new int[5]),
                "unrelated cards and shop saves keep their normal behavior");
            CheckpointShelf();
            ProviderShelves();
            ModifiedRules();
            CheckpointAccounting(own);
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
