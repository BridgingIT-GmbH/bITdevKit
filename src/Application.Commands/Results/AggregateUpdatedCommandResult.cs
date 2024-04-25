// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class AggregateUpdatedCommandResult
{
    public AggregateUpdatedCommandResult(string entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The aggregate id
    /// </summary>
    public string EntityId { get; }
}

public class AggregateUpdatedCommandResult<TId>
{
    public AggregateUpdatedCommandResult(TId entityId)
    {
        this.EntityId = entityId;
    }

    /// <summary>
    /// The aggregate id
    /// </summary>
    public TId EntityId { get; }
}
