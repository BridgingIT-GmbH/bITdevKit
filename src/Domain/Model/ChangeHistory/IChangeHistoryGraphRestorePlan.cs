// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Restores a configured graph path through module-owned domain logic.
/// </summary>
/// <typeparam name="TEntity">The aggregate/entity type.</typeparam>
/// <example>
/// <code>
/// public sealed class CustomerOrdersRestorePlan : IChangeHistoryGraphRestorePlan&lt;Customer&gt;
/// {
///     public Task&lt;Result&gt; RestoreAsync(Customer entity, IReadOnlyList&lt;ChangeHistoryGraphRestoreValue&gt; values, CancellationToken cancellationToken)
///         =&gt; Task.FromResult(Result.Success());
/// }
/// </code>
/// </example>
public interface IChangeHistoryGraphRestorePlan<in TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Restores graph values for a configured graph path.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    /// <param name="values">The graph values to restore.</param>
    /// <param name="cancellationToken">A token to observe while restoring.</param>
    /// <returns>A result indicating whether graph restore succeeded.</returns>
    Task<Result> RestoreAsync(TEntity entity, IReadOnlyList<ChangeHistoryGraphRestoreValue> values, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes one graph value passed to a graph restore plan.
/// </summary>
/// <example>
/// <code>
/// var value = new ChangeHistoryGraphRestoreValue("Orders[1].Items[2].Quantity", 1, typeof(int));
/// </code>
/// </example>
public sealed record ChangeHistoryGraphRestoreValue(string PropertyPath, object Value, Type ValueType);
