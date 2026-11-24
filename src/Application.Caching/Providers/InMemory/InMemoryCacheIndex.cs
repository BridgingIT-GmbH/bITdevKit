// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Globalization;

// Hash shards keep unrelated mutations concurrent. Queries copy only one bounded candidate slice
// while holding a shard lock; no lock or live native enumerator crosses an await.

/// <summary>Maintains lazy hash-sharded metadata indexes and returns copied bounded slices without holding locks across awaits.</summary>
/// <param name="metadataBudget">The finite metadata bookkeeping budget.</param>
/// <param name="residentBudget">The parent resident capacity budget.</param>
/// <example><code>var page = index.Find(selection, 0, null, null, 128);</code></example>
public sealed class InMemoryCacheIndex(ResourceBudget metadataBudget, ResourceBudget residentBudget) : IDisposable
{
    private const int ShardCount = 256;
    private static readonly string MaximumKey = new('\uffff', 4097);
    private static readonly IComparer<KeyEntry> KeyComparer = Comparer<KeyEntry>.Create((x, y) => StringComparer.Ordinal.Compare(x.Key, y.Key));
    private static readonly IComparer<NumericEntry> NumericComparer = Comparer<NumericEntry>.Create((x, y) =>
        x.Value == y.Value ? StringComparer.Ordinal.Compare(x.Key, y.Key) : x.Value.CompareTo(y.Value));
    private readonly Shard[] shards = new Shard[ShardCount];

    /// <summary>Selects one of the 256 fixed shards using the canonical key's ordinal hash.</summary>
    public static int ShardFor(string key) => StringComparer.Ordinal.GetHashCode(key) & (ShardCount - 1);

    /// <summary>Indexes one immutable entry by key, publication time, size, expiry and declared dimensions.</summary>
    public void Add(InMemoryCacheEntry entry)
    {
        var shard = this.GetOrCreate(ShardFor(entry.Metadata.Identity.Key));
        lock (shard.Gate)
        {
            var metadata = entry.Metadata;
            var key = new KeyEntry(entry.SortKey, entry);
            shard.Keys.Add(key);
            shard.Published.Add(new(metadata.PublishedAt.UtcTicks, entry.SortKey, entry));
            shard.Sizes.Add(new(metadata.StoredSizeBytes, entry.SortKey, entry));
            shard.Expiry.Add(new(metadata.ExpiresAt.UtcTicks, entry.SortKey, entry));
            shard.AbsoluteExpiry.Add(new(metadata.AbsoluteExpiresAt.UtcTicks, entry.SortKey, entry));
            foreach (var dimension in metadata.Dimensions)
            {
                Bucket(shard.Presence, dimension.Name).Add(key);
                if (dimension.Form == CacheDimensionForm.Interval)
                {
                    if (!shard.Intervals.TryGetValue(dimension.Name, out var intervals))
                    {
                        shard.Intervals.Add(dimension.Name, intervals = new());
                    }

                    intervals.Add(Number(dimension.Values[0]), Number(dimension.Values[1]), entry);
                }
                else
                {
                    foreach (var value in dimension.Values)
                    {
                        Bucket(shard.Values, new ValueKey(dimension.Name, value.Kind, value.CanonicalValue)).Add(key);
                    }
                }
            }
        }
    }

    /// <summary>Removes an exact generation from its shard and releases empty dimension buckets.</summary>
    public void Remove(InMemoryCacheEntry entry)
    {
        var shard = this.shards[ShardFor(entry.Metadata.Identity.Key)];
        if (shard is null)
        {
            return;
        }

        lock (shard.Gate)
        {
            var metadata = entry.Metadata;
            var key = new KeyEntry(entry.SortKey, entry);
            shard.Keys.Remove(key);
            shard.Published.Remove(new(metadata.PublishedAt.UtcTicks, entry.SortKey, entry));
            shard.Sizes.Remove(new(metadata.StoredSizeBytes, entry.SortKey, entry));
            shard.Expiry.Remove(new(metadata.ExpiresAt.UtcTicks, entry.SortKey, entry));
            shard.AbsoluteExpiry.Remove(new(metadata.AbsoluteExpiresAt.UtcTicks, entry.SortKey, entry));
            foreach (var dimension in metadata.Dimensions)
            {
                RemoveBucket(shard.Presence, dimension.Name, key);
                if (dimension.Form == CacheDimensionForm.Interval)
                {
                    if (shard.Intervals.TryGetValue(dimension.Name, out var intervals))
                    {
                        intervals.Remove(Number(dimension.Values[0]), entry);
                        if (intervals.Count == 0)
                        {
                            shard.Intervals.Remove(dimension.Name);
                        }
                    }
                }
                else
                {
                    foreach (var value in dimension.Values)
                    {
                        RemoveBucket(shard.Values, new ValueKey(dimension.Name, value.Kind, value.CanonicalValue), key);
                    }
                }
            }
        }
    }

