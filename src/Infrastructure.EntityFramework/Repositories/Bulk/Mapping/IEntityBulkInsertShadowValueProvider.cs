// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Supplies deterministic values for writable EF shadow properties during native bulk insertion.
/// </summary>
/// <typeparam name="TEntity">The inserted entity type.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Person, AppDbContext&gt;()
///     .WithShadowValueProvider&lt;TenantShadowValueProvider&gt;();
/// </code>
/// </example>
public interface IEntityBulkInsertShadowValueProvider<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Attempts to provide a deterministic provider value for one shadow property.</summary>
    /// <param name="context">The entity, EF property, and DbContext.</param>
    /// <param name="value">The value when this provider owns the property.</param>
    /// <returns><see langword="true"/> when a value was supplied.</returns>
    bool TryGetValue(EntityBulkInsertShadowPropertyContext<TEntity> context, out object value);
}
