// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;
using Newtonsoft.Json;

/// <summary>
///     <para>
///         An aggregate root is an entity which works as an entry point to our aggregate.
///         All business operations + domain events should go through the root. This way, the aggregate root
///         can take care of keeping the aggregate in a consistent state.
///     </para>
///     <para>
///         Entity{TId}
///         .--------------.           IAggregateRoot
///         | - Id         |          .------------------------.
///         |              |          | -DomainEvents          |
///         .--------------.          |                        |
///         /`\            .------------------------.
///         | inherits          /`\
///         |                    | implements
///         AggregateRoot{TId}         /
///         .------------------.       /
///         |                  |______/
///         |                  |
///         |                  |
///         .------------------.
///     </para>
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
{
    /// <summary>
    ///     Represents the domain events associated with the aggregate root.
    /// </summary>
    /// <remarks>
    ///     DomainEvents is used to register and manage events that occur within the lifecycle
    ///     of the aggregate root. It's accessible within the aggregate root to enable raising
    ///     events in response to state changes. Events registered in DomainEvents can trigger
    ///     subsequent actions or updates in other parts of the system.
    /// </remarks>
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DomainEvents DomainEvents { get; } = new();
}

[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AggregateRoot<TId, TIdType> : AggregateRoot<TId>, IEntity
    where TId : AggregateRootId<TIdType>
{
    private TId id;

    public new TId Id
    {
        get => this.id;
        set => this.id = value;
    }

    object IEntity.Id
    {
        get => this.id;
        set => this.id = (TId)value;
    }
}