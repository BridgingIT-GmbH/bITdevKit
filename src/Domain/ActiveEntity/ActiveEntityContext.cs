// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a context for Active Entity operations, bundling the provider and behaviors
/// resolved from the same DI scope. This ensures that the provider and behaviors share
/// the same lifetime and dependencies (e.g., DbContext).
///
/// <para>
/// <b>Lifetime Warning:</b> This context is only valid within the DI scope it was created from.
/// Do not cache or pass it across async boundaries outside of that scope, as it may lead to
/// <see cref="ObjectDisposedException"/> if the underlying scope (and its services) are disposed.
/// Always use it immediately within the creating method (e.g., inside <see cref="ActiveEntityContextScope.UseAsync{TEntity, TId, TResult}"/>
/// or <see cref="WithTransactionAsync{TEntity, TId}"/>).
/// </para>
///
/// <para>
/// If you need to extend the context with more dependencies, resolve them at creation time
/// and add them as properties here, rather than resolving them later to avoid scope mismatches.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The entity type handled by the context.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ActiveEntityContext{TEntity, TId}"/> class.
/// </remarks>
/// <param name="provider">The provider for CRUD operations (must not be null).</param>
/// <param name="behaviors">The behaviors for lifecycle hooks (must not be null).</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="provider"/> or <paramref name="behaviors"/> is null.</exception>
public class ActiveEntityContext<TEntity, TId>(
    IActiveEntityEntityProvider<TEntity, TId> provider,
    IEnumerable<IActiveEntityBehavior<TEntity>> behaviors)
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Gets the provider used for CRUD operations and transactions.
    /// This provider is bound to the DI scope the context was created from.
    /// </summary>
    public IActiveEntityEntityProvider<TEntity, TId> Provider { get; } = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Gets the collection of behaviors (lifecycle hooks) resolved from the same DI scope as the provider.
    /// These behaviors are executed before/after CRUD operations.
    /// </summary>
    public IReadOnlyCollection<IActiveEntityBehavior<TEntity>> Behaviors { get; } = behaviors?.ToList() ?? throw new ArgumentNullException(nameof(behaviors));
}
