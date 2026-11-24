// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies host-authorized management scope without retaining an HTTP principal or accepting scope from payload dimensions.</summary>
/// <param name="Namespace">Trusted application namespace.</param>
/// <param name="SubjectId">Authenticated host identity or explicit trusted process identity.</param>
/// <param name="Origin">Bounded transport identifier for audit attribution.</param>
/// <param name="Actions">Actions already authorized by the host.</param>
/// <param name="Collections">Optional authorized collection subset; null permits declared collections.</param>
/// <param name="RevealSensitiveMetadata">Whether the host explicitly permits unredacted keys and dimensions.</param>
/// <example><code>var access = new CacheAccessContext("catalog", userId, "dashboard", CacheAccessActions.Read);</code></example>
public sealed record CacheAccessContext(string Namespace, string SubjectId, string Origin, CacheAccessActions Actions,
    IReadOnlyCollection<string> Collections = null, bool RevealSensitiveMetadata = false);

/// <summary>Separates management read, payload publication, and invalidation authority.</summary>
[Flags]
public enum CacheAccessActions
{
    /// <summary>Grants no authority.</summary>
    None = 0,

    /// <summary>Allows bounded metadata and payload inspection.</summary>
    Read = 1,

    /// <summary>Allows publication through a registered contract.</summary>
    Write = 2,

    /// <summary>Allows removal, invalidation, clear, and operational controls.</summary>
    Invalidate = 4
}

/// <summary>Describes one declared collection without provider connection secrets or executable component registrations.</summary>
/// <param name="Name">Canonical collection name.</param>
/// <param name="Contract">Registered value-contract alias.</param>
/// <param name="Provider">Registered complete provider name.</param>
/// <param name="ProviderKind">Stable native provider kind.</param>
/// <param name="Binary">Whether the declared contract is raw binary.</param>
/// <param name="Options">Immutable effective collection policy.</param>
/// <param name="Capabilities">Truthful native capabilities.</param>
/// <example><code>var collections = await requester.SendAsync(new CacheCollectionsQuery(access));</code></example>
public sealed record CacheCollectionInfo(string Name, string Contract, string Provider, string ProviderKind, bool Binary,
    CacheCollectionOptions Options, CacheProviderCapabilities Capabilities);

/// <summary>Provides one bounded, redacted metadata page without reading payloads.</summary>
/// <param name="Entries">Visible metadata records.</param>
/// <param name="Count">Provider-reported count for the selection.</param>
/// <param name="CountIsExact">Whether that count is exact.</param>
/// <param name="Completed">Whether the current bounded scan completed.</param>
/// <param name="Continuation">Opaque continuation for the same selection.</param>
/// <param name="ObservedAt">UTC observation time.</param>
/// <param name="Cutoff">Fixed publication cutoff which must accompany every continuation for this browsing session.</param>
/// <example><code>var page = await requester.SendAsync(new CacheEntriesQuery(access, "cards"));</code></example>
public sealed record CacheEntriesPage(IReadOnlyList<CacheEntryMetadata> Entries, long Count, bool CountIsExact,
    bool Completed, string Continuation, DateTimeOffset ObservedAt, DateTimeOffset Cutoff);