    /// <summary>Replaces the indexed expiry after authoritative sliding renewal of this same generation.</summary>
    public void UpdateExpiry(InMemoryCacheEntry entry, DateTimeOffset previous, DateTimeOffset current)
    {
        var shard = this.shards[ShardFor(entry.Metadata.Identity.Key)];
        lock (shard.Gate)
        {
            shard.Expiry.Remove(new(previous.UtcTicks, entry.SortKey, entry));
            shard.Expiry.Add(new(current.UtcTicks, entry.SortKey, entry));
        }
    }

    /// <summary>Copies at most the requested candidate count and one lookahead using stable ordinal keyset progress.</summary>
    public InMemoryCacheIndexSlice Find(CacheSelection selection, int shardNumber, long? afterOrder, string afterKey, int maximum)
    {
        if (shardNumber is < 0 or >= ShardCount || maximum is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(shardNumber));
        }

        var shard = Volatile.Read(ref this.shards[shardNumber]);
        if (shard is null || selection.IsEmpty)
        {
            return new([], false);
        }

        lock (shard.Gate)
        {
            var candidates = new List<InMemoryCacheIndexCandidate>(maximum + 1);
            var plan = Plan(selection);
            if (plan.Kind == CacheSelectionKind.Overlap)
            {
                if (shard.Intervals.TryGetValue(plan.Name, out var intervals))
                {
                    intervals.Find(Number(plan.Values[0]), Number(plan.Values[1]), afterOrder, afterKey, candidates, maximum + 1);
                }
            }
            else if (plan.Kind is CacheSelectionKind.PublishedRange or CacheSelectionKind.StoredSizeRange)
            {
                AddNumeric(plan.Kind == CacheSelectionKind.PublishedRange ? shard.Published : shard.Sizes,
                    Number(plan.Values[0]), Number(plan.Values[1]), afterOrder, afterKey, candidates, maximum + 1);
            }
            else
            {
                var buckets = CandidateBuckets(shard, plan, afterKey);
                AddKeys(buckets, afterKey, candidates, maximum + 1);
            }

            var more = candidates.Count > maximum;
            if (more)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }

