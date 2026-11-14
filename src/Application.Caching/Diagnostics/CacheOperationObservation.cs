// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Observes logical public outcomes once, independently of internal provider rechecks and owned source attempts.</summary>
/// <example><code>var started = CacheOperationObservation.Begin(slot, clock);</code></example>
public static class CacheOperationObservation
{
    /// <summary>Records a public method invocation and captures its monotonic start timestamp.</summary>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The monotonic observation clock.</param>
    /// <param name="key">An optional logical key recorded only as an explicitly enabled process-local keyed fingerprint.</param>
    /// <param name="operation">The controlled public operation category.</param>
    /// <returns>The invocation start timestamp.</returns>
    public static long Begin(CacheObservationSlot slot, TimeProvider clock, string key = null, CacheOperationKind operation = CacheOperationKind.Get)
    {
        slot.Record(CacheObservationKind.Request);
        slot.RecordKey(key, operation);
        return clock.GetTimestamp();
    }

    /// <summary>Observes one typed/binary logical lookup without boxing its successful value.</summary>
    /// <typeparam name="T">The declared value or acquired handle type.</typeparam>
    /// <param name="started">The public invocation start.</param>
    /// <param name="pending">The actual mandatory operation.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <param name="operation">The public operation category.</param>
    /// <returns>The unchanged actual operation result.</returns>
    public static ValueTask<Result<CacheLookup<T>>> Lookup<T>(long started, ValueTask<Result<CacheLookup<T>>> pending,
        CacheObservationSlot slot, TimeProvider clock, CacheOperationKind operation)
    {
        if (!pending.IsCompletedSuccessfully) { return AwaitLookupAsync(started, pending, slot, clock, operation); }

        var result = pending.Result;
        FinishLookup(result, slot, operation);
        slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started));
        return ValueTask.FromResult(result);
    }

    /// <summary>Observes batch invocation throughput separately from successfully read entry counts.</summary>
    /// <typeparam name="T">The declared value or handle type.</typeparam>
    /// <param name="started">The invocation start.</param>
    /// <param name="pending">The ordered actual item outcomes.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <param name="operation">The public batch category.</param>
    /// <returns>The unchanged actual batch result.</returns>
    public static async ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> Batch<T>(long started,
        ValueTask<Result<IReadOnlyList<Result<CacheLookup<T>>>>> pending, CacheObservationSlot slot, TimeProvider clock, CacheOperationKind operation)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.IsSuccess)
            {
                foreach (var item in result.Value) { FinishLookup(item, slot, operation); }
            }
            else { Failure(result, slot); }

            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    /// <summary>Records confirmed entry publication separately from method invocation.</summary>
    /// <param name="started">The invocation start.</param>
    /// <param name="pending">The actual storage outcome.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <returns>The unchanged storage result.</returns>
    public static async ValueTask<Result<CacheWriteOutcome>> Write(long started, ValueTask<Result<CacheWriteOutcome>> pending, CacheObservationSlot slot, TimeProvider clock)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.IsFailure) { Failure(result, slot); }

            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    /// <summary>Records actual committed entry counts and bounded invalidation-unit outcomes.</summary>
    /// <param name="started">The invocation start.</param>
    /// <param name="pending">The actual bounded invalidation result.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <returns>The unchanged invalidation result.</returns>
    public static async ValueTask<Result<CacheInvalidationOutcome>> Invalidation(long started, ValueTask<Result<CacheInvalidationOutcome>> pending,
        CacheObservationSlot slot, TimeProvider clock)
    {
        slot.Record(CacheObservationKind.InvalidationRequested);
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.IsSuccess)
            {
                slot.Record(CacheObservationKind.InvalidatedEntries, result.Value.AffectedCount);
                slot.Record(CacheObservationKind.InvalidationCompleted);
            }
            else { slot.Record(CacheObservationKind.InvalidationFailed); Failure(result, slot); }

            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    /// <summary>Observes a bounded non-payload result without inventing entry counts for metadata previews.</summary>
    /// <typeparam name="T">The actual operation response.</typeparam>
    /// <param name="started">The invocation start.</param>
    /// <param name="pending">The mandatory response.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <returns>The unchanged response.</returns>
    public static async ValueTask<Result<T>> Plain<T>(long started, ValueTask<Result<T>> pending, CacheObservationSlot slot, TimeProvider clock)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.IsFailure) { Failure(result, slot); }

            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    /// <summary>Observes a result-only action while preserving its exact failure and cancellation semantics.</summary>
    /// <param name="started">The invocation start.</param>
    /// <param name="pending">The mandatory action.</param>
    /// <param name="slot">The predeclared collection slot.</param>
    /// <param name="clock">The observation clock.</param>
    /// <returns>The unchanged action result.</returns>
    public static async ValueTask<Result> Action(long started, ValueTask<Result> pending, CacheObservationSlot slot, TimeProvider clock)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            if (result.IsFailure) { Failure(result, slot); }

            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    private static async ValueTask<Result<CacheLookup<T>>> AwaitLookupAsync<T>(long started, ValueTask<Result<CacheLookup<T>>> pending,
        CacheObservationSlot slot, TimeProvider clock, CacheOperationKind operation)
    {
        try
        {
            var result = await pending.ConfigureAwait(false);
            FinishLookup(result, slot, operation);
            return result;
        }
        finally { slot.RecordDuration(CacheLatencyKind.Operation, clock.GetElapsedTime(started)); }
    }

    private static void FinishLookup<T>(Result<CacheLookup<T>> result, CacheObservationSlot slot, CacheOperationKind operation)
    {
        if (result.IsFailure) { Failure(result, slot); return; }

        var lookup = result.Value;
        if (lookup.Found) { slot.Record(CacheObservationKind.ReadSuccess); }

        if (lookup.LookupOutcome == CacheLookupOutcome.Hit)
        {
            slot.Record(lookup.IsNotFound ? CacheObservationKind.NegativeHit : CacheObservationKind.PositiveHit);
        }
        else if (lookup.LookupOutcome != CacheLookupOutcome.Bypassed && operation != CacheOperationKind.Refresh)
        {
            slot.Record(CacheObservationKind.Miss);
        }
    }

    private static void Failure(IResult result, CacheObservationSlot slot)
    {
        var code = result.Errors.OfType<CacheError>().FirstOrDefault()?.Code;
        var kind = code switch
        {
            CacheErrorCode.Capacity => CacheObservationKind.AdmissionRejected,
            CacheErrorCode.Corruption => CacheObservationKind.Corruption,
            CacheErrorCode.Compatibility => CacheObservationKind.CompatibilityFailure,
            _ => (CacheObservationKind?)null
        };
        if (kind is { } observed) { slot.Record(observed); }
    }
}