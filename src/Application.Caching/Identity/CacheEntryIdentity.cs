// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>Identifies one entry with ordinal-ignore-case equivalence and explicit contract isolation.</summary>
/// <example><code>var identity = new CacheEntryIdentity("catalog", "cards", "v1", "product:42");</code></example>
public readonly record struct CacheEntryIdentity
{
    /// <summary>Creates a validated immutable identity.</summary>
    [JsonConstructor]
    public CacheEntryIdentity(string @namespace, string collection, string contractVersion, string key)
    {
        this.Namespace = CacheIdentityNormalizer.Namespace(@namespace);
        this.Collection = CacheIdentityNormalizer.Name(collection);
        this.ContractVersion = CacheIdentityNormalizer.ContractVersion(contractVersion);
        this.Key = CacheIdentityNormalizer.Key(key);
    }

    /// <summary>Gets the canonical application namespace.</summary>
    public string Namespace { get; }

    /// <summary>Gets the canonical collection name.</summary>
    public string Collection { get; }

    /// <summary>Gets the canonical contract version.</summary>
    public string ContractVersion { get; }

    /// <summary>Gets the canonical opaque key.</summary>
    public string Key { get; }

    /// <summary>Gets an unambiguous format-one representation; physical digests still require identity checks.</summary>
    [JsonIgnore]
    public string Canonical => "1:" + Part(this.Namespace) + Part(this.Collection) + Part(this.ContractVersion) + Part(this.Key);

    /// <summary>Gets a deterministic physical key, never a caller-supplied filesystem path.</summary>
    [JsonIgnore]
    public string Digest => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(this.Canonical)));

    /// <summary>Gets a logical collection identity shared by all retained contract versions.</summary>
    [JsonIgnore]
    public string CollectionIdentity => "1:" + Part(this.Namespace) + Part(this.Collection);

    /// <summary>Gets whether this is a constructed identity rather than an uninitialized struct.</summary>
    [JsonIgnore]
    public bool IsValid => this.Namespace is not null && this.Collection is not null && this.ContractVersion is not null && this.Key is not null;

    private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
}