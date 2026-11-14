// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Connects optional observers to the existing source terminal latch without polling, tasks or stream buffering.</summary>
/// <remarks>Live transfer gauges use actual resource admission occupancy. Terminal timeout does not release an ignored native read.</remarks>
/// <example><code>var session = CacheTransferObservationSession.Start("CARDS", "DATABASE", false, slot, clock, observers);</code></example>
public sealed class CacheTransferObservationSession
{
    private readonly CacheTransferContext context;
    private readonly CacheObservationSlot slot;
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly IReadOnlyList<ICacheTransferObservationScope> scopes;
    private int completed;

    private CacheTransferObservationSession(CacheTransferContext context, CacheObservationSlot slot, TimeProvider clock,
        IReadOnlyList<ICacheTransferObservationScope> scopes)
    {
        this.context = context;
        this.slot = slot;
        this.clock = clock;
        this.started = clock.GetTimestamp();
        this.scopes = scopes;
        slot?.Record(CacheObservationKind.TransferAcquired);
    }

    /// <summary>Starts enabled intrinsic and configured observations. The disabled path returns null without per-transfer state.</summary>
    /// <param name="collection">The declared collection alias.</param>
    /// <param name="provider">The declared provider alias.</param>
    /// <param name="partial">Whether the acquisition selects a partial representation.</param>
    /// <param name="slot">The optional predeclared intrinsic slot.</param>
    /// <param name="clock">The monotonic observation clock.</param>
    /// <param name="observers">Observers resolved once with the collection handle.</param>
    /// <returns>The active session, or null when all observations are disabled.</returns>
    public static CacheTransferObservationSession Start(string collection, string provider, bool partial, CacheObservationSlot slot,
        TimeProvider clock, IReadOnlyList<ICacheTransferObserver> observers)
    {
        if (slot?.Enabled != true && (observers is null || observers.Count == 0)) { return null; }

        var context = new CacheTransferContext(collection, provider, partial);
        List<ICacheTransferObservationScope> scopes = null;
        if (observers is not null)
        {
            foreach (var observer in observers)
            {
                try
                {
                    if (observer.BeginTransfer(context) is { } scope) { (scopes ??= []).Add(scope); }
                }
                catch { /* Optional observers cannot prevent successful acquisition. */ }
            }
        }

        return slot?.Enabled == true || scopes is not null ? new(context, slot, clock, scopes) : null;
    }

    /// <summary>Dispatches the actual terminal outcome once and isolates every optional callback.</summary>
    /// <param name="completion">The winning source terminal result.</param>
    /// <param name="bytesDelivered">The source bytes delivered before the winning result.</param>
    public void Complete(Result<CacheReadCompletion> completion, long bytesDelivered)
    {
        if (Interlocked.Exchange(ref this.completed, 1) != 0) { return; }

        var elapsed = this.clock.GetElapsedTime(this.started);
        var abandoned = completion.IsSuccess && completion.Value == CacheReadCompletion.Incomplete ||
            completion.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.Incomplete);
        this.slot?.Record(abandoned ? CacheObservationKind.TransferAbandoned : completion.IsSuccess ? CacheObservationKind.TransferCompleted : CacheObservationKind.TransferFailed);
        this.slot?.RecordDuration(CacheLatencyKind.Transfer, elapsed);
        if (completion.IsFailure && completion.Errors.OfType<CacheError>().Any(error => error.Code == CacheErrorCode.Corruption))
        {
            this.slot?.Record(CacheObservationKind.Corruption);
        }

        if (this.scopes is null) { return; }

        var observation = new CacheTransferObservation(this.context, completion, bytesDelivered, elapsed);
        foreach (var scope in this.scopes)
        {
            try { scope.Complete(observation); }
            catch { /* The source terminal outcome has already been decided. */ }
        }
    }
}