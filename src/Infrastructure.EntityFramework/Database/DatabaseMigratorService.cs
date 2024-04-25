// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[Obsolete("Use the new AddDatabaseMigratorService() service extension from now on")]
public class MigrationsHostedService<TContext> : DatabaseMigratorService<TContext>
    where TContext : DbContext
{
    public MigrationsHostedService(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        : base(loggerFactory, serviceProvider)
    {
    }
}

public class DatabaseMigratorService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseMigratorService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly DatabaseMigratorOptions options;

    public DatabaseMigratorService(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        DatabaseMigratorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseMigratorService<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<DatabaseMigratorService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.options = options ?? new DatabaseMigratorOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} database migrator startup delayed (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);

            await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
        }

        try
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            if (this.options.EnsureDeleted)
            {
                await context.Database.EnsureDeletedAsync(cancellationToken).AnyContext();
            }

            if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory" &&
                (await context.Database.GetPendingMigrationsAsync(cancellationToken: cancellationToken)).Any())
            {
                this.logger.LogInformation("{LogKey} database migrator started (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);

                await context.Database.MigrateAsync(cancellationToken).AnyContext();
                this.logger.LogInformation("{LogKey} database migrator finished (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);
            }
            else
            {
                this.logger.LogDebug("{LogKey} database migrator skipped, no pending migrations (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} database migrator failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, typeof(TContext).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}