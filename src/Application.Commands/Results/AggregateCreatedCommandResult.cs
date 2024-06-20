// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class AggregateCreatedCommandResult(string entityId)
{
    /// <summary>
    /// The aggregate id
    /// </summary>
    public string EntityId { get; } = entityId;
}

public class AggregateCreatedCommandResult<TId>(TId entityId)
{
    /// <summary>
    /// The aggregate id
    /// </summary>
    public TId EntityId { get; } = entityId;
}
