﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public interface IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default);

    Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken = default);
}