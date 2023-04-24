using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CarinaStudio.Diagnostics;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Memory cache for <see cref="IStringSource"/>s.
/// </summary>
public class StringSourceCache
{
    // Entry in cache.
    class CacheEntry
    {
        public volatile string? Key;
        public volatile CacheEntry? NextUsedEntry;
        public volatile CacheEntry? PreviousUsedEntry;
        public volatile IStringSource? StringSource;
    }
    
    
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<StringSourceCache>();
    static readonly long CacheEntryByteCount = Memory.EstimateInstanceSize<CacheEntry>();
    static readonly long KeyValuePairByteCount = Memory.EstimateInstanceSize<KeyValuePair<string, CacheEntry>>();
    
    
    // Fields.
    long cachedStringSourcesByteCount;
    readonly IDictionary<string, CacheEntry> entriesByKey = new Dictionary<string, CacheEntry>();
    volatile CacheEntry? leastRecentlyUsedEntry;
    long maxByteCount = 1L << 30; // 1GB
    volatile CacheEntry? recentlyUsedEntry;
    readonly object syncLock = new();


    /// <summary>
    /// Add source source to the cache.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <param name="source">String source.</param>
    /// <returns>True if string source has been added to cache successfully.</returns>
    public bool Add(string key, IStringSource source)
    {
        lock (this.syncLock)
        {
            if (this.entriesByKey.TryGetValue(key, out var entry))
            {
                this.cachedStringSourcesByteCount += (source.ByteCount - entry.StringSource!.ByteCount);
                entry.StringSource = source;
                this.SetEntryAsRecentlyUsed(entry);
            }
            else
            {
                entry = new CacheEntry
                {
                    Key = key,
                    StringSource = source,
                };
                this.cachedStringSourcesByteCount += source.ByteCount;
                this.entriesByKey[key] = entry;
                // ReSharper disable NonAtomicCompoundOperator
                this.leastRecentlyUsedEntry ??= entry;
                // ReSharper restore NonAtomicCompoundOperator
                if (this.recentlyUsedEntry is not null)
                {
                    entry.NextUsedEntry = this.recentlyUsedEntry;
                    this.recentlyUsedEntry.PreviousUsedEntry = entry;
                }
                this.recentlyUsedEntry = entry;
            }
            this.EvictExceeded();
            return this.recentlyUsedEntry == entry;
        }
    }


    /// <summary>
    /// Clear all cached string sources from the cache.
    /// </summary>
    public void Clear()
    {
        lock (this.syncLock)
        {
            this.entriesByKey.Clear();
            this.recentlyUsedEntry = null;
            this.leastRecentlyUsedEntry = null;
            this.cachedStringSourcesByteCount = 0;
        }
    }


    /// <summary>
    /// Get size of memory occupied by the cache.
    /// </summary>
    public long ByteCount => BaseByteCount + this.entriesByKey.Count * (CacheEntryByteCount + KeyValuePairByteCount) + this.cachedStringSourcesByteCount;


    /// <summary>
    /// Get size of memory of all <see cref="IStringSource"/> instances in the cache.
    /// </summary>
    public long CachedStringSourcesByteCount => this.cachedStringSourcesByteCount;


    // Evict cache entries which are exceeded the capacity.
    void EvictExceeded()
    {
        if (this.ByteCount <= this.maxByteCount)
            return;
        var lruEntry = this.leastRecentlyUsedEntry;
        do
        {
            if (lruEntry is null)
                break;
            this.entriesByKey.Remove(lruEntry.Key!);
            this.cachedStringSourcesByteCount -= lruEntry.StringSource!.ByteCount;
            var prevLruEntry = lruEntry.PreviousUsedEntry;
            if (prevLruEntry is not null)
            {
                lruEntry.PreviousUsedEntry = null;
                prevLruEntry.NextUsedEntry = null;
                this.leastRecentlyUsedEntry = prevLruEntry;
            }
            else
            {
                this.recentlyUsedEntry = null;
                this.leastRecentlyUsedEntry = null;
            }
            lruEntry = prevLruEntry;
        } while (this.ByteCount > this.maxByteCount);
    }


    /// <summary>
    /// Get or set maximum size of memory can be used by the cache.
    /// </summary>
    public long MaxByteCount
    {
        get => this.maxByteCount;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            lock (this.syncLock)
            {
                if (value >= this.maxByteCount)
                    this.maxByteCount = value;
                else
                {
                    this.maxByteCount = value;
                    this.EvictExceeded();
                }
            }
        }
    }


    // Move entry to the head of recently used list.
    void SetEntryAsRecentlyUsed(CacheEntry entry)
    {
        if (this.recentlyUsedEntry == entry)
            return;
        var prevEntry = entry.PreviousUsedEntry;
        var nextEntry = entry.NextUsedEntry;
        if (prevEntry is not null)
        {
            entry.PreviousUsedEntry = null;
            prevEntry.NextUsedEntry = nextEntry;
        }
        if (nextEntry is not null)
        {
            entry.NextUsedEntry = null;
            nextEntry.PreviousUsedEntry = prevEntry;
        }
        if (this.leastRecentlyUsedEntry == entry)
            this.leastRecentlyUsedEntry = prevEntry;
        if (this.recentlyUsedEntry is not null)
        {
            entry.NextUsedEntry = this.recentlyUsedEntry;
            this.recentlyUsedEntry.PreviousUsedEntry = entry;
        }
        else
            entry.NextUsedEntry = null;
        entry.PreviousUsedEntry = null;
        this.recentlyUsedEntry = entry;
    }


    /// <summary>
    /// Try get string source from cache.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <param name="source">Cached string source.</param>
    /// <returns>True if cached string source found.</returns>
    public bool TryGet(string key, [NotNullWhen(true)] out IStringSource? source)
    {
        source = null;
        lock (syncLock)
        {
            if (!this.entriesByKey.TryGetValue(key, out var entry))
                return false;
            source = entry.StringSource!;
            this.SetEntryAsRecentlyUsed(entry);
        }
        return true;
    }
}