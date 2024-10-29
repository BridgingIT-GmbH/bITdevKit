// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

public class EntityFrameworkRepository<TEntity> : IRepository
    where TEntity : class
{
    protected EntityFrameworkRepository(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.Logger = options.CreateLogger<IRepository>();

        try
        {
            var connectionString = this.Options.DbContext.Database.GetDbConnection().ConnectionString;
            if (connectionString.Equals("DataSource=:memory:", StringComparison.OrdinalIgnoreCase))
            {
                // needed for sqlite inmemory
                this.Options.DbContext.Database.OpenConnection();
                this.Options.DbContext.Database.EnsureCreated();
            }
        }
        catch (InvalidOperationException)
        {
            // not possible for DbContext with UseInMemoryDatabase enabled (options)
            // 'Relational-specific methods can only be used when the context is using a relational database provider.'
        }
    }

    protected EntityFrameworkRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    protected EntityFrameworkRepositoryOptions Options { get; }

    protected ILogger<IRepository> Logger { get; }

    protected DbSet<TEntity> GetDbSet()
    {
        return this.Options.DbContext.Set<TEntity>();
    }

    protected IDbConnection GetDbConnection()
    {
        return this.Options.DbContext.Database.GetDbConnection();
    }

    protected IDbTransaction GetDbTransaction()
    {
        return this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();
    }
}