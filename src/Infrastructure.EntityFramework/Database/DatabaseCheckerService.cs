// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.Hosting;

/// <summary>
///     Hosted service that checks database connectivity for a <see cref="DbContext" /> during application startup.
/// </summary>
/// <typeparam name="TContext">The database context type to check.</typeparam>
public class DatabaseCheckerService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<DatabaseCheckerService<TContext>> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IDatabaseReadyService databaseReadyService;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly DatabaseCheckerOptions options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabaseCheckerService{TContext}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="applicationLifetime">The application lifetime used to register startup execution.</param>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="databaseReadyService">The service that reports database readiness and fault state.</param>
    /// <param name="options">The checker behavior options.</param>
    public DatabaseCheckerService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        IDatabaseReadyService databaseReadyService = null,
        DatabaseCheckerOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<DatabaseCheckerService<TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<DatabaseCheckerService<TContext>>();
        this.serviceProvider = serviceProvider;
        this.databaseReadyService = databaseReadyService;
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new DatabaseCheckerOptions();
    }

    /// <summary>
    ///     Starts the checker and executes connectivity checks after the host has started.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel startup processing.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var contextName = typeof(TContext).Name;

        if (!this.options.Enabled)
        {
            this.logger.LogInformation("{LogKey} database checker skipped, not enabled (context={DbContextType})", Constants.LogKey, contextName);
            this.databaseReadyService?.SetReady(contextName);

            return Task.CompletedTask;
        }

        var registration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogInformation("{LogKey} database checker startup delayed (context={DbContextType})", Constants.LogKey, contextName);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(this.options.StartupDelay, cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            // Ignore cancellation during startup delay
                        }
                    }
                }

                try
                {
                    using var scope = this.serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<TContext>();

                    this.logger.LogInformation("{LogKey} database checker started (context={DbContextType}, provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, context.Database.ProviderName);
                    var exists = await this.CheckDatabaseAccessible(contextName, context, cancellationToken);
                    if (exists)
                    {
                        this.logger.LogInformation("{LogKey} database checker succeeded (context={DbContextType}, provider={EntityFrameworkCoreProvider})", Constants.LogKey, contextName, context.Database.ProviderName);
                        this.databaseReadyService?.SetReady(contextName);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "{LogKey} database checker failed: {ErrorMessage} (context={DbContextType})", Constants.LogKey, ex.Message, contextName);

                    this.databaseReadyService?.SetFaulted(contextName, ex.Message);

                    if (this.options.HaltOnFailure)
                    {
                        var failFastMessage = $"{Constants.LogKey} database checker: app terminated (context={contextName}, error={ex.Message})";
                        this.logger.LogCritical(ex, "{FailFastMessage}", failFastMessage);
                        Console.Error.WriteLine(failFastMessage);
                        Environment.FailFast(failFastMessage, ex);
                    }
                }
            }, cancellationToken);
        });

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
                    this.logger.LogWarning("{LogKey} database checker failed: database not accessible (context={DbContextType})", Constants.LogKey, contextName);
                    throw new Exception("Database not accessible.");
                }
            }, cancellationToken);
        }
        catch (Exception) // All retries failed, exists remains false
        {
            exists = false;
        }

        return exists;
    }

    /// <summary>
    ///     Stops the checker service.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel stop processing.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private bool IsInMemoryContext(TContext context)
    {
        return context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    }
}
