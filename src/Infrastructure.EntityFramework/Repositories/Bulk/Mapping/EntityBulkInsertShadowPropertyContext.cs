// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Identifies the entity and EF shadow property for which a deterministic value is requested.
/// </summary>
/// <typeparam name="TEntity">The inserted entity type.</typeparam>
/// <param name="Entity">The current entity.</param>
/// <param name="Property">The EF shadow property metadata.</param>
/// <param name="DbContext">The active DbContext.</param>
/// <example>
/// <code>
/// var propertyContext = new EntityBulkInsertShadowPropertyContext&lt;Person&gt;(person, property, dbContext);
/// </code>
/// </example>
public sealed record EntityBulkInsertShadowPropertyContext<TEntity>(
    TEntity Entity,
    IProperty Property,
    DbContext DbContext
)
    where TEntity : class, IEntity;
