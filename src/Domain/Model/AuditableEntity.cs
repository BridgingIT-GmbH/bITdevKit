// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;

[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
{
    public AuditState AuditState { get; set; } = new();
}

[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class AuditableEntity<TId, TIdType> : AuditableEntity<TId>
    where TId : EntityId<TIdType>
{
    public new EntityId<TIdType> Id { get; set; }
}