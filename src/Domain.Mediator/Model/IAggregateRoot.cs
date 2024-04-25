// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Text.Json.Serialization;

/// <summary>
/// <para>
/// An aggregate root is an entity which works as an entry point to our aggregate.
/// All business operations + domain events should go through the root. This way, the aggregate root
/// can take care of keeping the aggregate in a consistent state.
/// </para>
/// <para>
///
///    Entity{TId}
///   .--------------.           IAggregateRoot
///   | - Id         |          .------------------------.
///   |              |          | -DomainEvents          |
///   .--------------.          |                        |
///              /`\            .------------------------.
///               | inherits          /`\
///               |                    | implements
///        AggregateRoot{TId}         /
///       .------------------.       /
///       |                  |______/
///       |                  |
///       |                  |
///       .------------------.
///
/// </para>
/// </summary>
public interface IAggregateRoot : IEntity
{
    [JsonIgnore]
    DomainEvents DomainEvents { get; }
}