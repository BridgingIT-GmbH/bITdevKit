// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Represents entity framework repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityFrameworkRepository<TEntity> : IRepository
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityFrameworkRepository</c> class.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
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

    /// <summary>
    /// Initializes a new instance of the <c>EntityFrameworkRepository</c> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    protected EntityFrameworkRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// Gets the options.
    /// </summary>
    protected EntityFrameworkRepositoryOptions Options { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<IRepository> Logger { get; }

    /// <summary>
    /// Gets db set.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected DbSet<TEntity> GetDbSet()
    {
        return this.Options.DbContext.Set<TEntity>();
    }

    /// <summary>
    /// Gets db connection.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected IDbConnection GetDbConnection()
    {
        return this.Options.DbContext.Database.GetDbConnection();
    }

    /// <summary>
    /// Gets db transaction.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected IDbTransaction GetDbTransaction()
    {
        return this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();
    }
}
