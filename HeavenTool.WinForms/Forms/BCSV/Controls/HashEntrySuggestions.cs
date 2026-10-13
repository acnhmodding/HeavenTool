using HeavenTool.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using static HeavenTool.Forms.BCSV.Controls.HashEntry;

namespace HeavenTool.Forms.BCSV.Controls;

// Called on the UI thread: asynchronous batches keep reads of the mutable hash
// dictionaries on that thread, alongside the code that registers new labels.
internal static class HashEntrySuggestions
{
    private sealed record CacheEntry(List<uint> Source, int Count, int NameCount, Task<HashComboBoxEntry[]> Items);
    private static readonly Dictionary<(uint Column, bool Murmur), CacheEntry> cache = [];

    public static Task<HashComboBoxEntry[]> GetAsync(uint column, bool murmur)
    {
        if (!HashManager.EnumListCRC32.TryGetValue(column, out var source))
            return Task.FromResult<HashComboBoxEntry[]>([]);

        var key = (column, murmur);
        int nameCount = HashManager.CRC32_Hashes.Count;
        if (cache.TryGetValue(key, out var previous) && ReferenceEquals(source, previous.Source)
            && source.Count == previous.Count && nameCount == previous.NameCount)
            return previous.Items;

        var items = BuildAsync(source.ToArray(), murmur);
        cache[key] = new CacheEntry(source, source.Count, nameCount, items);
        return items;
    }

    private static async Task<HashComboBoxEntry[]> BuildAsync(uint[] hashes, bool murmur)
    {
        var items = new List<HashComboBoxEntry>(hashes.Length);
        for (int i = 0; i < hashes.Length; i++)
        {
            uint hash = hashes[i];
            var name = hash.GetHashTranslationOrNull();
            if (!murmur)
                items.Add(new HashComboBoxEntry(name ?? $"0x{hash:X8}", hash));
            else if (name != null)
                items.Add(new HashComboBoxEntry(name, name.ToMurmur()));

            if ((i + 1) % 256 == 0)
                await Task.Delay(1);
        }
        return [.. items];
    }
}
