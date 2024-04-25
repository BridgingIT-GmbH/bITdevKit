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

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} database creator startup delayed (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);

            await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
        }

        try
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                this.logger.LogInformation("{LogKey} database creator started (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);

                if (this.options.EnsureDeleted)
                {
                    await context.Database.EnsureDeletedAsync(cancellationToken).AnyContext();
                }

                await context.Database.EnsureCreatedAsync(cancellationToken).AnyContext();

                try
                {
                    // creates tables of additional DbContexts
                    var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                    databaseCreator?.CreateTables();
                }
                catch
                {
                    // ignore, tables are already created
                }

                this.logger.LogInformation("{LogKey} database creator finished (context={DbContextType})", Constants.LogKey, typeof(TContext).Name);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} database creator failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, typeof(TContext).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}