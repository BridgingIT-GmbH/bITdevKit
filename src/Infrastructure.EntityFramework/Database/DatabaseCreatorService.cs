// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class DatabaseCreatorService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseCreatorService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly DatabaseCreatorOptions options;

    public DatabaseCreatorService(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        DatabaseCreatorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseCreatorService<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<DatabaseCreatorService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.options = options ?? new DatabaseCreatorOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var contextName = typeof(TContext).Name;

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} database creator startup delayed (context={DbContextType})", Constants.LogKey, contextName);

            await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
        }

        try
        {
            //this.logger.LogDebug("{LogKey} database creator initializing (context={DbContextType})", Constants.LogKey, contextName);
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (!this.IsInMemoryContext(context))
            {
                this.logger.LogDebug("{LogKey} database creator started (context={DbContextType}, provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, context.Database.ProviderName);

                var exists = await context.Database.CanConnectAsync(cancellationToken);
                if (exists && this.options.EnsureDeleted)
                {
                    this.logger.LogDebug("{LogKey} database creator delete tables (context={DbContextType})", Constants.LogKey, contextName);
                    await context.Database.EnsureDeletedAsync(cancellationToken).AnyContext();
                    exists = false;
                }

                if (exists && this.options.EnsureTruncated)
                {
                    this.logger.LogDebug("{LogKey} database creator truncate tables (context={DbContextType})", Constants.LogKey, contextName);
                    if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            SqlStatements.SqlServer.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables), cancellationToken).AnyContext();
                    }
                    else if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            SqlStatements.Sqlite.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables), cancellationToken).AnyContext();
                    }
                    else
                    {
                        throw new ArgumentException($"Database provider '{context.Database.ProviderName}' does not supported truncating tables.");
                    }
                }

                this.logger.LogDebug("{LogKey} database creator create tables (context={DbContextType})", Constants.LogKey, contextName);
                //await context.Database.EnsureCreatedAsync(cancellationToken).AnyContext();

                // alternative way for EnsureCreatedAsync
                // also creates tables of additional DbContexts which are housed in the same database
                var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                if (!exists)
                {
                    this.logger.LogDebug("{LogKey} database creator create database (context={DbContextType})", Constants.LogKey, contextName);
                    await databaseCreator?.CreateAsync(cancellationToken);
                    exists = true;
                }

                await databaseCreator?.CreateTablesAsync(cancellationToken);

                this.logger.LogInformation("{LogKey} database creator finished (context={DbContextType}, dbexists({DbExists}), provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, exists, context.Database.ProviderName);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} database creator failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, contextName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private bool IsInMemoryContext(TContext context)
    {
        return context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    }
}