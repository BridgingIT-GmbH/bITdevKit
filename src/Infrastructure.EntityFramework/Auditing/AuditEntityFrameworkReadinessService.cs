// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using BridgingIT.DevKit.Common;

/// <summary>Validates schema/control state after optional host database readiness, without creating tables or changing providers.</summary>
/// <example><code>await readiness.StartAsync(cancellationToken);</code></example>
public sealed class AuditEntityFrameworkReadinessService<TContext>(EntityFrameworkAuditStorageProvider<TContext> provider, IDatabaseReadyService databaseReadyService = null,
    AuditEntityFrameworkOptions options = null, IHostApplicationLifetime applicationLifetime = null, TimeProvider timeProvider = null) : BackgroundService
    where TContext : DbContext, IAuditingDbContext
{
    /// <summary>Starts coordinated background validation, or validates immediately when no startup coordination is selected.</summary>
    /// <example><code>await readiness.StartAsync(cancellationToken);</code></example>
    public override Task StartAsync(CancellationToken cancellationToken)
        => databaseReadyService is null && (options?.StartupDelay ?? TimeSpan.Zero) == TimeSpan.Zero
            ? this.ValidateAsync(cancellationToken) : base.StartAsync(cancellationToken);

    /// <summary>Allows host-owned background database creation to finish before validating the audit schema.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await AuditBackgroundStartup.WaitAsync<TContext>(options ?? new(), applicationLifetime, databaseReadyService, timeProvider, stoppingToken).ConfigureAwait(false);
        await this.ValidateAsync(stoppingToken).ConfigureAwait(false);
    }

    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        var ready = await provider.CheckReadinessAsync(cancellationToken).ConfigureAwait(false);
        if (ready.IsFailure) throw new InvalidOperationException("Audit storage is unavailable or uninitialized. Deploy the host-owned audit migrations before startup.");
    }

}
