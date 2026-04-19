// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox;

using Domain.Outbox;
using Models;
using Repositories;

/// <summary>
/// Persists new outbox messages for the Entity Framework outbox module.
/// </summary>
public class OutboxMessageWriterRepository
    : EntityFrameworkGenericRepository<OutboxMessage, Outbox>, IOutboxMessageWriterRepository
{
    /// <summary>
    /// Initializes a new repository instance and enables automatic persistence.
    /// </summary>
    /// <param name="options">The repository options.</param>
    public OutboxMessageWriterRepository(EntityFrameworkRepositoryOptions options)
        : base(options)
    {
        options.Autosave = true;
    }
}
