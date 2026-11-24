// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Reports a skipped optional SQL Server factory registration once through the host's configured logger at startup.</summary>
/// <typeparam name="TContext">The context whose existing registration is preserved.</typeparam>
/// <param name="logger">The actual host logger resolved through dependency injection.</param>
/// <param name="reason">The fixed ExistingFactory or MissingNativeOptions diagnostic code.</param>
/// <remarks>This startup diagnostic performs no native I/O and starts no background loop.</remarks>
/// <exception cref="ArgumentNullException">The host logger is null.</exception>
/// <exception cref="ArgumentException">The reason is not one of the fixed registration diagnostic codes.</exception>
/// <example><code>await diagnostic.StartAsync(cancellationToken);</code></example>
public sealed class SqlServerDbContextFactoryWarning<TContext>(ILogger<SqlServerDbContextFactoryWarning<TContext>> logger, string reason) : IHostedService
    where TContext : DbContext
{
    private readonly ILogger<SqlServerDbContextFactoryWarning<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string reason = reason is "ExistingFactory" or "MissingNativeOptions" ? reason :
        throw new ArgumentException("Choose an existing-factory or missing-native-options diagnostic code.", nameof(reason));
    private int started;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref this.started, 1) == 0)
        {
            this.logger.LogWarning("[{LogKey}] independent SQL Server context factory registration skipped (context={ContextType}, reason={Reason})",
                "SqlServerDbContextFactory", typeof(TContext).FullName, this.reason);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}