// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Measures finite fully drained asynchronous allocation batches separately from retained heap.</summary>
public static class AppStateAllocationExperiments
{
    /// <summary>Runs isolated warmup and five measured batches for each owned-work scenario.</summary>
    /// <param name="args">Optional --output followed by the result JSON path.</param>
    /// <returns>Completion after all workers and owners are drained.</returns>
    public static async Task RunAsync(string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--output");
        var path = outputIndex >= 0 ? args[outputIndex + 1] : ".tmp/appstate-benchmarks/async-allocations.json";
        var results = new List<object>();
        foreach (var scenario in new[] { "idle", "async-selectors", "coalesced-saves", "lease-subscription-cleanup" })
        {
            await BatchAsync(scenario, 1000);
            for (var batch = 0; batch < 5; batch++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var start = GC.GetTotalAllocatedBytes(precise: true);
                var gen0 = GC.CollectionCount(0);
                var gen1 = GC.CollectionCount(1);
                var gen2 = GC.CollectionCount(2);
                var timer = Stopwatch.StartNew();
                var cleanup = await BatchAsync(scenario, 10000);
                timer.Stop();
                var bytes = GC.GetTotalAllocatedBytes(precise: true) - start;
                var collections = new[] { GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2 };
                var retained = GC.GetTotalMemory(forceFullCollection: true);
                results.Add(new { Scenario = scenario, Batch = batch, Operations = 10000, AllocatedBytes = bytes,
                    BytesPerOperation = bytes / 10000d, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
                    Collections = collections, RetainedHeapBytes = retained,
                    cleanup.SubscriptionsCreated, cleanup.SubscriptionsDisposed, cleanup.FinalRegistrationCount });
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { SchemaVersion = 1,
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount, Results = results },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"AppState allocation measurements: {path}");
    }

    private static async Task<(int SubscriptionsCreated, int SubscriptionsDisposed, int FinalRegistrationCount)> BatchAsync(string scenario, int operations)
    {
        if (scenario == "idle")
        {
            for (var i = 0; i < operations; i++) { await Task.CompletedTask; }

            return (0, 0, 0);
        }

        if (scenario == "async-selectors")
        {
            await using var state = new CatalogViewState();
            var workers = new List<Task>(operations);
            using var subscription = state.Subscribe(m => m.Page, (change, token) =>
            {
                var worker = YieldAsync();
                workers.Add(worker);
                return worker;
            });
            for (var i = 1; i <= operations; i++) { await state.RestoreSnapshot(new() { Page = i }); }

            await Task.WhenAll(workers);
            subscription.Dispose();
            return (1, 1, 0);
        }

        if (scenario == "coalesced-saves")
        {
            var storage = new SessionScopedStateStoreProvider();
            await using var state = new CatalogViewState(new AppStateOptions
            { PersistenceType = AppStatePersistenceType.SessionScoped, DebounceDelay = TimeSpan.FromDays(1) }, storage);
            await state.Load();
            for (var i = 1; i <= operations; i++)
            {
                await state.RestoreSnapshot(new() { Page = i });
                if (i % 100 == 0) { await state.Save(); }
            }

            return (0, 0, 0);
        }

        var collection = new ServiceCollection().AddLogging();
        collection.AddAppState().AddState<CatalogViewState>().Done();
        using var services = collection.BuildServiceProvider();
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        var manager = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var disposedSubscriptions = 0;
        for (var i = 0; i < operations; i++)
        {
            using var lease = factory.Create<CatalogViewState>();
            using var subscription = lease.State.Subscribe(m => m.Page, static (change, token) => Task.CompletedTask);
            subscription.Dispose();
            disposedSubscriptions++;
        }

        if (manager.GetAllStates().Count != 0) { throw new InvalidOperationException("Owned state registrations leaked."); }

        return (operations, disposedSubscriptions, manager.GetAllStates().Count);
    }

    private static async Task YieldAsync() => await Task.Yield();
}
