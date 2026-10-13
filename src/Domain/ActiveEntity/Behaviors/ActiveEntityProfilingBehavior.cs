// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;

/// <summary>Profiles complete Active Entity operations, joining an existing operation or owning an independent operation.</summary>
/// <typeparam name="TEntity">The entity type handled by the behavior.</typeparam>
/// <param name="profiling">The optional profiler; omission preserves ordinary Active Entity execution.</param>
/// <example><code>builder.WithBehavior&lt;ActiveEntityProfilingBehavior&lt;Order&gt;&gt;();</code></example>
public sealed class ActiveEntityProfilingBehavior<TEntity>(IOperationProfiler profiling = null)
    : ActiveEntityBehaviorBase<TEntity>,
        IActiveEntityOperationBehavior<TEntity>
    where TEntity : class, IEntity
{
    private static readonly string entityType = typeof(TEntity).FullName ?? typeof(TEntity).Name;
    private static readonly string keyPrefix = "activeentity:" + typeof(TEntity).Name + ":";

    /// <inheritdoc />
    /// <example><code>var result = await behavior.ExecuteAsync("InsertAsync", next, cancellationToken);</code></example>
    public Task<TResult> ExecuteAsync<TResult>(
        string operation,
        Func<Task<TResult>> next,
        CancellationToken cancellationToken = default
    )
    {
        if (profiling is null)
        {
            return next();
        }

        var name =
            operation?.EndsWith("Async", StringComparison.Ordinal) == true
                ? operation[..^5]
                : operation;
        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest { Key = keyPrefix + name, Kind = "ActiveEntity" },
            (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension("activeentity.entityType", entityType);
                        scope.SetDimension("activeentity.operation", name);
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change Active Entity execution. */
                }

                return next();
            },
            cancellationToken,
            result =>
                result is IResult businessResult
                    ? OperationProfilingHelpers.ClassifyResult(businessResult)
                    : null
        );
    }
}
