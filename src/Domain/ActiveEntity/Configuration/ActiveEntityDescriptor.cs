// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;

/// <summary>
/// Describes an entity type that has been configured for ActiveEntity.
/// </summary>
/// <param name="EntityType">The entity type.</param>
/// <param name="IdType">The identifier type of the entity.</param>
/// <param name="IsSoftDeletable">A value indicating whether the entity implements soft deletion.</param>
/// <example>
/// <code>
/// var descriptor = new ActiveEntityDescriptor(typeof(Customer), typeof(Guid), false);
/// </code>
/// </example>
public sealed record ActiveEntityDescriptor(Type EntityType, Type IdType, bool IsSoftDeletable);
