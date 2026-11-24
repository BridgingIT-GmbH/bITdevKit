// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns an isolated real-file host for durable provider contract tests.</summary>
public sealed class LiteDbDurableTestHost : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-durable-");

    /// <summary>Builds the feature registrations without starting external services.</summary>
    /// <param name="configure">Additional service registrations.</param>
    /// <param name="file">An optional preserved file for reopen checks.</param>
    /// <param name="configureJobs">Optional scheduler definitions for runtime integration.</param>
    /// <param name="configureOrchestrations">Optional workflow definitions for runtime integration.</param>
    public LiteDbDurableTestHost(Action<IServiceCollection> configure = null, string file = null, Action<JobBuilderContext> configureJobs = null,
        Action<OrchestrationBuilderContext> configureOrchestrations = null)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        this.File = file ?? Path.Combine(this.directory.FullName, "durable.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOrchestrationClock>(this.Clock);
        services.AddSingleton<TimeProvider>(new ClockTimeProvider(this.Clock));
        services.AddLiteDbDatabase(o => o.ConnectionString = this.File);
        var jobs = services.AddJobScheduler().AliveEnabled(false);
        configureJobs?.Invoke(jobs);
        jobs.WithLiteDb();
        var orchestrations = services.AddOrchestrations();
        configureOrchestrations?.Invoke(orchestrations);
        orchestrations.WithLiteDb();
        configure?.Invoke(services);
        this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Gets the file used by this host.</summary>
    public string File { get; }

    /// <summary>Gets the root provider.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the deterministic clock used by both stores.</summary>
    public FakeOrchestrationClock Clock { get; } = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Gets the Jobs store.</summary>
    public IJobStoreProvider Jobs => this.Services.GetRequiredService<IJobStoreProvider>();

    /// <summary>Gets the orchestration store.</summary>
    public IOrchestrationStorageProvider Orchestrations => this.Services.GetRequiredService<IOrchestrationStorageProvider>();

    /// <summary>Releases the owner and removes this host's temporary directory.</summary>
    /// <returns>The cleanup task.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.Services.DisposeAsync();
        this.directory.Delete(true);
    }

    private sealed class ClockTimeProvider(FakeOrchestrationClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.UtcNow;
    }
}
