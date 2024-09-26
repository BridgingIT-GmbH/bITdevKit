﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Common;
using Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

public class DatabaseMigratorService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseMigratorService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly DatabaseMigratorOptions options;

    public DatabaseMigratorService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        DatabaseMigratorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseMigratorService<TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<DatabaseMigratorService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.applicationLifetime = applicationLifetime;
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new DatabaseMigratorOptions();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        var contextName = typeof(TContext).Name;

        _ = Task.Run(async () =>
            {
                // Wait "indefinitely", until ApplicationStarted is triggered
                await Task.Delay(Timeout.InfiniteTimeSpan, this.applicationLifetime.ApplicationStarted)
                    .ContinueWith(_ =>
                        {
                            this.logger.LogDebug("{LogKey} database migrator - application started", Constants.LogKey);
                        },
                        TaskContinuationOptions.OnlyOnCanceled)
                    .ConfigureAwait(false);

                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogDebug("{LogKey} database migrator startup delayed (context={DbContextType})",
                        Constants.LogKey,
                        contextName);

                    await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
                }

                try
                {
                    //this.logger.LogDebug("{LogKey} database migrator initializing (context={DbContextType})", Constants.LogKey, contextName);
                    using var scope = this.serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<TContext>();

                    if (this.options.LogModel)
                    {
                        // https://learn.microsoft.com/en-us/ef/core/modeling/#debug-view
                        this.logger.LogDebug(context.Model.ToDebugString());
                    }

                    if (!IsInMemoryContext(context))
                    {
                        this.logger.LogDebug(
                            "{LogKey} database migrator started (context={DbContextType}, provider={EntityFrameworkCoreProvider})",
                            Constants.LogKey,
                            contextName,
                            context.Database.ProviderName);

                        var exists = await context.Database.CanConnectAsync(cancellationToken);
                        if (exists && this.options.EnsureDeleted)
                        {
                            this.logger.LogDebug("{LogKey} database migrator delete tables (context={DbContextType})",
                                Constants.LogKey,
                                contextName);
                            await context.Database.EnsureDeletedAsync(cancellationToken).AnyContext();
                            exists = false;
                        }

                        if (exists && this.options.EnsureTruncated)
                        {
                            this.logger.LogDebug("{LogKey} database migrator truncate tables (context={DbContextType})",
                                Constants.LogKey,
                                contextName);
                            if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
                            {
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.SqlServer.TruncateAllTables(this.options
                                            .EnsureTruncatedIgnoreTables),
                                        cancellationToken)
                                    .AnyContext();
                            }
                            else if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                            {
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.Sqlite.TruncateAllTables(this.options
                                            .EnsureTruncatedIgnoreTables),
                                        cancellationToken)
                                    .AnyContext();
                            }
                            else
                            {
                                throw new ArgumentException(
                                    $"Database provider '{context.Database.ProviderName}' does not supported truncating tables.");
                            }
                        }

                        this.logger.LogDebug(
                            "{LogKey} database migrator get pending migrations (context={DbContextType})",
                            Constants.LogKey,
                            contextName);
                        var migrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
                        if (!exists || migrations.SafeAny())
                        {
                            this.logger.LogDebug(
                                $"{{LogKey}} database migrator apply pending migrations (context={{DbContextType}}) {migrations.ToString(", ")}",
                                Constants.LogKey,
                                contextName);

                            await context.Database.MigrateAsync(cancellationToken).AnyContext();
                            exists = true;

                            this.logger.LogInformation(
                                "{LogKey} database migrator finished (context={DbContextType}, dbexists({DbExists}), provider={EntityFrameworkCoreProvider})",
                                Constants.LogKey,
                                contextName,
                                exists,
                                context.Database.ProviderName);
                        }
                        else
                        {
                            this.logger.LogDebug(
                                "{LogKey} database migrator skipped, no pending migrations (context={DbContextType})",
                                Constants.LogKey,
                                contextName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex,
                        "{LogKey} database migrator failed: {ErrorMessage} (context={DbContextType})",
                        Constants.LogKey,
                        ex.Message,
                        contextName);
                }
            },
            cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static bool IsInMemoryContext(TContext context)
    {
        return context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    }
}