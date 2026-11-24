// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Guards a single behavior invocation so each ordered stage and the underlying operation can execute at most once.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var invocation = new CacheBehaviorInvocation&lt;string&gt;(operation); await invocation.ContinueAsync(token);</code></example>
public sealed class CacheBehaviorInvocation<T>(Func<CancellationToken, ValueTask<Result<T>>> operation)
{
    private readonly Lock gate = new();
    private bool closed;
    private long visited;
    private Task<Result<T>> actual;

    /// <summary>Gets the actual underlying operation task, or null before a continuation starts it.</summary>
    public Task<Result<T>> Actual
    {
        get { lock (this.gate) { return this.actual; } }
    }

    /// <summary>Atomically admits an unvisited behavior stage while the invocation remains open.</summary>
    public bool TryEnterStage(int index)
    {
        lock (this.gate)
        {
            var mask = 1L << index;
            if (this.closed || (this.visited & mask) != 0)
            {
                return false;
            }

            this.visited |= mask;
            return true;
        }
    }

    /// <summary>Starts the underlying operation exactly once, returning a contract failure after repeated or sealed invocation.</summary>
    public ValueTask<Result<T>> ContinueAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (this.closed || this.actual is not null)
            {
                return ValueTask.FromResult(Result<T>.Failure(new CacheError(CacheErrorCode.Contract,
                    "A public cache operation continuation can execute only once.")));
            }

            this.actual = ExecuteAsync(operation, cancellationToken);
            return new(this.actual);
        }
    }

    /// <summary>Prevents subsequent continuation calls and returns the operation already started, if any.</summary>
    public Task<Result<T>> Seal()
    {
        lock (this.gate)
        {
            this.closed = true;
            return this.actual;
        }
    }

    private static async Task<Result<T>> ExecuteAsync(Func<CancellationToken, ValueTask<Result<T>>> next, CancellationToken cancellationToken) =>
        await next(cancellationToken).ConfigureAwait(false);
}