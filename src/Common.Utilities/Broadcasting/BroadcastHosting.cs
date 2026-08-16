// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Resolves the configured node identity or a machine-and-process default.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <example><code>services.AddSingleton&lt;IBroadcastNodeIdentityProvider, DefaultBroadcastNodeIdentityProvider&gt;();</code></example>
public sealed class DefaultBroadcastNodeIdentityProvider(BroadcastingOptions options)
    : IBroadcastNodeIdentityProvider
{
    /// <inheritdoc />
    public string GetNodeIdentity()
    {
        var identity = string.IsNullOrWhiteSpace(options.NodeIdentity)
            ? $"{Environment.MachineName}:{Environment.ProcessId}"
            : options.NodeIdentity.Trim();

        if (identity.Length > 256)
        {
            throw new InvalidOperationException(
                "Broadcast node identity cannot exceed 256 characters."
            );
        }

        return identity;
    }
}

/// <summary>
/// Registers the local node after application startup and removes it during graceful shutdown.
/// </summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="identityProvider">The local identity provider.</param>
/// <param name="registry">The effective node registry.</param>
/// <param name="addressResolvers">The ordered address resolvers.</param>
/// <param name="applicationLifetime">The application lifetime used to await completed host startup.</param>
/// <param name="timeProvider">The provider-neutral clock.</param>
/// <param name="metrics">The optional metrics service.</param>
/// <param name="logger">The optional structured logger.</param>
/// <param name="databaseReadyService">
/// The optional shared database-readiness coordinator. When absent, registration does not wait for
/// database readiness.
/// </param>
/// <example><code>services.AddSingleton&lt;IHostedService, BroadcastNodeLifecycleService&gt;();</code></example>
public sealed class BroadcastNodeLifecycleService(
    BroadcastingOptions options,
    IBroadcastNodeIdentityProvider identityProvider,
    IBroadcastRegistryStore registry,
    IEnumerable<IBroadcastNodeAddressResolver> addressResolvers,
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider,
    IMetricsService metrics = null,
    ILogger<BroadcastNodeLifecycleService> logger = null,
    IDatabaseReadyService databaseReadyService = null
) : BackgroundService
{
    private static readonly DateTimeOffset ProcessStartedUtc = DateTimeOffset.UtcNow;
    private string identity;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        try
        {
            await WaitForApplicationStartedAsync(applicationLifetime, stoppingToken)
                .ConfigureAwait(false);

            options.Validate();
            if (options.StartupDelay > TimeSpan.Zero)
            {
                if (logger is not null)
                {
                    BroadcastingTypedLogger.LogRegistrationStartupDelayed(
                        logger,
                        "UTL",
                        options.StartupDelay.TotalMilliseconds);
                }

                await Task.Delay(options.StartupDelay, timeProvider, stoppingToken)
                    .ConfigureAwait(false);
            }

            if (options.WaitForDatabaseReady && databaseReadyService is not null)
            {
                if (logger is not null)
                {
                    BroadcastingTypedLogger.LogDatabaseReadinessWaiting(
                        logger,
                        "UTL",
                        options.DatabaseReadyName ?? "all",
                        options.DatabaseReadyTimeout.TotalSeconds);
                }

                await databaseReadyService
                    .WaitForReadyAsync(
                        options.DatabaseReadyName,
                        timeout: options.DatabaseReadyTimeout,
                        cancellationToken: stoppingToken)
                    .ConfigureAwait(false);

                if (logger is not null)
                {
                    BroadcastingTypedLogger.LogDatabaseReadinessSatisfied(
                        logger,
                        "UTL",
                        options.DatabaseReadyName ?? "all");
                }
            }

            this.identity = identityProvider.GetNodeIdentity();
            Uri address = null;
            foreach (var resolver in addressResolvers)
            {
                address = await resolver.ResolveAsync(stoppingToken).ConfigureAwait(false);
                if (address is not null)
                {
                    break;
                }
            }

            if (registry.Capabilities.RequiresAdvertisedAddress && address is null)
            {
                throw new InvalidOperationException(
                    "The selected Broadcasting registry requires a directly reachable receiver address."
                );
            }

            var now = timeProvider.GetUtcNow();
            await registry
                .UpsertAsync(
                    new BroadcastNodeRegistrationRequest(
                        this.identity,
                        address,
                        options.Scopes.ToArray(),
                        ProcessStartedUtc,
                        now,
                        options.RegistrationLeaseEnabled
                            ? now + options.RegistrationLeaseDuration
                            : null
                    ),
                    stoppingToken
                )
                .ConfigureAwait(false);

            BroadcastingMetrics.RecordRegistration(metrics, "registered");
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogNodeRegistered(
                    logger,
                    "UTL",
                    this.identity,
                    options.Scopes.Count);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown during delayed startup or database-readiness waiting is normal.
        }
        catch (Exception exception)
        {
            BroadcastingMetrics.RecordRegistration(metrics, "failed");
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogRegistryFailure(logger, "UTL", "register", exception);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        if (!options.Enabled || string.IsNullOrEmpty(this.identity))
        {
            return;
        }

        try
        {
            await registry.RemoveAsync(this.identity, cancellationToken).ConfigureAwait(false);
            BroadcastingMetrics.RecordRegistration(metrics, "unregistered");
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogNodeUnregistered(logger, "UTL", this.identity);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown cancellation must not fail the host.
        }
        catch (Exception exception)
        {
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogRegistryFailure(logger, "UTL", "unregister", exception);
            }
        }
    }

    private static async Task WaitForApplicationStartedAsync(
        IHostApplicationLifetime applicationLifetime,
        CancellationToken cancellationToken)
    {
        if (applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = applicationLifetime.ApplicationStarted.Register(
            static state => ((TaskCompletionSource)state).TrySetResult(),
            started);
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Renews optional node leases and marks expired registrations inactive.</summary>
/// <param name="options">The shared Broadcasting configuration.</param>
/// <param name="identityProvider">The local identity provider.</param>
/// <param name="registry">The effective node registry.</param>
/// <param name="applicationLifetime">The host application lifetime.</param>
/// <param name="timeProvider">The provider-neutral clock.</param>
/// <param name="databaseReadyService">The optional shared database-readiness coordinator.</param>
/// <example><code>services.AddSingleton&lt;IHostedService, BroadcastRegistrationLeaseService&gt;();</code></example>
public sealed class BroadcastRegistrationLeaseService(
    BroadcastingOptions options,
    IBroadcastNodeIdentityProvider identityProvider,
    IBroadcastRegistryStore registry,
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider,
    IDatabaseReadyService databaseReadyService = null
)
    : PeriodicBackgroundService(
        new PeriodicBackgroundServiceOptions
        {
            Interval = options.RegistrationLeaseRenewalInterval,
            StartupDelay = options.StartupDelay,
        },
        applicationLifetime,
        timeProvider
    )
{
    private bool databaseReady;

    /// <inheritdoc />
    protected override bool IsEnabled => options.Enabled && options.RegistrationLeaseEnabled;

    /// <inheritdoc />
    protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        if (
            !this.databaseReady
            && options.WaitForDatabaseReady
            && databaseReadyService is not null)
        {
            await databaseReadyService
                .WaitForReadyAsync(
                    options.DatabaseReadyName,
                    timeout: options.DatabaseReadyTimeout,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            this.databaseReady = true;
        }

        var now = this.TimeProvider.GetUtcNow();
        await registry
            .RenewLeaseAsync(
                identityProvider.GetNodeIdentity(),
                now + options.RegistrationLeaseDuration,
                cancellationToken
            )
            .ConfigureAwait(false);
        await registry.ExpireLeasesAsync(now, cancellationToken).ConfigureAwait(false);
    }
}
