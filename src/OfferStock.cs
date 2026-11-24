using System;
using System.Collections.Generic;

namespace AmayaWithoutCooldown
{
    internal sealed class OfferStock
    {
        private const int DayTag = 0x41574301;
        private const int SeedTag = 0x41574302;
        private const int Tag = 0x41574303;
        private const int NativeFields = 5;
        private int key;
        private int rerolls;
        private string[] offers;
        private readonly HashSet<int> sold = new HashSet<int>();
        private readonly HashSet<int> supplied = new HashSet<int>();
        private string supplier = "";
        internal int View { get; private set; }

        internal bool IsCurrent(int stockKey, int currentRerolls) => key == stockKey && rerolls == currentRerolls;

        // GameManager.Save clears the replay counter after serializing building data.
        // Adopt that reset only at load; later changes still invalidate the view.
        internal void LoadedCounter(int current)
        {
            if (offers != null && current == 0 && rerolls > 0) rerolls = 0;
        }

        internal bool Matches(int slot, string id) => offers != null && slot >= 0 && slot < offers.Length
            && offers[slot] == id && !IsSold(slot);

        internal bool SelectStock(int stockKey, int currentRerolls, bool refresh = false)
        {
            View++;
            if (refresh || !IsCurrent(stockKey, currentRerolls))
            {
                key = stockKey;
                rerolls = currentRerolls;
                sold.Clear();
                supplied.Clear();
                supplier = "";
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

        internal void RecordSupplier(int slot, string provider)
        {
            if (offers == null && slot >= 0 && !string.IsNullOrEmpty(provider))
            {
                supplied.Add(slot);
                supplier = provider;
            }
        }

        internal bool SuppliedBy(int slot, string provider) => supplied.Contains(slot) && supplier == provider;
        internal bool IsSold(int slot) => sold.Contains(slot);

        internal bool MarkSold(int slot) => offers != null && slot >= 0 && slot < offers.Length && sold.Add(slot);

        internal void Write(List<int> data)
        {
            if (data.Count < NativeFields) return;
            for (int slot = 0; slot < 3; slot++)
                data[2 + slot] = IsSold(slot) ? 1 : 0;

            if (offers == null) return;
            data.Add(Tag);
            data.Add(key);
            data.Add(rerolls);
            data.Add(offers.Length);
            WriteString(data, supplier);
            for (int slot = 0; slot < offers.Length; slot++)
            {
                data.Add((IsSold(slot) ? 1 : 0) | (supplied.Contains(slot) ? 2 : 0));
                WriteString(data, offers[slot]);
            }
        }

        private static void WriteString(List<int> data, string value)
        {
            data.Add(value.Length);
            foreach (char character in value) data.Add(character);
        }

        private static bool ReadString(List<int> data, ref int offset, out string value)
        {
            value = null;
            if (offset >= data.Count) return false;
            int length = data[offset++];
            if (length < 0 || length > data.Count - offset) return false;
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                int next = data[offset++];
                if (next < char.MinValue || next > char.MaxValue) return false;
                chars[i] = (char)next;
            }
            value = new string(chars);
            return true;
        }

        internal static OfferStock Read(List<int> data, int stockKey)
        {
            var native = new OfferStock { key = stockKey };
            if (data.Count < NativeFields) return native;
            native.rerolls = data[0];
            for (int slot = 0; slot < 3; slot++)
                if (data[2 + slot] > 0) native.sold.Add(slot);

            int start = data.IndexOf(Tag, NativeFields);
            if (start < 0) start = data.IndexOf(SeedTag, NativeFields);
            if (start < 0) start = data.IndexOf(DayTag, NativeFields);
            if (start < 0 || data.Count - start < 4) return native;

            bool current = data[start] == Tag;
            int count = data[start + 3];
            int offset = start + 4;
            var restored = new OfferStock {
                key = data[start] == DayTag ? unchecked(data[start + 1] * 31) : data[start + 1],
                rerolls = data[start + 2]
            };
            if (current && !ReadString(data, ref offset, out restored.supplier)) return native;
            // Each entry has a length and at least one character, plus flags in v3.
            if (count < 0 || count > (data.Count - offset) / (current ? 3 : 2)) return native;
            restored.offers = new string[count];
            if (!current) restored.sold.UnionWith(native.sold);
            for (int slot = 0; slot < count; slot++)
            {
                if (current)
                {
                    if (offset >= data.Count) return native;
                    int flags = data[offset++];
                    if ((flags & ~3) != 0) return native;
                    if ((flags & 1) != 0) restored.sold.Add(slot);
                    if ((flags & 2) != 0) restored.supplied.Add(slot);
                }
                if (!ReadString(data, ref offset, out var id) || id.Length == 0) return native;
                restored.offers[slot] = id;
            }
            return restored;
        }
    }
}
