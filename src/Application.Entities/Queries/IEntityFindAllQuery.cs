// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Defines operations for i entity find all query.
/// </summary>
public interface IEntityFindAllQuery;

/// <summary>
/// Defines operations for i entity find all query.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityFindAllQuery<TEntity> : IEntityFindAllQuery
    where TEntity : class, IEntity;
