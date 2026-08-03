// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides atomic access to process-local blob content shared by in-memory providers.
/// </summary>
/// <example>
/// <code>
/// var context = new InMemoryBlobStoreContext();
/// var provider = new InMemoryBlobStoreProvider(context);
/// </code>
/// </example>
public sealed class InMemoryBlobStoreContext
{
    private readonly object syncRoot = new();
    private readonly Dictionary<BlobKey, InMemoryBlobEntry> blobs = [];

    /// <summary>
    /// Adds or replaces an entry atomically and returns a detached snapshot of the stored entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="failIfExists">Whether an existing entry prevents the write.</param>
    /// <param name="stored">The detached stored entry when the write succeeds.</param>
    /// <returns><see langword="true" /> when the entry was stored.</returns>
    /// <example>
    /// <code>
    /// var written = context.TryStore(entry, failIfExists: true, out var stored);
    /// </code>
    /// </example>
    public bool TryStore(InMemoryBlobEntry entry, bool failIfExists, out InMemoryBlobEntry stored)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Info);

        lock (this.syncRoot)
        {
            if (failIfExists && this.blobs.ContainsKey(entry.Info.Key))
            {
                stored = null;
                return false;
            }

            var createdAt = this.blobs.TryGetValue(entry.Info.Key, out var existing)
                ? existing.Info.CreatedAt
                : entry.Info.CreatedAt;
            var normalized = Clone(entry, createdAt);
            this.blobs[entry.Info.Key] = normalized;
            stored = Clone(normalized);
            return true;
        }
    }

    /// <summary>
    /// Gets a detached snapshot for a key.
    /// </summary>
    /// <param name="key">The blob key.</param>
    /// <param name="entry">The detached entry when found.</param>
    /// <returns><see langword="true" /> when the key exists.</returns>
    /// <example>
    /// <code>
    /// var found = context.TryGet(key, out var entry);
    /// </code>
    /// </example>
    public bool TryGet(BlobKey key, out InMemoryBlobEntry entry)
    {
        lock (this.syncRoot)
        {
            if (this.blobs.TryGetValue(key, out var found))
            {
                entry = Clone(found);
                return true;
            }

            entry = null;
            return false;
        }
    }

    /// <summary>
    /// Updates one entry atomically using a detached input snapshot.
    /// </summary>
    /// <param name="key">The blob key.</param>
    /// <param name="ifMatchETag">The optional required ETag.</param>
    /// <param name="update">Creates the replacement from a detached current entry.</param>
    /// <param name="stored">The detached replacement when successful.</param>
    /// <param name="etagMismatch">Whether the update failed because the ETag did not match.</param>
    /// <returns><see langword="true" /> when the entry was updated.</returns>
    /// <example>
    /// <code>
    /// var updated = context.TryUpdate(key, etag, current => replacement, out var stored, out var conflict);
    /// </code>
    /// </example>
    public bool TryUpdate(
        BlobKey key,
        string ifMatchETag,
        Func<InMemoryBlobEntry, InMemoryBlobEntry> update,
        out InMemoryBlobEntry stored,
        out bool etagMismatch)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (this.syncRoot)
        {
            etagMismatch = false;
            if (!this.blobs.TryGetValue(key, out var current))
            {
                stored = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ifMatchETag) &&
                !string.Equals(ifMatchETag, current.Info.ETag, StringComparison.Ordinal))
            {
                stored = null;
                etagMismatch = true;
                return false;
            }

            var replacement = update(Clone(current)) ?? throw new InvalidOperationException("The update must return an entry.");
            var normalized = Clone(replacement, current.Info.CreatedAt);
            this.blobs[key] = normalized;
            stored = Clone(normalized);
            return true;
        }
    }

    /// <summary>
    /// Determines whether a key exists.
    /// </summary>
    /// <param name="key">The blob key.</param>
    /// <returns><see langword="true" /> when the key exists.</returns>
    /// <example>
    /// <code>
    /// var exists = context.Contains(key);
    /// </code>
    /// </example>
    public bool Contains(BlobKey key)
    {
        lock (this.syncRoot)
        {
            return this.blobs.ContainsKey(key);
        }
    }

    /// <summary>
    /// Returns detached snapshots of all entries.
    /// </summary>
    /// <returns>A stable detached snapshot.</returns>
    /// <example>
    /// <code>
    /// var entries = context.GetSnapshot();
    /// </code>
    /// </example>
    public IReadOnlyCollection<InMemoryBlobEntry> GetSnapshot()
    {
        lock (this.syncRoot)
        {
            return this.blobs.Values.Select(entry => Clone(entry)).ToArray();
        }
    }

    /// <summary>
    /// Removes an entry atomically when its optional ETag matches.
    /// </summary>
    /// <param name="key">The blob key.</param>
    /// <param name="ifMatchETag">The optional required ETag.</param>
    /// <param name="etagMismatch">Whether an existing entry had a different ETag.</param>
    /// <returns><see langword="true" /> when an entry was removed.</returns>
    /// <example>
    /// <code>
    /// var removed = context.TryRemove(key, etag, out var conflict);
    /// </code>
    /// </example>
    public bool TryRemove(BlobKey key, string ifMatchETag, out bool etagMismatch)
    {
        lock (this.syncRoot)
        {
            etagMismatch = false;
            if (!this.blobs.TryGetValue(key, out var current))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ifMatchETag) &&
                !string.Equals(ifMatchETag, current.Info.ETag, StringComparison.Ordinal))
            {
                etagMismatch = true;
                return false;
            }

            return this.blobs.Remove(key);
        }
    }

    /// <summary>
    /// Removes up to the requested number of entries that expired on or before a timestamp.
    /// </summary>
    /// <param name="expiresOnOrBefore">The inclusive expiration boundary.</param>
    /// <param name="take">The maximum number of entries to remove.</param>
    /// <returns>
    /// The keys of removed entries.
    /// </returns>
    /// <example>
    /// <code>
    /// var removed = context.RemoveExpired(DateTimeOffset.UtcNow, 100);
    /// </code>
    /// </example>
    public IReadOnlyList<BlobKey> RemoveExpired(DateTimeOffset expiresOnOrBefore, int take)
    {
        lock (this.syncRoot)
        {
            var keys = this.blobs
                .Where(item => item.Value.Info.ExpiresAt is not null && item.Value.Info.ExpiresAt <= expiresOnOrBefore)
                .OrderBy(item => item.Value.Info.ExpiresAt)
                .ThenBy(item => item.Key.Container, StringComparer.Ordinal)
                .ThenBy(item => item.Key.Name, StringComparer.Ordinal)
                .Take(Math.Max(1, take))
                .Select(item => item.Key)
                .ToArray();

            foreach (var key in keys)
            {
                this.blobs.Remove(key);
            }

            return keys;
        }
    }

    private static InMemoryBlobEntry Clone(InMemoryBlobEntry entry, DateTimeOffset? createdAt = null) => new()
    {
        Content = [.. entry.Content],
        Info = new BlobInfo
        {
            Key = entry.Info.Key,
            Length = entry.Info.Length,
            ContentType = entry.Info.ContentType,
            ContentHash = entry.Info.ContentHash,
            ETag = entry.Info.ETag,
            CreatedAt = createdAt ?? entry.Info.CreatedAt,
            LastModifiedAt = entry.Info.LastModifiedAt,
            ExpiresAt = entry.Info.ExpiresAt,
            Properties = entry.Info.Properties?.Clone() ?? new PropertyBag()
        }
    };
}
