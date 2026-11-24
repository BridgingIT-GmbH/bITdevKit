// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Profiling;

using BridgingIT.DevKit.Tests.Profiling;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs the shared provider contract against disposable native files.</summary>
[IntegrationTest("Infrastructure")]
public sealed partial class LiteDbProfilingContractTests : ProfilingStorageContractTestsBase, IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-profiling-contract-");
    private readonly List<ServiceProvider> services = [];
    private readonly Dictionary<IProfilingStorageProvider, (ServiceProvider Services, string File, ProfilingOptions Options, TimeProvider Clock)> hosts = [];

    /// <inheritdoc />
    protected override Task<IProfilingStorageProvider> CreateProviderAsync(ProfilingOptions options, TimeProvider clock)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        return Task.FromResult(this.Create(options, clock, Path.Combine(this.directory.FullName, Guid.NewGuid().ToString("N") + ".db")));
    }

    private IProfilingStorageProvider Create(ProfilingOptions options, TimeProvider clock, string file)
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(options);
        registrations.AddSingleton(clock);
        registrations.AddLiteDbDatabase(o => o.ConnectionString = file);
        registrations.AddProfiling().WithLiteDbProvider();
        var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.services.Add(provider);
        var store = provider.GetRequiredService<IProfilingStorageProvider>();
        this.hosts[store] = (provider, file, options, clock);
        return store;
    }

    private async Task<IProfilingStorageProvider> ReopenAsync(IProfilingStorageProvider store)
    {
        var host = this.hosts[store];
        await host.Services.DisposeAsync();
        this.services.Remove(host.Services);
        this.hosts.Remove(store);
        return this.Create(host.Options, host.Clock, host.File);
    }

    /// <summary>Closes every native owner before deleting test files.</summary>
    /// <returns>The cleanup operation.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var provider in this.services) await provider.DisposeAsync();
        this.directory.Delete(true);
    }
}
