// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

internal sealed class TestEntityBulkInsertProvider(string providerName, long result)
    : IEntityBulkInsertProvider
{
    public string ProviderName { get; } = providerName;

    public long Result { get; set; } = result;

    public bool IsSupported { get; set; } = true;

    public string UnsupportedReason { get; set; }

    public int CallCount { get; private set; }

    public DbContext LastContext { get; private set; }

    public int LastEntityCount { get; private set; }

    public bool LastHadActiveTransaction { get; private set; }

    public Action<DbContext> OnInsert { get; set; }

    public Exception ExceptionToThrow { get; set; }

    public Task<long> InsertAsync<TEntity>(
        DbContext context,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken = default
    )
        where TEntity : class
    {
        this.CallCount++;
        this.LastContext = context;
        this.LastEntityCount = batch.Entities.Count;
        this.LastHadActiveTransaction = context.Database.CurrentTransaction is not null;
        this.OnInsert?.Invoke(context);

        if (this.ExceptionToThrow is not null)
        {
            throw this.ExceptionToThrow;
        }

        return Task.FromResult(this.Result);
    }
}
