using System;
using System.Collections.Concurrent;
using System.Threading;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// Values a preview loads once each, kept only while recent builds use them. A build calls <see cref="Begin"/> first, takes its
/// values with <see cref="Get"/> or <see cref="TryGet"/>, and calls <see cref="Trim"/> when it has them all, which drops what
/// none of the last <c>keep</c> builds used. Values a scene still holds stay alive in it; only the cache forgets them.
/// </summary>
/// <remarks>
/// Builds may overlap. A value is marked with the newest build's number, whichever build takes it, and a build only trims
/// what's older than itself, so it never drops what a newer one took.
/// </remarks>
public sealed class RecentCache<TKey, TValue>(int keep) where TKey : notnull
{
    private sealed class Entry(Lazy<TValue> value)
    {
        public readonly Lazy<TValue> Value = value;
        public long LastUsed;
    }

    private readonly ConcurrentDictionary<TKey, Entry> entries = new();
    private long builds;

    public int Count => entries.Count;

    /// <summary>Starts a build, and returns its number for <see cref="Trim"/>.</summary>
    public long Begin() => Interlocked.Increment(ref builds);

    /// <summary>The value for <paramref name="key"/>, loaded by <paramref name="load"/> the first time.</summary>
    public TValue Get(TKey key, Func<TKey, TValue> load) => Use(entries.GetOrAdd(key, k => new Entry(new(() => load(k))))).Value.Value;

    /// <summary>The value for <paramref name="key"/>, if there is one.</summary>
    public bool TryGet(TKey key, out TValue value)
    {
        var found = entries.TryGetValue(key, out var entry);
        value = found ? Use(entry!).Value.Value : default!;
        return found;
    }

    /// <summary>Keeps <paramref name="value"/> for <paramref name="key"/>, unless there's one already.</summary>
    public void Add(TKey key, TValue value) => Use(entries.GetOrAdd(key, _ => new Entry(new(value))));

    private Entry Use(Entry entry)
    {
        Interlocked.Exchange(ref entry.LastUsed, Interlocked.Read(ref builds));
        return entry;
    }

    /// <summary>Drops the values none of the last builds up to <paramref name="build"/> used.</summary>
    public void Trim(long build)
    {
        foreach (var (key, entry) in entries)
            if (Interlocked.Read(ref entry.LastUsed) <= build - keep) entries.TryRemove(new(key, entry));
    }

    public void Clear() => entries.Clear();
}
