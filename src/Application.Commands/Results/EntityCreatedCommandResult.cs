// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class EntityCreatedCommandResult
{
    public EntityCreatedCommandResult(string entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The entity id
    /// </summary>
    public string EntityId { get; }
}

public class EntityCreatedCommandResult<TId>
{
    public EntityCreatedCommandResult(TId entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The entity id
    /// </summary>
    public TId EntityId { get; }
}
