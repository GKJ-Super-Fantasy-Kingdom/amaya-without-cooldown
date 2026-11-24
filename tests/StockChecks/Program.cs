using System;
using System.Collections.Generic;
using System.Linq;
using AmayaWithoutCooldown;

static void Check(bool passed, string name)
{
    if (!passed) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

var chosen = new List<string> { "Cartographer", "Écho", "Third" };
var stock = new OfferStock();
Check(!stock.SelectStock(4, 2), "first visit uses native generator");
stock.Remember(chosen);
Check(stock.MarkSold(1) && stock.MarkSold(2), "multiple purchases record separate slots");
chosen.Clear();
Check(stock.SelectStock(4, 2), "same day and reroll restore the offer list");
stock.RestoreOffers(chosen);
Check(chosen.SequenceEqual(new[] { "Cartographer", "Écho", "Third" }), "sold and unsold identities keep position");
Check(!stock.IsSold(0) && stock.IsSold(1) && stock.IsSold(2), "sold slots stay sold on return");
var save = new List<int> { 2, 0, 0, 0, 0 };
stock.Write(save);
var loaded = OfferStock.Read(save, 4);
Check(loaded.SelectStock(4, 2) && loaded.IsSold(1), "continue restores the exact selection and sold mask");
loaded.RestoreOffers(chosen);
Check(chosen[1] == "Écho", "relic identifiers round-trip");
Check(!loaded.SelectStock(4, 3) && !loaded.IsSold(1), "a paid or free native reroll starts fresh stock");
Check(!stock.SelectStock(5, 2) && !stock.IsSold(2), "natural day change expires old stock");
var old = OfferStock.Read(new List<int> { 2, 1, 1, 0, 0 }, 4);
Check(!old.SelectStock(4, 2) && old.IsSold(0), "legacy native save keeps recorded sold slots");
Check(!old.MarkSold(-1) && !old.MarkSold(3), "invalid callback IDs rejected");
var shortStock = new List<string> { "Only" };
old.Remember(shortStock);
Check(!old.MarkSold(1) && !old.MarkSold(0), "short stock has no phantom purchasable slots");
save.RemoveAt(save.Count - 1);
Check(!OfferStock.Read(save, 4).SelectStock(4, 2), "truncated snapshot falls back to native generation");
var empty = new OfferStock();
empty.SelectStock(2, 0);
empty.Remember(new List<string>());
Check(empty.SelectStock(2, 0) && !empty.MarkSold(0), "empty selection can be reopened");

var epoch = new OfferStock();
epoch.SelectStock(31, 0);
epoch.Remember(new List<string> { "A", "B", "C" });
Check(epoch.Matches(1, "B") && !epoch.Matches(1, "C"), "a stale slot cannot grant a replacement identifier");
Check(epoch.MarkSold(1) && !epoch.MarkSold(1) && !epoch.Matches(1, "B"), "a sold purchase cannot be replayed");
var extended = new List<int> { 0, 0, 0, 0, 0, 109, 42 };
epoch.Write(extended);
var resumed = OfferStock.Read(extended, 31);
Check(extended[5] == 109 && extended[6] == 42 && resumed.SelectStock(31, 0) && resumed.IsSold(1),
    "other save fields survive and stock is recovered after them");
Check(!resumed.SelectStock(31, 0, true) && !resumed.IsSold(1), "a real refresh works even with unchanged reroll count");
var legacy = new List<int> { 0, 0, 1, 0, 0, 0x41574301, 3, 0, 1, 1, 'A' };
var migrated = OfferStock.Read(legacy, 93);
Check(migrated.SelectStock(93, 0) && migrated.IsSold(0), "0.1.0 day-based save migrates to the native seed");

var seedSave = new List<int> { 2, 0, 1, 0, 0, 0x41574302, 93, 2, 1, 1, 'A' };
var seedStock = OfferStock.Read(seedSave, 93);
seedStock.LoadedCounter(0);
Check(seedStock.SelectStock(93, 0) && seedStock.IsSold(0), "seed-based 0.1.0 save restores after a checkpoint counter reset");
var replay = OfferStock.Read(seedSave, 93);
replay.LoadedCounter(3);
Check(!replay.SelectStock(93, 3), "nonzero reroll replay still invalidates an older shelf");

var wide = new OfferStock();
wide.SelectStock(93, 2);
wide.Remember(Enumerable.Range(0, 40).Select(i => "offer" + i).ToList());
wide.MarkSold(0);
wide.MarkSold(32);
var wideSave = new List<int> { 2, 0, 0, 0, 0 };
wide.Write(wideSave);
var wideLoad = OfferStock.Read(wideSave, 93);
Check(wideLoad.SelectStock(93, 2) && wideLoad.IsSold(0) && wideLoad.IsSold(32)
    && wideLoad.Matches(33, "offer33") && wideSave[2] == 1 && wideSave[3] == 0,
    "expanded slots beyond an integer mask round-trip independently of native flags");
var truncated = new List<int>(wideSave);
truncated.RemoveAt(truncated.Count - 1);
var fallback = OfferStock.Read(truncated, 93);
Check(!fallback.SelectStock(93, 2) && fallback.IsSold(0) && !fallback.IsSold(32),
    "an incomplete expanded save falls back to native flags without a partial shelf");
