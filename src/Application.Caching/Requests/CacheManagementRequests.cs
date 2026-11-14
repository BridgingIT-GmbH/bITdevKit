// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Captures trusted access metadata shared by transport-neutral cache management requests.</summary>
/// <typeparam name="TValue">The bounded response contract.</typeparam>
/// <param name="access">Current host-authorized scope.</param>
/// <param name="collection">One declared target, or null for an explicit bounded multi-collection operation.</param>
/// <example><code>await requester.SendAsync(new CacheCollectionsQuery(access));</code></example>
public abstract class CacheManagementRequest<TValue>(CacheAccessContext access, string collection = null) : RequestBase<TValue>
{
    /// <summary>Gets current trusted host authorization and attribution.</summary>
    public CacheAccessContext Access { get; } = access;

    /// <summary>Gets the declared target collection.</summary>
    public string Collection { get; } = collection;
}

/// <summary>Lists declared collections within the current authorized scope.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheCollectionsQuery(CacheAccessContext access) : CacheManagementRequest<IReadOnlyList<CacheCollectionInfo>>(access)
{

}

/// <summary>Reads one bounded metadata page without opening payloads.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheEntriesQuery(CacheAccessContext access, string collection, CacheSelection selection = null, int take = 100, string continuation = null, DateTimeOffset? cutoff = null) : CacheManagementRequest<CacheEntriesPage>(access, collection)
{
    /// <summary>Gets the metadata selection; null means all keys.</summary>
    public CacheSelection Selection { get; } = selection ?? CacheSelection.Pattern("*");

    /// <summary>Gets the bounded requested page size.</summary>
    public int Take { get; } = take;

    /// <summary>Gets opaque provider continuation.</summary>
    public string Continuation { get; } = continuation;

    /// <summary>Gets the fixed publication cutoff returned by the first page; required with a continuation.</summary>
    public DateTimeOffset? Cutoff { get; } = cutoff;
}

/// <summary>Reads authoritative compatible snapshot metadata without opening or decoding bytes.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheEntryMetadataQuery(CacheAccessContext access, string collection, string key, CacheAccessActions requiredAction = CacheAccessActions.Read) : CacheManagementRequest<CacheLookup<CacheEntryMetadata>>(access, collection)
{
    /// <summary>Gets the exact opaque key.</summary>
    public string Key { get; } = key;

    /// <summary>Gets the already authorized action whose conditional resource operation needs this metadata.</summary>
    public CacheAccessActions RequiredAction { get; } = requiredAction;
}

/// <summary>Acquires one protected encoded/raw snapshot without any source loading.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheEntryReadQuery(CacheAccessContext access, string collection, string key, CacheReadOptions options = null) : CacheManagementRequest<CacheLookup<ICacheReadHandle>>(access, collection)
{
    /// <summary>Gets the exact opaque key.</summary>
    public string Key { get; } = key;

    /// <summary>Gets optional range, seeking, and expected-version constraints.</summary>
    public CacheReadOptions Options { get; } = options;
}

/// <summary>Publishes streamed or explicitly boundary-mapped content through the declared collection contract.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheEntryWriteCommand(CacheAccessContext access, string collection, CacheItem item, Stream input = null, object value = null, bool typed = false, CacheEntryDescriptor descriptor = null, CacheWriteCondition condition = default) : CacheManagementRequest<CacheWriteOutcome>(access, collection)
{
    /// <summary>Gets the key and declared dependencies.</summary>
    public CacheItem Item { get; } = item;

    /// <summary>Gets caller-owned streamed input, which the handler never disposes.</summary>
    public Stream Input { get; } = input;

    /// <summary>Gets the code-mapped typed value.</summary>
    public object Value { get; } = value;

    /// <summary>Gets whether an approved boundary mapping produced the typed value.</summary>
    public bool Typed { get; } = typed;

    /// <summary>Gets collection-bounded entry metadata overrides.</summary>
    public CacheEntryDescriptor Descriptor { get; } = descriptor;

    /// <summary>Gets conditions checked atomically again at publication.</summary>
    public CacheWriteCondition Condition { get; } = condition;
}

/// <summary>Removes one exact key with ordinary publication fencing and an optional atomic version condition.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheEntryRemoveCommand(CacheAccessContext access, string collection, string key, CacheWriteCondition condition = default) : CacheManagementRequest<bool>(access, collection)
{
    /// <summary>Gets the exact opaque key.</summary>
    public string Key { get; } = key;

    /// <summary>Gets the atomic acquired-version condition.</summary>
    public CacheWriteCondition Condition { get; } = condition;
}

