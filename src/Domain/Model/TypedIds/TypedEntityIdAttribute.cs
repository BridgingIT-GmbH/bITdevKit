// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     An attribute that specifies the TypedId for an Entity. This Attributes
///     is used for the code generation of TypedId instances.
/// </summary>
/// <typeparam name="TId">The type of the identifier.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TypedEntityIdAttribute<TId> : Attribute { }