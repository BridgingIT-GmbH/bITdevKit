// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed record OperationProfilingFrame(
    IProfilingOperationScope Operation,
    ProfilingCaptureState Capture,
    ProfilingInvocation Invocation,
    bool Suppressed,
    OperationProfilingFrame Previous = null
);

internal sealed class OperationProfilingContext
{
    private readonly AsyncLocal<OperationProfilingFrame> frame = new();
    /// <summary>Exposes the Current profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public OperationProfilingFrame Current { get => this.frame.Value; set => this.frame.Value = value; }

    /// <summary>Exposes the Enter profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public IDisposable Enter(OperationProfilingFrame value)
    {
        var previous = this.Current;
        if (value is not null)
        {
            value = value with { Previous = previous };
        }

        this.Current = value;
        return new Restore(this, previous, value);
    }

    private sealed class Restore(OperationProfilingContext context, OperationProfilingFrame previous, OperationProfilingFrame entered) : IDisposable
    {
        private int disposed;

        /// <summary>Exposes the Dispose profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 0 && (ReferenceEquals(context.Current, entered) || entered?.Invocation is null && entered?.Operation is not null && ReferenceEquals(context.Current?.Operation, entered.Operation)))
            {
                var restore = previous;
                while (restore is not null)
                {
                    if (restore.Operation is ProfilingOperationScope { Disposed: true } operation)
                    {
                        restore = operation.Previous;
                    }
                    else if (restore.Invocation?.Closed == true)
                    {
                        restore = restore.Previous;
                    }
                    else
                    {
                        break;
                    }
                }

                context.Current = restore;
            }
        }
    }
}

/// <summary>Accepts immutable completed recordings without application-path storage calls.</summary>
/// <example>Used by the shared profiling writer and its node-local health observations.</example>
public interface IOperationProfilingCompletionSink
{
    /// <summary>Attempts nonblocking acceptance of an immutable completed root.</summary>
    /// <example><code>var accepted = sink.TryEnqueue(record);</code></example>
    bool TryEnqueue(OperationProfilingRecord record);
}

internal sealed class OperationProfilingCaptureCounters
{
    /// <summary>Exposes the Attempted profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Attempted;
    /// <summary>Exposes the Admitted profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Admitted;
    /// <summary>Exposes the Rejected profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Rejected;
    /// <summary>Exposes the Completed profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Completed;
    /// <summary>Exposes the CaptureFaults profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long CaptureFaults;
    /// <summary>Counts admitted captures discarded after an inconsistent observation or finalization fault.</summary>
    /// <example>Included once in node-local diagnostic loss, separately from queue rejection.</example>
    public long DiscardedCaptures;
    /// <summary>Exposes the CompletionRejected profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long CompletionRejected;
    /// <summary>Exposes the Expired profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Expired;
    /// <summary>Exposes the LateObservations profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long LateObservations;
    /// <summary>Exposes the LoggingFaults profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long LoggingFaults;
}
