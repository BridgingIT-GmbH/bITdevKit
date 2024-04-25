// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

public interface IEntity
{
    /// <summary>
    /// Gets the identifier value.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    object Id { get; set; }
}

public interface IEntity<TId> : IEntity
{
    /// <summary>
    /// Gets or sets the entity id.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The id may be of type <c>string</c>, <c>int</c>, or another value type.
    ///     </para>
    /// </remarks>
    new TId Id { get; set; }
}