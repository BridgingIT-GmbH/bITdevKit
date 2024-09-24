// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using Repositories;

public interface IOutboxMessageWriterRepository : IRepository
{
    Task<OutboxMessage> InsertAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

    Task<OutboxMessage> FindOneAsync(
        object id,
        IFindOptions<OutboxMessage> options = null,
        CancellationToken cancellationToken = default);
}