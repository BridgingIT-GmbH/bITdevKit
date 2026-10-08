// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Checks final capability composition, shared provider ownership and optional injection.</summary>
public sealed class ProfilingRegistrationTests
{
    /// <summary>Checks omitted setup registers no fallback profiling dependency.</summary>
    [Fact]
    public void OmittedRegistration_HasNoFacadeProviderOrWorkers()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        provider.GetService<IOperationProfiler>().ShouldBeNull();
        provider.GetService<IProfilingStorageProvider>().ShouldBeNull();
        provider.GetServices<IHostedService>().ShouldBeEmpty();
    }

    /// <summary>Checks all capture combinations use one provider and the correct independent workers.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    /// <summary>Exposes the profiling test fixture contract.</summary>
    /// <example>Used by the profiling registration contract tests.</example>
    public void Capabilities_AllCombinations_ShareFacetsWithIndependentWorkers(bool runtime, bool operations)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        var builder = services.AddProfiling(o => o.Enabled());
        if (runtime) { builder.WithRuntimeProfiling(); }

        if (operations) { builder.WithOperationProfiling(); }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var storage = provider.GetRequiredService<IProfilingStorageProvider>();
        provider.GetRequiredService<IRuntimeProfilingStore>().ShouldBeSameAs(storage.Runtime);
        provider.GetRequiredService<IOperationProfilingStore>().ShouldBeSameAs(storage.Operations);
        provider.GetServices<IHostedService>().OfType<OperationProfilingWriterService>().Count().ShouldBe(operations ? 1 : 0);
        provider.GetServices<IHostedService>().OfType<ProfilingMaintenanceService>().Count().ShouldBe(1);
        (provider.GetService<IRuntimeProfilingCollector>() is not null).ShouldBe(runtime);
        (provider.GetService<IBroadcastRegistryStore>() is not null).ShouldBe(runtime);
        using var capture = provider.GetRequiredService<IOperationProfiler>().BeginOperation("configured");
        capture.IsRecording.ShouldBe(operations);
    }

    /// <summary>Checks repeated setup retains explicit options and does not duplicate hosted workers.</summary>
    [Fact]
    public void ReorderedRegistration_PreservesExplicitSettingsAndOneWorkerSet()
    {
        var services = new ServiceCollection();
        var first = services.AddProfiling().WithOperationProfiling(o => o.BatchSize(123));
        first.WithOperationProfiling(o => o.Enabled(false));
        services.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        first.Options.OperationEnabled.ShouldBeFalse();
        first.WithOperationProfiling(o => o.Enabled());
        using var provider = services.BuildServiceProvider();
        first.Options.Operations.BatchSize.ShouldBe(123);
        provider.GetServices<IHostedService>().OfType<OperationProfilingWriterService>().ShouldHaveSingleItem();
        provider.GetServices<IHostedService>().OfType<OperationProfilingCleanupService>().ShouldHaveSingleItem();
        provider.GetServices<IHostedService>().OfType<ProfilingMaintenanceService>().ShouldHaveSingleItem();
    }

    /// <summary>Checks the master flag installs an inert façade with no capture workers.</summary>
    [Fact]
    public void MasterDisabled_LeavesInertFacadeAndNoCaptureWorkers()
    {
        var services = new ServiceCollection();
        services.AddProfiling().WithOperationProfiling().WithRuntimeProfiling();
        using var provider = services.BuildServiceProvider();
        using var operation = provider.GetRequiredService<IOperationProfiler>().BeginOperation("disabled");
        operation.IsRecording.ShouldBeFalse();
        operation.Id.ShouldBe(Guid.Empty);
        provider.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<ProfilingConfigurationValidationService>();
        provider.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().CaptureEnabled.ShouldBeFalse();
    }

    /// <summary>Checks final options validation occurs after reordered fluent configuration.</summary>
    [Fact]
    public async Task InvalidFinalLimits_FailStartupValidation()
    {
        var services = new ServiceCollection();
        var builder = services.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        builder.Options.Operations.QueueCapacity = 0;
        using var provider = services.BuildServiceProvider();
        var action = () => Task.Run(() => provider.GetServices<IHostedService>().First().StartAsync(default));
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    /// <summary>Checks conflicting explicit providers fail instead of installing write fan-out.</summary>
    [Fact]
    public void Provider_ConflictingSelections_FailWithoutReplacingOriginal()
    {
        var services = new ServiceCollection();
        var builder = services.AddProfiling().WithInMemoryProvider();
        var action = () => builder.WithProvider<AlternateProvider>();
        action.ShouldThrow<InvalidOperationException>();
        builder.WithInMemoryProvider();
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IProfilingStorageProvider>().ShouldHaveSingleItem().ShouldBeOfType<InMemoryProfilingStorageProvider>();
    }

    /// <summary>Checks a broken explicitly registered dependency is not treated as optional absence.</summary>
    [Fact]
    public void ExplicitBrokenGraph_PropagatesSetupFailure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOperationProfiler>(_ => throw new InvalidOperationException("broken graph"));
        services.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var action = () => provider.GetRequiredService<IOperationProfiler>();
        action.ShouldThrow<InvalidOperationException>().Message.ShouldBe("broken graph");
    }

    /// <summary>Checks host worker stop ordering closes captures before persistence drain.</summary>
    [Fact]
    public void WorkerOrder_ClosesCaptureBeforeDrain()
    {
        var services = new ServiceCollection();
        services.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IHostedService>().ToList();
        workers.IndexOf(workers.OfType<OperationProfilingWriterService>().Single())
            .ShouldBeLessThan(workers.IndexOf(workers.OfType<OperationProfilingCleanupService>().Single()));
    }

    /// <summary>Checks provider defaults do not overwrite explicitly equal-to-memory defaults.</summary>
    [Fact]
    public void ProviderDefaults_PreserveEveryExplicitRetentionSetting()
    {
        var defaults = new OperationProfilingOptions().SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));
        defaults.MaximumRetainedOperations.ShouldBe(100000);
        defaults.MaximumOperationAge.ShouldBe(TimeSpan.FromDays(7));
        defaults.MaximumRetainedBytes.ShouldBe(long.MaxValue);
        var explicitOptions = new OperationProfilingOptions
        {
            MaximumRetainedOperations = 10000, MaximumOperationAge = TimeSpan.FromHours(24), MaximumRetainedBytes = 128L * 1024 * 1024,
        };
        var selected = explicitOptions.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));
        selected.MaximumRetainedOperations.ShouldBe(10000);
        selected.MaximumOperationAge.ShouldBe(TimeSpan.FromHours(24));
        selected.MaximumRetainedBytes.ShouldBe(128L * 1024 * 1024);
    }

    private sealed class AlternateProvider : IProfilingStorageProvider
    {
        private readonly InMemoryProfilingStorageProvider storage = new();
        /// <summary>Exposes the profiling test fixture contract.</summary>
        /// <example>Used by the profiling registration contract tests.</example>
        public ProfilingProviderCapabilities Capabilities => this.storage.Capabilities;
        public IRuntimeProfilingStore Runtime => this.storage.Runtime;
        public IOperationProfilingStore Operations => this.storage.Operations;
        /// <summary>Exposes the profiling test fixture contract.</summary>
        /// <example>Used by the profiling registration contract tests.</example>
        public Task<IResult<ProfilingClearResult>> ClearAsync(ProfilingClearRequest request, CancellationToken cancellationToken = default) => this.storage.ClearAsync(request, cancellationToken);
        public Task<IResult<ProfilingMaintenanceResult>> ResumeMaintenanceAsync(ProfilingMaintenanceRequest request, CancellationToken cancellationToken = default) => this.storage.ResumeMaintenanceAsync(request, cancellationToken);
    }
}
