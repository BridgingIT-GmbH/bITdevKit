// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;

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
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
{
    protected AggregateRoot()
    {
        this.DomainEvents = new DomainEvents();
    }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DomainEvents DomainEvents { get; }
}

[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AggregateRoot<TId, TIdType> : AggregateRoot<TId>, IEntity
    where TId : AggregateRootId<TIdType>
{
    public new AggregateRootId<TIdType> Id { get; set; }

    object IEntity.Id
    {
        get { return this.Id; }
        set { this.Id = (TId)value; }
    }
}