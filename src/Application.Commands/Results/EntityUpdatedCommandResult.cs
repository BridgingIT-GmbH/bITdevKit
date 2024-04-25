// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class EntityUpdatedCommandResult
{
    public EntityUpdatedCommandResult(string entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The entity id
    /// </summary>
    public string EntityId { get; }
}

public class EntityUpdatedCommandResult<TId>
{
    public EntityUpdatedCommandResult(TId entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The entity id
    /// </summary>
    public TId EntityId { get; }
}
