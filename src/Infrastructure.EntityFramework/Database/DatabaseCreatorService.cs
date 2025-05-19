// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Xml.Linq;
using Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;

public class DatabaseCreatorService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseCreatorService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IDatabaseReadyService databaseReadyService;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly DatabaseCreatorOptions options;

    public DatabaseCreatorService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        IDatabaseReadyService databaseReadyService = null,
        DatabaseCreatorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseCreatorService<TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<DatabaseCreatorService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.databaseReadyService = databaseReadyService;
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new DatabaseCreatorOptions();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        var contextName = typeof(TContext).Name;

        var registration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogDebug("{LogKey} database creator startup delayed (context={DbContextType})", Constants.LogKey, contextName);

                    await Task.Delay(this.options.StartupDelay, cancellationToken);
                }

                try
                {
                    using var scope = this.serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<TContext>();

                    if (this.options.LogModel)
                    {
                        this.logger.LogDebug(context.Model.ToDebugString());
                    }

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
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.SqlServer.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables), cancellationToken).AnyContext();
                            }
                            else if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                            {
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.Sqlite.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables),
                                        cancellationToken).AnyContext();
                            }
                            else
                            {
                                throw new ArgumentException(
                                    $"Database provider '{context.Database.ProviderName}' does not supported truncating tables.");
                            }
                        }

                        this.logger.LogDebug("{LogKey} database creator create tables (context={DbContextType})", Constants.LogKey, contextName);
                        //await context.Database.EnsureCreatedAsync(cancellationToken).AnyContext();

                        // alternative way for EnsureCreatedAsync
                        // also creates tables of additional DbContexts which are housed in the same database
                        var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                        if (!exists) // only create tabled if db does not exist
                        {
                            this.logger.LogDebug("{LogKey} database creator create database (context={DbContextType})", Constants.LogKey, contextName);
                            await databaseCreator?.CreateAsync(cancellationToken);
                            exists = true;

                            await databaseCreator?.CreateTablesAsync(cancellationToken);
                        }

                        this.databaseReadyService.SetReady(contextName);
                        this.logger.LogInformation("{LogKey} database creator finished (context={DbContextType}, dbexists({DbExists}), provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, exists, context.Database.ProviderName);
                    }
                }
                catch (Exception ex)
                {
                    this.databaseReadyService.SetFaulted(contextName, ex.Message);
                    this.logger.LogError(ex, "{LogKey} database creator failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, contextName);
                }
            }, cancellationToken);
        });

        return Task.CompletedTask;
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