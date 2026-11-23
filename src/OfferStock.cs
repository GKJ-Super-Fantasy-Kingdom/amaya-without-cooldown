using System;
using System.Collections.Generic;

namespace AmayaWithoutCooldown
{
    // The game saves five integers per object shop: rerolls, closed and three sold slots.
    // The tagged extension travels with this building in the same save and rewind.
    internal sealed class OfferStock
    {
        private const int Tag = 0x41574301;
        private const int NativeFields = 5;
        private const int SlotCount = 3;
        private int day;
        private int rerolls;
        private int sold;
        private string[] offers;

        internal bool SelectStock(int currentDay, int currentRerolls)
        {
            if (day != currentDay || rerolls != currentRerolls)
            {
                day = currentDay;
                rerolls = currentRerolls;
                sold = 0;
                offers = null;
            }
            return offers != null;
        }

        internal void Remember(List<string> chosen) => offers = chosen.ToArray();

        internal void RestoreOffers(List<string> chosen)
        {
            chosen.Clear();
            chosen.AddRange(offers);
        }

        internal bool IsSold(int slot) => slot >= 0 && slot < SlotCount && (sold & (1 << slot)) != 0;

        internal bool MarkSold(int slot)
        {
            if (offers == null || slot < 0 || slot >= offers.Length || slot >= SlotCount)
                return false;

            sold |= 1 << slot;
            return true;
        }

        internal void Write(List<int> data)
        {
            if (data.Count != NativeFields)
                return;

            for (int slot = 0; slot < SlotCount; slot++)
                data[2 + slot] = IsSold(slot) ? 1 : 0;

            if (offers == null)
                return;

            data.Add(Tag);
            data.Add(day);
            data.Add(rerolls);
            data.Add(offers.Length);
            foreach (string identifier in offers)
            {
                data.Add(identifier.Length);
                foreach (char character in identifier)
                    data.Add(character);
            }
        }

        internal static OfferStock Read(List<int> data, int currentDay)
        {
            var stock = new OfferStock { day = currentDay };
            if (data.Count < NativeFields)
                return stock;

            stock.rerolls = data[0];
            for (int slot = 0; slot < SlotCount; slot++)
                if (data[2 + slot] > 0)
                    stock.sold |= 1 << slot;

            if (data.Count < 9 || data[5] != Tag)
                return stock;

            int count = data[8];
            if (count < 0 || count > SlotCount)
                return stock;

            var restored = new string[count];
            int offset = 9;
            for (int slot = 0; slot < count; slot++)
            {
                if (offset >= data.Count)
                    return stock;
                int length = data[offset++];
                if (length <= 0 || length > data.Count - offset)
                    return stock;
                var identifier = new char[length];
                for (int i = 0; i < length; i++)
                {
                    int value = data[offset++];
                    if (value < char.MinValue || value > char.MaxValue)
                        return stock;
                    identifier[i] = (char)value;
                }
                restored[slot] = new string(identifier);
            }

            stock.day = data[6];
            stock.rerolls = data[7];
            stock.offers = restored;
            return stock;
        }
    }
}
