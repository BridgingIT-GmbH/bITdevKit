// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Binds generated cache paths to one dedicated root and validates its durable identity, profile and recovery generation before use.</summary>
/// <example><code>var root = new CacheStoreRoot(profile, transient, resident); var path = root.ShardDirectory(identity);</code></example>
public sealed class CacheStoreRoot(FileSystemCacheOptions profile, ResourceBudget transient, ResourceBudget resident)
{
    private const string HeaderName = "cache-root.json";

    /// <summary>Gets the validated native filesystem access and quota profile for this root.</summary>
    public FileSystemCacheOptions Profile { get; } = profile;

    /// <summary>Gets the normalized absolute dedicated cache root directory.</summary>
    public string Path { get; } = profile.RootPath;

    /// <summary>Creates an admitted empty root or verifies its existing checked identity and profile, rejecting roots fenced for offline recovery.</summary>
    public async ValueTask<CacheMetadataDocument<CacheStoreHeader>> OpenAsync(CancellationToken cancellationToken)
    {
        FileSystemPaths.RejectLinks(this.Path);
        CacheFileSystemAccess.ValidateBeforeCreate(this.Path, this.Profile.Native.AccessKind);
        FileSystemDurability.EnsureDirectory(this.Path);
        using var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.Path, "authority", false, cancellationToken).ConfigureAwait(false);
        if (authority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        var path = this.Resolve(HeaderName);
        if (!File.Exists(path))
        {
            // A selected root belongs to this format. Never adopt or repair another provider's existing files.
            if (Directory.EnumerateFileSystemEntries(this.Path).Any(p => System.IO.Path.GetFileName(p) != "authority"))
            {
                throw new CacheTransferException(CacheErrorCode.Compatibility);
            }

            FileSystemDurability.ValidatePublicationDirectory(this.Path);
            var header = new CacheStoreHeader { StoreId = Guid.NewGuid().ToString("N"), AccessKind = this.Profile.Native.AccessKind,
                Durability = this.Profile.Native.Durability, MaximumBytes = this.Profile.Native.MaxStorageBytes, MaximumEntries = this.Profile.Native.MaxEntries };
            // Store identity is durable in both acknowledgement profiles; Buffered applies to cache data generations.
            await CacheCheckedMetadata.WriteAsync(this.Path, HeaderName, header, authority, true, transient, resident, cancellationToken).ConfigureAwait(false);
        }

        var document = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(path, transient, resident, cancellationToken).ConfigureAwait(false);
        try
        {
            document.Value.Validate(this.Profile);
            if (document.Value.RecoveryInProgress) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            return document;
        }
        catch { document.Dispose(); throw; }
    }

    /// <summary>Builds a collection directory using only the digest of its canonical collection identity.</summary>
    public string CollectionDirectory(CacheEntryIdentity identity)
    {
        if (!identity.IsValid) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return this.Resolve("collections", Digest(identity.CollectionIdentity));
    }

    /// <summary>Durably records completed control-ledger initialization so missing committed control files cannot be mistaken for a fresh cache.</summary>
    public async ValueTask<CacheStoreHeader> MarkInitializedAsync(CacheStoreHeader expected, CancellationToken cancellationToken)
    {
        using var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.Path, "authority", false, cancellationToken).ConfigureAwait(false);
        if (authority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        using var stored = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(this.Resolve(HeaderName), transient, resident, cancellationToken).ConfigureAwait(false);
        var value = stored.Value;
        value.Validate(this.Profile);
        if (value.StoreId != expected.StoreId || value.RecoveryGeneration != expected.RecoveryGeneration || value.RecoveryInProgress)
        {
            throw new CacheTransferException(CacheErrorCode.ProviderUnavailable);
        }

        if (value.LedgerInitialized) { return value; }

        value = value with { LedgerInitialized = true };
        await CacheCheckedMetadata.WriteAsync(this.Path, HeaderName, value, authority, true, transient, resident, cancellationToken).ConfigureAwait(false);
        return value;
    }

    /// <summary>Builds a generated collection-shard directory for an exact identity or explicit fixed shard number.</summary>
    public string ShardDirectory(CacheEntryIdentity identity) => this.ShardDirectory(identity, Shard(identity));

