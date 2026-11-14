// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds an indexed refresh-candidate query for the current declared contract.</summary>
/// <param name="Namespace">The application namespace.</param>
/// <param name="Collection">The declared logical collection.</param>
/// <param name="ContractVersion">The host's current contract version; other deployments' contracts are excluded.</param>
/// <param name="Now">The fixed sweep time used for validity and activity eligibility.</param>
/// <param name="RefreshAhead">The maximum lead, additionally capped to twenty percent of published lifetime.</param>
/// <param name="ActivityWindow">The finite foreground-activity window.</param>
/// <param name="Limit">The maximum indexed candidates examined, at most 1,000.</param>
/// <param name="Continuation">The provider-issued bounded index continuation, or null for a new pass.</param>
/// <example><code>var due = await provider.QueryRefreshAsync(new("catalog", "cards", "1", now, lead, activity));</code></example>
public sealed record CacheRefreshQuery(string Namespace, string Collection, string ContractVersion, DateTimeOffset Now,
    TimeSpan RefreshAhead, TimeSpan ActivityWindow, int Limit = 128, string Continuation = null);

/// <summary>Returns eligible metadata and bounded index progress without opening payloads or observing foreground access.</summary>
/// <param name="Entries">Current valid due versions, excluding negative markers and versions in scheduler backoff.</param>
/// <param name="Continuation">Opaque index progress, or null when the pass is complete.</param>
/// <param name="CandidatesExamined">The candidates charged to this page's finite allowance.</param>
/// <example><code>foreach (var metadata in page.Entries) { Inspect(metadata.Version); }</code></example>
public sealed record CacheRefreshPage(IReadOnlyList<CacheEntryMetadata> Entries, string Continuation, int CandidatesExamined);

/// <summary>Records scheduler backoff only if the selected version is still current.</summary>
/// <param name="Identity">The exact current-contract identity.</param>
/// <param name="Version">The version selected before the attempt.</param>
/// <param name="SourceFailed">Whether the repeatable source failed; successful work clears scheduler failures.</param>
/// <param name="CompletedAt">The completion timestamp used for finite backoff.</param>
/// <example><code>await provider.RecordRefreshAttemptAsync(new(identity, version, true, now));</code></example>
public sealed record CacheRefreshAttempt(CacheEntryIdentity Identity, CacheEntryVersion Version, bool SourceFailed, DateTimeOffset CompletedAt);