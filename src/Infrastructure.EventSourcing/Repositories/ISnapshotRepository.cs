// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.Repositories;

public interface ISnapshotRepository : IRepository
{
    Task<byte[]> GetSnapshotAsync(Guid aggregateId, string immutableName, CancellationToken cancellationToken);

    Task SaveSnapshotAsync(Guid aggregateId, byte[] blob, string immutableName, CancellationToken cancellationToken);
}