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
Check(!old.MarkSold(1) && old.MarkSold(0), "short stock has no phantom purchasable slots");
save.RemoveAt(save.Count - 1);
Check(!OfferStock.Read(save, 4).SelectStock(4, 2), "truncated snapshot falls back to native generation");
var empty = new OfferStock();
empty.SelectStock(2, 0);
empty.Remember(new List<string>());
Check(empty.SelectStock(2, 0) && !empty.MarkSold(0), "empty selection can be reopened");
