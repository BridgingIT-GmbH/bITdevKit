// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Model;

public class NullRepositoryTransaction<TEntity> : IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    public async Task ExecuteScopedAsync(Func<Task> action)
    {
        await action();
    }

    public async Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action)
    {
        return await action();
    }
}