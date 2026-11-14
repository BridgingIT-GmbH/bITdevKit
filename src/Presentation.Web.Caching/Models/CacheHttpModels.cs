// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Contains one bounded structured invalidation selection. Targets remain route/query parameters so exposure precedes body reads.</summary>
/// <param name="Selection">Validated metadata selection; omission matches nothing.</param>
/// <param name="Background">Whether submission returns accepted tracked work.</param>
/// <example><code>var request = new CacheHttpInvalidationRequest(CacheSelection.Prefix("product:"));</code></example>
public sealed record CacheHttpInvalidationRequest(CacheSelection Selection = null, bool Background = false);

/// <summary>Describes a prepared write without taking ownership of the HTTP request stream.</summary>
/// <param name="Input">Caller-owned stream for binary/validated encoded publication.</param>
/// <param name="Value">Explicitly boundary-mapped typed value.</param>
/// <param name="Typed">Whether the registered public model mapping produced Value.</param>
/// <param name="Reservation">Owned conservative graph reservation, retained through actual publication.</param>
/// <remarks>When handing the reservation to CacheResourceLeaseContext.Begin, dispose that scope instead of this record so tracked work can retain its share.</remarks>
/// <example><code>using var lease = CacheResourceLeaseContext.Begin(prepared.Reservation); await PublishAsync(prepared);</code></example>
public sealed record CacheHttpPreparedWrite(Stream Input, object Value, bool Typed, IDisposable Reservation = null) : IDisposable
{
    /// <summary>Releases HTTP-owned graph admission after actual publication ends; the request stream remains caller-owned.</summary>
    public void Dispose() => this.Reservation?.Dispose();
}

/// <summary>Describes the public metadata view independently of provider-native entities.</summary>
/// <param name="Key">Authorized, possibly redacted display key.</param>
/// <param name="Version">Opaque acquired entry version.</param>
/// <param name="NotFound">Whether the acquired snapshot is a confirmed absence marker.</param>
/// <param name="PublishedAt">UTC publication instant.</param>
/// <param name="ExpiresAt">Effective authoritative UTC expiry.</param>
/// <param name="LogicalBytes">Logical representation size.</param>
/// <param name="StoredBytes">Stored provider representation size.</param>
/// <example><code>var model = CacheHttpEntryMetadata.From(metadata);</code></example>
public sealed record CacheHttpEntryMetadata(string Key, string Version, bool NotFound, DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt, long LogicalBytes, long StoredBytes)
{
    /// <summary>Maps already authorized/redacted metadata without exposing native ownership or storage records.</summary>
    public static CacheHttpEntryMetadata From(CacheEntryMetadata metadata) => new(metadata.DisplayKey, metadata.Version.Value,
        metadata.IsNotFound, metadata.PublishedAt, metadata.ExpiresAt, metadata.LogicalSizeBytes, metadata.StoredSizeBytes);
}

/// <summary>Returns a bounded public metadata page without provider-native ownership/index records.</summary>
/// <param name="Entries">Authorized public metadata models.</param>
/// <param name="Count">Observed or exact matching count.</param>
/// <param name="CountIsExact">Whether the count covers the complete selection.</param>
/// <param name="Completed">Whether the current bounded walk completed.</param>
/// <param name="Continuation">Opaque continuation for this selection.</param>
/// <param name="Cutoff">Fixed publication cutoff required with continuation.</param>
/// <param name="ObservedAt">UTC observation time.</param>
/// <example><code>UseNextPage(page.Continuation, page.Cutoff);</code></example>
public sealed record CacheHttpEntriesResponse(IReadOnlyList<CacheHttpEntryMetadata> Entries, long Count, bool CountIsExact,
    bool Completed, string Continuation, DateTimeOffset Cutoff, DateTimeOffset ObservedAt);

/// <summary>Returns the actual write acknowledgement independently of HTTP request acceptance.</summary>
/// <param name="StorageOutcome">Confirmed storage or controlled suppression/conflict state.</param>
/// <param name="Version">Opaque confirmed version, absent without confirmed publication.</param>
/// <example><code>if (response.StorageOutcome == CacheStorageOutcome.Stored) { Retained(); }</code></example>
public sealed record CacheHttpWriteResponse(CacheStorageOutcome StorageOutcome, string Version);

/// <summary>Returns one bounded tracked operation without internal revisions or provider request records.</summary>
/// <param name="Id">Opaque retained handle.</param>
/// <param name="Collection">Declared target collection.</param>
/// <param name="Clear">Whether this is explicit clear.</param>
/// <param name="State">Observed execution state.</param>
/// <param name="CancellationRequested">Whether cancellation is waiting for a safe boundary.</param>
/// <param name="AffectedCount">Confirmed committed affected entries.</param>
/// <param name="CountIsExact">Count accuracy.</param>
/// <param name="Completed">Whether logical scope is complete.</param>
/// <param name="CreatedAt">UTC creation time.</param>
/// <param name="UpdatedAt">UTC latest transition.</param>
/// <example><code>ShowCommittedProgress(status.AffectedCount, status.Completed);</code></example>
public sealed record CacheHttpOperationStatus(string Id, string Collection, bool Clear, string State, bool CancellationRequested,
    long? AffectedCount, bool? CountIsExact, bool? Completed, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>Maps authorized application progress without exposing its native checkpoint revision.</summary>
    public static CacheHttpOperationStatus From(CacheProviderOperation operation) => new(operation.Id, operation.Collection,
        operation.Request?.Clear == true, operation.State.ToString(), operation.CancellationRequested, operation.Outcome?.AffectedCount,
        operation.Outcome?.CountIsExact, operation.Outcome?.Completed ?? operation.State == CacheOperationState.Completed, operation.CreatedAt, operation.UpdatedAt);
}

/// <summary>Describes one child submission, preserving acceptance and partial failures separately.</summary>
/// <param name="Collection">Declared target.</param>
/// <param name="Accepted">Whether background work was accepted.</param>
/// <param name="Status">Actual retained status, or null on a failed child.</param>
/// <param name="Location">Direct authorized status URL.</param>
/// <param name="Error">Controlled child failure.</param>
/// <example><code>foreach (var child in response) { Show(child.Accepted, child.Status, child.Error); }</code></example>
public sealed record CacheHttpOperationResponse(string Collection, bool Accepted, CacheHttpOperationStatus Status, string Location, string Error = null);

/// <summary>Returns bounded invalidation scope without exposing provider-native metadata records.</summary>
/// <param name="Collection">Declared target.</param>
/// <param name="Count">Observed matching count, or null after failure.</param>
/// <param name="CountIsExact">Whether the count covers the complete selection.</param>
/// <param name="Completed">Whether bounded preview completed.</param>
/// <param name="Continuation">Opaque continuation, where available.</param>
/// <param name="Sample">Bounded authorized public metadata sample.</param>
/// <param name="Error">Controlled child failure.</param>
/// <example><code>ShowPreview(response.Count, response.CountIsExact);</code></example>
public sealed record CacheHttpPreviewResponse(string Collection, long? Count, bool CountIsExact, bool Completed,
    string Continuation, IReadOnlyList<CacheHttpEntryMetadata> Sample, string Error = null);