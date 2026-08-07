// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Applies a ChangeHistory restore value through module-owned domain logic.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// <code>
/// public sealed class CustomerNameRestoreHandler : IChangeHistoryRestoreHandler&lt;Customer&gt;
/// {
///     public Task&lt;Result&gt; RestoreAsync(Customer entity, ChangeHistoryRestoreContext context, CancellationToken cancellationToken)
///         =&gt; Task.FromResult(entity.ChangeName((string)context.Value));
/// }
/// </code>
/// </example>
public interface IChangeHistoryRestoreHandler<in TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Restores one property/path value on the supplied entity.
    /// </summary>
    /// <param name="entity">The entity to mutate.</param>
    /// <param name="context">The restore context.</param>
    /// <param name="cancellationToken">A token to observe while restoring.</param>
    /// <returns>A result that indicates whether the domain restore succeeded.</returns>
    Task<Result> RestoreAsync(TEntity entity, ChangeHistoryRestoreContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes one value being restored from ChangeHistory.
/// </summary>
/// <example>
/// <code>
/// var context = new ChangeHistoryRestoreContext("FirstName", "Alice", typeof(string), changeSetId, "Support correction");
/// </code>
/// </example>
public sealed record ChangeHistoryRestoreContext(
    string PropertyName,
    object Value,
    Type ValueType,
    Guid ChangeSetId,
    string Reason);
