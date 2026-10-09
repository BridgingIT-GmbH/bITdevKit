// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>Provides an optional execution wrapper around a complete Active Entity operation, including its lifecycle hooks.</summary>
/// <typeparam name="TEntity">The entity type handled by the behavior.</typeparam>
/// <example><code>builder.WithBehavior&lt;ActiveEntityProfilingBehavior&lt;Order&gt;&gt;();</code></example>
public interface IActiveEntityOperationBehavior<TEntity> : IActiveEntityBehavior<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Executes the next operation boundary exactly once and returns its result.</summary>
    /// <typeparam name="TResult">The result type returned by the operation.</typeparam>
    /// <param name="operation">The calling Active Entity operation name.</param>
    /// <param name="next">The remaining operation and its before/after hooks.</param>
    /// <param name="cancellationToken">The application's cancellation token.</param>
    /// <returns>The unmodified business result.</returns>
    /// <example><code>var result = await behavior.ExecuteAsync("InsertAsync", () =&gt; entity.InsertAsync(context), cancellationToken);</code></example>
    Task<TResult> ExecuteAsync<TResult>(
        string operation,
        Func<Task<TResult>> next,
        CancellationToken cancellationToken = default
    );
}
