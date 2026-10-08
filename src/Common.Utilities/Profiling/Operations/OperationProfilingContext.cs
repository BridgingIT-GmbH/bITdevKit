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
    public OperationProfilingFrame Current { get => this.frame.Value; set => this.frame.Value = value; }

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

internal interface IOperationProfilingCompletionSink
{
    bool TryEnqueue(OperationProfilingRecord record);
}

internal sealed class OperationProfilingCaptureCounters
{
    public long Attempted;
    public long Admitted;
    public long Rejected;
    public long Completed;
    public long CaptureFaults;
    public long CompletionRejected;
    public long Expired;
    public long LateObservations;
    public long LoggingFaults;
}
