// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Evidence;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns isolated business and evidence files for native provider tests.</summary>
public sealed class LiteDbEvidenceTestHost : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-evidence-");

    /// <summary>Registers providers with independent owners and an explicit disposable prerelease opt-in.</summary>
    /// <param name="configure">Additional service registrations.</param>
    /// <param name="file">An optional preserved audit file.</param>
    /// <param name="fileEventFile">An optional preserved monitoring file.</param>
    /// <param name="maxRows">The maximum materialized rows per command.</param>
    public LiteDbEvidenceTestHost(Action<IServiceCollection> configure = null, string file = null, string fileEventFile = null, int maxRows = 10_000)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        this.AuditFile = file ?? Path.Combine(this.directory.FullName, "audit.db");
        this.MonitoringFile = fileEventFile ?? Path.Combine(this.directory.FullName, "monitoring.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(this.Clock);
        services.AddLiteDbDatabase(o => o.ConnectionString = this.MonitoringFile);
        services.AddLiteDbDatabase("auditing", o => o.ConnectionString = this.AuditFile);
        services.AddFileMonitoring(_ => { }).WithLiteDbStore(o => o.MaxMaterializedRows = maxRows);
        services.AddAuditing().WithLiteDbProvider(o => o.MaxMaterializedRows = maxRows);
        configure?.Invoke(services);
        this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Gets the root service provider.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the native audit filename.</summary>
    public string AuditFile { get; }

    /// <summary>Gets the native monitoring filename.</summary>
    public string MonitoringFile { get; }

    /// <summary>Gets the controlled clock.</summary>
    public EvidenceClock Clock { get; } = new();

    /// <summary>Gets the monitoring event store.</summary>
    public LiteDbFileEventStore Files => this.Services.GetRequiredService<LiteDbFileEventStore>();

    /// <summary>Gets the selected auditing provider.</summary>
    public LiteDbAuditingStorageProvider Audits => this.Services.GetRequiredService<LiteDbAuditingStorageProvider>();

    /// <summary>Disposes native owners before deleting this host's disposable files.</summary>
    /// <returns>The cleanup operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.Services.DisposeAsync();
        this.directory.Delete(true);
    }
}

/// <summary>Provides a stable clock for event-range and retention tests.</summary>
public sealed class EvidenceClock : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
}
