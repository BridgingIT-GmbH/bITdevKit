// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Model;

public interface IDatabaseTransaction
{
    Task ExecuteScopedAsync(Func<Task> action);

    [Obsolete("Please use ExecuteScopedAsync from now on")]
    Task<TEntity> ExecuteScopedWithResultAsync<TEntity>(Func<Task<TEntity>> action)
        where TEntity : class, IEntity;

    Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action)
    where TEntity : class, IEntity;
}