            return new(candidates.AsReadOnly(), more);
        }
    }

    /// <summary>Copies a bounded expiry-ordered slice before the supplied UTC-tick boundary.</summary>
    public InMemoryCacheIndexSlice FindExpiring(int shardNumber, long upperTicks, long? afterOrder, string afterKey, int maximum)
    {
        var shard = Volatile.Read(ref this.shards[shardNumber]);
        if (shard is null)
        {
            return new([], false);
        }

        lock (shard.Gate)
        {
            var candidates = new List<InMemoryCacheIndexCandidate>(maximum + 1);
            AddNumeric(shard.Expiry, 0, upperTicks, afterOrder, afterKey, candidates, maximum + 1);
            var more = candidates.Count > maximum;
            if (more)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }

            return new(candidates.AsReadOnly(), more);
        }
    }

    /// <summary>Copies a bounded absolute-expiry slice within the requested refresh horizon.</summary>
    public InMemoryCacheIndexSlice FindRefresh(int shardNumber, long lowerTicks, long upperTicks, long? afterOrder, string afterKey, int maximum)
    {
        var shard = Volatile.Read(ref this.shards[shardNumber]);
        if (shard is null)
        {
            return new([], false);
        }

        lock (shard.Gate)
        {
            var candidates = new List<InMemoryCacheIndexCandidate>(maximum + 1);
            AddNumeric(shard.AbsoluteExpiry, lowerTicks, upperTicks, afterOrder, afterKey, candidates, maximum + 1);
            var more = candidates.Count > maximum;
            if (more)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }

            return new(candidates.AsReadOnly(), more);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        for (var i = 0; i < this.shards.Length; i++)
        {
            var shard = Interlocked.Exchange(ref this.shards[i], null);
            if (shard is not null)
            {
                lock (shard.Gate)
                {
                    shard.Reservation.Dispose();
                }
            }
        }
    }

    private Shard GetOrCreate(int number)
    {
        var current = Volatile.Read(ref this.shards[number]);
        if (current is not null)
        {
            return current;
        }

        if (!ResourceCharge.TryReserve(4096, out var reservation, metadataBudget, residentBudget))
        {
            throw new CacheCapacityException("Native index shard metadata capacity was exhausted.");
        }

        var created = new Shard(reservation);
        current = Interlocked.CompareExchange(ref this.shards[number], created, null);
        if (current is not null)
        {
            reservation.Dispose();
        }

        return current ?? created;
    }

    private static CacheSelection Plan(CacheSelection selection)
    {
        if (selection.Kind != CacheSelectionKind.And)
        {
            return selection;
        }

        var leaves = Flatten(selection).ToArray();
        return leaves.FirstOrDefault(s => s.Kind is CacheSelectionKind.Key or CacheSelectionKind.Equal or CacheSelectionKind.Contains)
            ?? leaves.FirstOrDefault(s => s.Kind is CacheSelectionKind.ContainsAny or CacheSelectionKind.Overlap)
            ?? leaves.FirstOrDefault(s => s.Kind is CacheSelectionKind.Prefix or CacheSelectionKind.PublishedRange or CacheSelectionKind.StoredSizeRange)
            ?? selection;
    }

    private static IEnumerable<CacheSelection> Flatten(CacheSelection selection)
    {
        if (selection.Kind == CacheSelectionKind.And)
        {
            foreach (var child in selection.Children)
            {
                foreach (var leaf in Flatten(child))
                {
                    yield return leaf;
                }
            }
        }
        else
        {
            yield return selection;
        }
    }

    private static IEnumerable<IEnumerable<KeyEntry>> CandidateBuckets(Shard shard, CacheSelection plan, string afterKey)
    {
        if (plan.Kind is CacheSelectionKind.Equal or CacheSelectionKind.Contains or CacheSelectionKind.ContainsAny)
        {
            foreach (var value in plan.Values.Distinct())
            {
                if (shard.Values.TryGetValue(new(plan.Name, value.Kind, value.CanonicalValue), out var bucket))
                {
                    yield return After(bucket, afterKey);
                }
            }

            yield break;
        }

        if (plan.Kind == CacheSelectionKind.Present)
        {
            if (shard.Presence.TryGetValue(plan.Name, out var bucket))
            {
                yield return After(bucket, afterKey);
            }

            yield break;
        }

        if (plan.Kind is CacheSelectionKind.Key or CacheSelectionKind.Prefix)
        {
            var prefix = plan.Kind == CacheSelectionKind.Key ? plan.Text + "\0" : plan.Text;
            yield return PrefixRange(shard.Keys, prefix, afterKey);
            yield break;
        }

        if (plan.Kind == CacheSelectionKind.Pattern)
        {
            // Leading literal characters prune glob candidates; complex suffixes use a bounded key-index walk.
            var literal = LeadingLiteral(plan.Text);
            yield return literal.Length == 0 ? After(shard.Keys, afterKey) : PrefixRange(shard.Keys, literal, afterKey);
            yield break;
        }

        yield return After(shard.Keys, afterKey); // OR/absence remain bounded metadata-index walks, never payload scans.
    }

    private static void AddKeys(IEnumerable<IEnumerable<KeyEntry>> buckets, string afterKey,
        List<InMemoryCacheIndexCandidate> output, int maximum)
    {
        var queue = new PriorityQueue<IEnumerator<KeyEntry>, string>(StringComparer.Ordinal);
        var enumerators = new List<IEnumerator<KeyEntry>>();
        try
        {
            foreach (var bucket in buckets)
            {
                var enumerator = bucket.GetEnumerator();
                enumerators.Add(enumerator);
                while (enumerator.MoveNext())
                {
                    if (afterKey is null || StringComparer.Ordinal.Compare(enumerator.Current.Key, afterKey) > 0)
                    {
                        queue.Enqueue(enumerator, enumerator.Current.Key);
                        break;
                    }
                }
            }

            string previous = null;
            while (queue.Count != 0 && output.Count < maximum)
            {
                var enumerator = queue.Dequeue();
                var item = enumerator.Current;
                if (previous != item.Key)
                {
                    output.Add(new(item.Entry));
                    previous = item.Key;
                }

                if (enumerator.MoveNext())
                {
                    queue.Enqueue(enumerator, enumerator.Current.Key);
                }
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                enumerator.Dispose();
            }
        }
    }

    private static void AddNumeric(SortedSet<NumericEntry> source, long lower, long upper, long? afterOrder, string afterKey,
        List<InMemoryCacheIndexCandidate> output, int maximum)
    {
        var start = afterOrder.HasValue && afterOrder.Value >= lower ? new NumericEntry(afterOrder.Value, afterKey, null) : new(lower, "", null);
        if (start.Value > upper)
        {
            return;
        }

        foreach (var item in source.GetViewBetween(start, new(upper, MaximumKey, null)))
        {
            if (item.Value >= upper)
            {
                break;
            }

            if (!afterOrder.HasValue || item.Value > afterOrder.Value ||
                (item.Value == afterOrder.Value && StringComparer.Ordinal.Compare(item.Key, afterKey) > 0))
            {
                output.Add(new(item.Entry, item.Value));
                if (output.Count >= maximum)
                {
                    break;
                }
            }
        }
    }

    private static SortedSet<KeyEntry> Bucket<TKey>(Dictionary<TKey, SortedSet<KeyEntry>> buckets, TKey key)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            buckets.Add(key, bucket = new(KeyComparer));
        }

        return bucket;
    }

    private static IEnumerable<KeyEntry> After(SortedSet<KeyEntry> source, string afterKey) => afterKey is null ? source :
        source.GetViewBetween(new(afterKey, null), new(MaximumKey, null));

    private static IEnumerable<KeyEntry> PrefixRange(SortedSet<KeyEntry> source, string prefix, string afterKey)
    {
        var lower = afterKey is not null && StringComparer.Ordinal.Compare(afterKey, prefix) > 0 ? afterKey : prefix;
        var upper = NextPrefix(prefix);
        return StringComparer.Ordinal.Compare(lower, upper) > 0 ? [] : source.GetViewBetween(new(lower, null), new(upper, null));
    }

    private static void RemoveBucket<TKey>(Dictionary<TKey, SortedSet<KeyEntry>> buckets, TKey key, KeyEntry entry)
    {
        if (buckets.TryGetValue(key, out var bucket))
        {
            bucket.Remove(entry);
            if (bucket.Count == 0)
            {
                buckets.Remove(key);
            }
        }
    }

    private static long Number(CacheDimensionValue value) => long.Parse(value.CanonicalValue, CultureInfo.InvariantCulture);

    private static string NextPrefix(string prefix)
    {
        var chars = prefix.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] != char.MaxValue)
            {
                chars[i]++;
                return new(chars, 0, i + 1);
            }
        }

        return MaximumKey;
    }

    private static string LeadingLiteral(string pattern)
    {
        var literal = new System.Text.StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] is '*' or '?')
            {
                break;
            }

            if (pattern[i] == '\\')
            {
                i++;
            }

            literal.Append(pattern[i]);
        }

        return literal.ToString();
    }

    private sealed class Shard(ResourceCharge reservation)
    {
        /// <summary>Serializes this shard's mutable indexes without spanning asynchronous work.</summary>
        public readonly Lock Gate = new();

        /// <summary>Retains finite shard bookkeeping capacity until index disposal.</summary>
        public readonly ResourceCharge Reservation = reservation;

        /// <summary>Orders immutable generations by canonical composite key.</summary>
        public readonly SortedSet<KeyEntry> Keys = new(KeyComparer);

        /// <summary>Orders generations by publication timestamp and composite key.</summary>
        public readonly SortedSet<NumericEntry> Published = new(NumericComparer);

        /// <summary>Orders generations by stored byte size and composite key.</summary>
        public readonly SortedSet<NumericEntry> Sizes = new(NumericComparer);

        /// <summary>Orders generations by current authoritative expiry and composite key.</summary>
        public readonly SortedSet<NumericEntry> Expiry = new(NumericComparer);

        /// <summary>Orders generations by absolute expiry cap and composite key.</summary>
        public readonly SortedSet<NumericEntry> AbsoluteExpiry = new(NumericComparer);

        /// <summary>Indexes dimension presence by canonical composite key.</summary>
        public readonly Dictionary<string, SortedSet<KeyEntry>> Presence = new(StringComparer.Ordinal);

        /// <summary>Indexes exact typed dimension values by canonical composite key.</summary>
        public readonly Dictionary<ValueKey, SortedSet<KeyEntry>> Values = [];

        /// <summary>Indexes interval overlaps separately per canonical dimension name.</summary>
        public readonly Dictionary<string, InMemoryCacheIntervalIndex> Intervals = new(StringComparer.Ordinal);
    }

    private readonly record struct KeyEntry(string Key, InMemoryCacheEntry Entry);
    private readonly record struct NumericEntry(long Value, string Key, InMemoryCacheEntry Entry);
    private readonly record struct ValueKey(string Name, CacheDimensionKind Kind, string Value);
}

/// <summary>Returns a bounded copied candidate slice with indexed continuation availability.</summary>
/// <param name="Candidates">The bounded copied immutable candidates.</param>
/// <param name="HasMore">Whether indexed candidates remain.</param>
/// <example><code>foreach (var candidate in page.Candidates) { Inspect(candidate.Entry.Metadata); }</code></example>
public readonly record struct InMemoryCacheIndexSlice(IReadOnlyList<InMemoryCacheIndexCandidate> Candidates, bool HasMore);