    /// <summary>Builds a generated collection-shard directory for an exact identity or explicit fixed shard number.</summary>
    public string ShardDirectory(CacheEntryIdentity identity, int shard)
    {
        if (shard is < 0 or > 255) { throw new CacheTransferException(CacheErrorCode.Validation); }

        var collection = this.CollectionDirectory(identity);
        return this.Resolve(System.IO.Path.GetRelativePath(this.Path, collection), "shards", shard.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Combines validated literal path components beneath the dedicated root and rejects filesystem links.</summary>
    public string Resolve(params string[] generatedComponents)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine([this.Path, .. generatedComponents]));
        var boundary = System.IO.Path.TrimEndingDirectorySeparator(this.Path) + System.IO.Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        FileSystemPaths.RejectLinks(path);
        return path;
    }

    /// <summary>Maps the logical-key identity digest to one of 256 shards so every value contract for the same key shares native authority.</summary>
    public static int Shard(CacheEntryIdentity identity) => SHA256.HashData(Encoding.UTF8.GetBytes(
        new CacheEntryIdentity(identity.Namespace, identity.Collection, "logical-key", identity.Key).Canonical))[0];

    /// <summary>Computes a lowercase hexadecimal SHA-256 digest of the supplied UTF-8 canonical identity text.</summary>
    public static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

/// <summary>Persists the root identity, native profile, quota limits and durable generation fence used by every cache participant.</summary>
/// <example><code>header.Validate(profile); var inspectedIdentity = header.StoreId;</code></example>
public sealed record CacheStoreHeader
{
    /// <summary>Gets the checked root serialization format, currently one.</summary>
    public int Format { get; init; } = 1;

    /// <summary>Gets the canonical cache-identity encoding version accepted by this store.</summary>
    public int IdentityFormat { get; init; } = 1;

    /// <summary>Gets the fixed shard count, currently 256 per logical collection.</summary>
    public int Shards { get; init; } = 256;

    /// <summary>Gets the generated identity that changes when the disposable store is explicitly reset.</summary>
    public string StoreId { get; init; }

    /// <summary>Gets the native local or shared access profile all participants must use.</summary>
    public string AccessKind { get; init; }

    /// <summary>Gets the payload acknowledgement profile; control receipts remain durable independently.</summary>
    public string Durability { get; init; }

    /// <summary>Gets the shared store&apos;s fixed maximum attributed physical bytes.</summary>
    public long MaximumBytes { get; init; }

    /// <summary>Gets the shared store&apos;s fixed maximum current and prepared entry count.</summary>
    public long MaximumEntries { get; init; }

    /// <summary>Gets the durable generation advanced before any offline recovery cleanup begins.</summary>
    public long RecoveryGeneration { get; init; }

    /// <summary>Gets whether normal participants must remain fenced while offline verification or reset is incomplete.</summary>
    public bool RecoveryInProgress { get; init; }

    /// <summary>Gets the previous generation accepted only by the explicitly stopped-participant recovery procedure.</summary>
    public long? RecoveryBaseGeneration { get; init; }

    /// <summary>Gets whether committed control state has existed and may no longer be silently recreated if missing.</summary>
    public bool LedgerInitialized { get; init; }

    /// <summary>Gets the serialized reserved collection catalog field retained by the root format.</summary>
    public IReadOnlyList<string> Collections { get; init; } = [];

    /// <summary>Gets the serialized reserved contract catalog field retained by the root format.</summary>
    public IReadOnlyList<string> Contracts { get; init; } = [];

    /// <summary>Rejects mismatched identity format, native profile, fixed quotas and inconsistent recovery-fence fields.</summary>
    public void Validate(FileSystemCacheOptions profile)
    {
        if (this.Format != 1 || this.IdentityFormat != 1 || this.Shards != 256 || !Guid.TryParseExact(this.StoreId, "N", out _) ||
            this.RecoveryGeneration < 0 || (this.RecoveryInProgress && (!this.RecoveryBaseGeneration.HasValue || this.RecoveryBaseGeneration < 0 ||
                this.RecoveryBaseGeneration >= this.RecoveryGeneration)) || (!this.RecoveryInProgress && this.RecoveryBaseGeneration.HasValue) ||
            !Digests(this.Collections) || !Digests(this.Contracts))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        if (this.AccessKind != profile.Native.AccessKind || this.Durability != profile.Native.Durability ||
            this.MaximumBytes != profile.Native.MaxStorageBytes || this.MaximumEntries != profile.Native.MaxEntries)
        {
            throw new CacheTransferException(CacheErrorCode.Compatibility);
        }
    }

    private static bool Digests(IReadOnlyList<string> values) => values is not null && values.Count <= 1024 &&
        values.All(v => v?.Length == 64 && v.All(char.IsAsciiHexDigit)) && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
}