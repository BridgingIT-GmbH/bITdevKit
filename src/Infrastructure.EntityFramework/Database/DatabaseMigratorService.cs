// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common.Utilities;
using Database;
using Microsoft.Extensions.Hosting;

public class DatabaseMigratorService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseMigratorService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IDatabaseReadyService databaseReadyService;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly DatabaseMigratorOptions options;

    public DatabaseMigratorService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        IDatabaseReadyService databaseReadyService = null,
        DatabaseMigratorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseMigratorService<TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<DatabaseMigratorService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.databaseReadyService = databaseReadyService;
        this.applicationLifetime = applicationLifetime;
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new DatabaseMigratorOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var contextName = typeof(TContext).Name;

        if (!this.options.Enabled)
        {
            this.logger.LogInformation("{LogKey} database migrator skipped, not enabled (context={DbContextType})", Constants.LogKey, contextName);

            if (this.databaseReadyService == null)
            {
                return;
            }

            using var scope = this.serviceProvider.CreateScope();
            if (await this.CheckDatabaseAccessible(contextName, scope.ServiceProvider.GetRequiredService<TContext>(), cancellationToken).AnyContext())
            {
                this.databaseReadyService?.SetReady(contextName);
            }

            return;
        }

        var registration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogInformation("{LogKey} database migrator startup delayed (context={DbContextType})", Constants.LogKey, contextName);
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
                        this.logger.LogInformation("{LogKey} database migrator started (context={DbContextType}, provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, context.Database.ProviderName);

                        var exists = await context.Database.CanConnectAsync(cancellationToken);
                        if (exists && this.options.EnsureDeleted)
                        {
                            this.logger.LogInformation("{LogKey} database migrator delete tables (context={DbContextType})", Constants.LogKey, contextName);
                            await context.Database.EnsureDeletedAsync(cancellationToken).AnyContext();
                            exists = false;
                        }

                        if (exists && this.options.EnsureTruncated)
                        {
                            this.logger.LogInformation("{LogKey} database migrator truncate tables (context={DbContextType})", Constants.LogKey, contextName);
                            if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
                            {
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.SqlServer.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables), cancellationToken).AnyContext();
                            }
                            else if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                            {
                                await context.Database
                                    .ExecuteSqlRawAsync(SqlStatements.Sqlite.TruncateAllTables(this.options.EnsureTruncatedIgnoreTables), cancellationToken).AnyContext();
                            }
                            else
                            {
                                throw new ArgumentException(
                                    $"Database provider '{context.Database.ProviderName}' does not supported truncating tables.");
                            }
                        }

                        this.logger.LogInformation("{LogKey} database migrator get pending migrations (context={DbContextType})", Constants.LogKey, contextName);
                        var migrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
                        if (!exists || migrations.SafeAny())
                        {
                            this.logger.LogInformation($"{{LogKey}} database migrator apply pending migrations (context={{DbContextType}}) {migrations.ToString(", ")}", Constants.LogKey, contextName);

                            await context.Database.MigrateAsync(cancellationToken).AnyContext();
                            exists = true;

                            this.logger.LogInformation("{LogKey} database migrator finished (context={DbContextType}, dbexists({DbExists}), provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, exists, context.Database.ProviderName);
                        }
                        else
                        {
                            this.logger.LogInformation("{LogKey} database migrator skipped, no pending migrations (context={DbContextType})", Constants.LogKey, contextName);
                        }
                    }

                    this.databaseReadyService.SetReady(contextName); // assume database is ready if we reach this point
                }
                catch (Exception ex)
                {
                    this.databaseReadyService.SetFaulted(contextName, ex.Message);
                    this.logger.LogError(ex, "{LogKey} database migrator failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, contextName);
                }
            }, cancellationToken);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<bool> CheckDatabaseAccessible(string contextName, TContext context, CancellationToken cancellationToken)
    {
        if (this.IsInMemoryContext(context))
        {
            return true; // In-memory databases always exist, no need to check
        }

        var retryer = new Retryer(maxRetries: 3, delay: TimeSpan.FromSeconds(1), useExponentialBackoff: false, handleErrors: false, logger: this.logger);
        var exists = false;
        try
        {
            await retryer.ExecuteAsync(async ct =>
            {
                exists = await context.Database.CanConnectAsync(ct);
                if (!exists)
                {
                    this.logger.LogWarning("{LogKey} database check failed: database not accessible (context={DbContextType})", Constants.LogKey, contextName);
                    throw new Exception("Database not accessible.");
                }

                this.logger.LogInformation("{LogKey} database check succeeded: database accessible (context={DbContextType})", Constants.LogKey, contextName);
            }, cancellationToken);
        }
        catch (Exception) // All retries failed, exists remains false
        {
            exists = false;
        }

        return exists;
    }

    private bool IsInMemoryContext(TContext context)
    {
        return context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    }
}