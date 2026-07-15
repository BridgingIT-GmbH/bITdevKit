// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

internal sealed class TestEntityBulkInsertProvider(string providerName, long result) : IEntityBulkInsertProvider
{
    public string ProviderName { get; } = providerName;

    public long Result { get; } = result;

    public int CallCount { get; private set; }

    public DbContext LastContext { get; private set; }

    public int LastEntityCount { get; private set; }

    public Exception ExceptionToThrow { get; set; }

    public Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        this.CallCount++;
        this.LastContext = context;
        this.LastEntityCount = batch.Entities.Count;

        if (this.ExceptionToThrow is not null)
        {
            throw this.ExceptionToThrow;
        }

        return Task.FromResult(this.Result);
    }
}
