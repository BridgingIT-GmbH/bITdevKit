﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox;

using Domain.Outbox;
using Models;
using Repositories;

public class OutboxMessageWriterRepository
    : EntityFrameworkGenericRepository<OutboxMessage, Outbox>, IOutboxMessageWriterRepository
{
    public OutboxMessageWriterRepository(EntityFrameworkRepositoryOptions options)
        : base(options)
    {
        options.Autosave = true;
    }
}