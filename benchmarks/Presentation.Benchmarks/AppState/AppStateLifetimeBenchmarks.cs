// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Measures state ownership against bounded live sibling inventories.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateLifetimeBenchmarks
{
    private ServiceProvider services;
    private IServiceScope scope;
    private AppStateFactory factory;
    private AppStateManager manager;
    private readonly List<IDisposable> siblings = [];

    /// <summary>Gets or sets the number of live sibling states.</summary>
    [Params(1, 100)]
    public int Siblings { get; set; }

    /// <summary>Resolves scoped dependencies and creates siblings before timing.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var collection = new ServiceCollection().AddLogging();
        collection.AddAppState().AddState<CatalogViewState>().Done();
        this.services = collection.BuildServiceProvider();
        this.scope = this.services.CreateScope();
        this.factory = this.scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        this.manager = this.scope.ServiceProvider.GetRequiredService<AppStateManager>();
        for (var i = 0; i < this.Siblings; i++) { this.siblings.Add(this.factory.Create<CatalogViewState>()); }
    }

    /// <summary>Creates and releases a state with the current runtime's dependencies.</summary>
    [Benchmark]
    public Guid CreateDisposeLease()
    {
        using var lease = this.factory.Create<CatalogViewState>();
        return lease.InstanceId;
    }

    /// <summary>Measures explicit metadata registration and removal.</summary>
    [Benchmark]
    public Guid RegisterUnregister()
    {
        using var state = new CatalogViewState();
        this.manager.RegisterState(state);
        this.manager.UnregisterState(state);
        return state.InstanceId;
    }

    /// <summary>Verifies inventory cleanup and releases the DI scope.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var sibling in this.siblings) { sibling.Dispose(); }

        if (this.manager.GetAllStates().Count != 0) { throw new InvalidOperationException("State inventory leaked."); }

        this.scope.Dispose();
        this.services.Dispose();
    }
}