/// <summary>Previews bounded selection scope without mutation or payload access.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheInvalidationPreviewQuery(CacheAccessContext access, string collection, CacheSelection selection) : CacheManagementRequest<CacheInvalidationPreview>(access, collection)
{
    /// <summary>Gets the structured selection; empty remains empty.</summary>
    public CacheSelection Selection { get; } = selection;
}

/// <summary>Submits explicit bounded collection invalidation through the common operation service.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheInvalidateCommand(CacheAccessContext access, IReadOnlyList<string> collections, CacheSelection selection, bool clear = false, bool background = false) : CacheManagementRequest<IReadOnlyList<Result<CacheOperationSubmission>>>(access)
{
    /// <summary>Gets the explicit bounded target set.</summary>
    public IReadOnlyList<string> Collections { get; } = CacheBoundedSnapshot.Copy(collections, 32, nameof(collections));

    /// <summary>Gets the selection, ignored only by explicit clear.</summary>
    public CacheSelection Selection { get; } = selection;

    /// <summary>Gets whether the explicit operation is clear.</summary>
    public bool Clear { get; } = clear;

    /// <summary>Gets whether acceptance returns before continuation execution.</summary>
    public bool Background { get; } = background;
}

/// <summary>Reads or controls one retained provider-owned operation under current authorization.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheOperationCommand(CacheAccessContext access, CacheOperationHandle handle, CacheOperationAction action = CacheOperationAction.Status) : CacheManagementRequest<CacheProviderOperation>(access, handle?.Collection)
{
    /// <summary>Gets the exact declared child operation.</summary>
    public CacheOperationHandle Handle { get; } = handle;

    /// <summary>Gets read, resume, or safe-boundary cancellation.</summary>
    public CacheOperationAction Action { get; } = action;
}

/// <summary>Reads or changes node-local automatic source admission without editing configuration.</summary>
/// <example><code>var result = await requester.SendAsync(request, cancellationToken: cancellationToken);</code></example>
public sealed class CacheBackgroundCommand(CacheAccessContext access, string collection, CacheBackgroundAction action = CacheBackgroundAction.Status) : CacheManagementRequest<CacheBackgroundState>(access, collection)
{
    /// <summary>Gets status, pause, or resume.</summary>
    public CacheBackgroundAction Action { get; } = action;
}

/// <summary>Identifies an operation control action.</summary>
public enum CacheOperationAction
{
    /// <summary>Reads retained status.</summary>
    Status,

    /// <summary>Continues the recorded remainder.</summary>
    Resume,

    /// <summary>Requests cancellation at a safe boundary.</summary>
    Cancel
}

/// <summary>Identifies node-local background source controls.</summary>
public enum CacheBackgroundAction
{
    /// <summary>Reads current configured and paused state.</summary>
    Status,

    /// <summary>Stops new automatic source admissions.</summary>
    Pause,

    /// <summary>Resumes already configured automatic work.</summary>
    Resume
}

/// <summary>Reads current bounded cache execution, provider, and effective-policy observations under current host scope.</summary>
/// <example><code>var report = await requester.SendAsync(new CacheDiagnosticsQuery(access, "cards"));</code></example>
public sealed class CacheDiagnosticsQuery(CacheAccessContext access, string collection = null) : CacheManagementRequest<CacheDiagnosticsReport>(access, collection);

/// <summary>Reads a finite process-local metric history under current authorization without touching payloads or sources.</summary>
/// <example><code>var history = await requester.SendAsync(new CacheMetricHistoryQueryRequest(access, new("cards")));</code></example>
public sealed class CacheMetricHistoryQueryRequest(CacheAccessContext access, CacheMetricHistoryQuery query) : CacheManagementRequest<CacheMetricHistoryResult>(access, query?.Collection)
{
    /// <summary>Gets the predeclared series/window/point selection.</summary>
    public CacheMetricHistoryQuery Query { get; } = query;
}

/// <summary>Lists the finite predeclared diagnostic series available for one authorized scope.</summary>
/// <example><code>var series = await requester.SendAsync(new CacheMetricSeriesQuery(access, "cards"));</code></example>
public sealed class CacheMetricSeriesQuery(CacheAccessContext access, string collection = null) : CacheManagementRequest<IReadOnlyList<CacheMetricSeriesDescriptor>>(access, collection);