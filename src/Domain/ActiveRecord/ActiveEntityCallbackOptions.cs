// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Options class to hold all before/after callback functions
/// </summary>
public class ActiveEntityCallbackOptions<TEntity, TId>
    where TEntity : class, IEntity
{
    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> BeforeInsertAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> AfterInsertAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> BeforeUpdateAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> AfterUpdateAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> BeforeUpsertAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> AfterUpsertAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> BeforeDeleteAsync { get; set; }

    public Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> AfterDeleteAsync { get; set; }
}