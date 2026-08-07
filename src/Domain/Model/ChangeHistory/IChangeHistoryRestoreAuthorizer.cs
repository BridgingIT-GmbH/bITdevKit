// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Authorizes ChangeHistory restore operations before mutation occurs.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <example>
/// <code>
/// public sealed class CustomerRestoreAuthorizer : IChangeHistoryRestoreAuthorizer&lt;Customer&gt;
/// {
///     public Task&lt;Result&gt; AuthorizeAsync(Customer entity, ChangeHistoryRestoreAuthorizationContext context, CancellationToken cancellationToken)
///         =&gt; Task.FromResult(Result.Success());
/// }
/// </code>
/// </example>
public interface IChangeHistoryRestoreAuthorizer<in TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Authorizes a restore request.
    /// </summary>
    /// <param name="entity">The entity being restored.</param>
    /// <param name="context">The restore authorization context.</param>
    /// <param name="cancellationToken">A token to observe while authorizing.</param>
    /// <returns>A successful result when restore is authorized.</returns>
    Task<Result> AuthorizeAsync(TEntity entity, ChangeHistoryRestoreAuthorizationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a restore authorization request.
/// </summary>
/// <example>
/// <code>
/// var context = new ChangeHistoryRestoreAuthorizationContext(changeSetId, "Undo typo");
/// </code>
/// </example>
public sealed record ChangeHistoryRestoreAuthorizationContext(Guid ChangeSetId, string Reason);
