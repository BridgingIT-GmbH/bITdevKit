// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ProfilingFoundationTests
{
    [Fact]
    public void WithRuntimeProfiling_DisableWithExistingBroadcast_RemovesOnlyOwnedHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBroadcasting();
        var state = services.First(d => d.ServiceType == typeof(BroadcastingRegistrationState)).ImplementationInstance as BroadcastingRegistrationState;
        var originalHandlers = state.Handlers.ToArray();
        var configuration = services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling();
        state.Handlers.Count.ShouldBe(originalHandlers.Length + 4);

        // Act
        configuration.WithRuntimeProfiling(o => o.Enabled(false));

        // Assert
        state.Handlers.ShouldBe(originalHandlers);
        services.ShouldContain(d => d.ServiceType == typeof(IBroadcastRegistryStore));
        services.ShouldNotContain(d => d.ServiceType == typeof(IRuntimeProfilingCollector));
    }

    [Fact]
    public void AddProfiling_MasterEnabledWithoutRuntime_InstallsNoBroadcastOrRuntimeWorkers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProfiling(o => o.Enabled());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<ProfilingOptions>().RuntimeEnabled.ShouldBeFalse();
        services.ShouldNotContain(d => d.ServiceType == typeof(IRuntimeProfilingBroadcastService));
        services.ShouldNotContain(d => d.ServiceType == typeof(IRuntimeProfilingCollector));
        services.ShouldNotContain(d => d.ServiceType == typeof(IBroadcastRegistryStore));
        provider.GetRequiredService<IProfilingNodeIdentityProvider>().GetNode().ShouldNotBeNull();
    }

    [Fact]
    public void WithRuntimeProfiling_BeforeMasterEnablement_ComposesOneWorkerSet()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProfiling().WithRuntimeProfiling(o => o.Duration(TimeSpan.FromMinutes(1)));

        // Act
        var configuration = services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling();
        configuration.WithRuntimeProfiling(o => o.SamplingInterval(TimeSpan.FromSeconds(2)));

        // Assert
        configuration.Options.RuntimeEnabled.ShouldBeTrue();
        configuration.Options.Runtime.Duration.ShouldBe(TimeSpan.FromMinutes(1));
        configuration.Options.Runtime.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(2));
        services.Count(d => d.ImplementationType == typeof(RuntimeProfilingCollectorHostedService)).ShouldBe(1);
    }

    [Fact]
    public void WithRuntimeProfiling_ExplicitDisableThenRepeatedCall_RemainsDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling();

        // Act
        configuration.WithRuntimeProfiling(o => o.Enabled(false));
        configuration.WithRuntimeProfiling();

        // Assert
        configuration.Options.RuntimeEnabled.ShouldBeFalse();
        services.ShouldNotContain(d => d.ServiceType == typeof(IRuntimeProfilingCollector));
        services.ShouldNotContain(d => d.ServiceType == typeof(IRuntimeProfilingBroadcastService));
    }

    [Fact]
    public void AddProfiling_Defaults_UseApprovedConservativeValues()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProfiling();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ProfilingOptions>();

        // Assert
        options.Enabled.ShouldBeFalse();
        RuntimeProfilingOptions.MinimumSamplingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.Runtime.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(1));
        options.Runtime.Duration.ShouldBe(TimeSpan.FromSeconds(30));
        options.Runtime.AutomaticStop.ShouldBeTrue();
        options.Runtime.MaximumRetainedSessions.ShouldBe(20);
        options.Runtime.MaximumSessionAge.ShouldBe(TimeSpan.FromDays(7));
        options.Runtime.Enabled.ShouldBeFalse();
        options.Operations.Enabled.ShouldBeFalse();
        options.Requests.Enabled.ShouldBeFalse();
        options.Runtime.ParticipationDeadline.ShouldBe(TimeSpan.FromSeconds(1));
        options.Runtime.FinalizationGracePeriod.ShouldBe(TimeSpan.FromSeconds(1));
        RuntimeProfilingOptions.DefaultSessionNameFormat.ShouldBe("O");
    }

    [Fact]
    public void SamplingInterval_BelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new RuntimeProfilingOptionsBuilder(new RuntimeProfilingOptions());

        // Act
        var action = () => builder.SamplingInterval(TimeSpan.FromMilliseconds(499));

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Duration_NonPositive_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new RuntimeProfilingOptionsBuilder(new RuntimeProfilingOptions());

        // Act
        var action = () => builder.Duration(TimeSpan.Zero);

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_EnabledWithoutAutomaticStop_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Runtime = new() { Enabled = true, AutomaticStop = false } };

        // Act
        var action = options.Validate;

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddProfiling_RepeatedCalls_UpdateOneSharedOptionsInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var first = services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling();
        var second = services.AddProfiling().WithRuntimeProfiling(options => options.SamplingInterval(TimeSpan.FromSeconds(2)).Duration(TimeSpan.FromMinutes(1)));
        using var provider = services.BuildServiceProvider();

        // Assert
        first.Options.ShouldBeSameAs(second.Options);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(ProfilingOptions))
            .ShouldBe(1);
        provider.GetRequiredService<ProfilingOptions>().Enabled.ShouldBeTrue();
        provider
            .GetRequiredService<ProfilingOptions>()
            .Runtime.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(2));
        provider.GetRequiredService<ProfilingOptions>().Runtime.Duration.ShouldBe(TimeSpan.FromMinutes(1));
        provider.GetServices<IRuntimeProfilingStore>().ShouldHaveSingleItem();
        provider.GetRequiredService<IRuntimeProfilingStore>().ShouldBeOfType<InMemoryRuntimeProfilingStore>();
        provider
            .GetServices<IProfilingNodeIdentityProvider>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingNodeIdentityProvider>();
        provider
            .GetServices<IRuntimeProfilingContextFactory>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingContextFactory>();
        provider
            .GetServices<IRuntimeProfilingSnapshotProbe>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingSnapshotProbe>();
        provider
            .GetServices<IRuntimeProfilingCollector>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingCollector>();
        services
            .Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(RuntimeProfilingCollectorHostedService)
            )
            .ShouldBe(1);
        provider
            .GetServices<IRuntimeProfilingControlService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingControlService>();
        provider
            .GetServices<IRuntimeProfilingBroadcastService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingBroadcastService>();
        provider
            .GetServices<IRuntimeProfilingMeasurementService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingMeasurementService>();
        provider
            .GetServices<IRuntimeProfilingEvaluationService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingEvaluator>();
        provider
            .GetServices<IRuntimeProfilingPerfettoExportService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingPerfettoExportService>();
        provider
            .GetServices<IRuntimeProfilingQueryService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RuntimeProfilingQueryService>();
        provider.GetRequiredService<RuntimeProfilingActiveSessionContext>().ShouldNotBeNull();
        provider.GetRequiredService<RuntimeProfilingSegmentContext>().ShouldNotBeNull();
        provider.GetRequiredService<RuntimeProfilingCustomMetricListener>().ShouldNotBeNull();
        services
            .Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(RuntimeProfilingCustomMetricHostedService)
            )
            .ShouldBe(1);
        var handlers = provider.GetRequiredService<BroadcastingRegistrationState>().Handlers;
        handlers
            .Count(handler => handler.PayloadType == typeof(RuntimeProfilingStartBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(RuntimeProfilingStopBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(RuntimeProfilingSnapshotBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(RuntimeProfilingGarbageCollectionBroadcast))
            .ShouldBe(1);
    }

    [Fact]
    public async Task AddProfiling_Disabled_RegistersOnlyInertApplicationSurfaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProfiling();
        using var provider = services.BuildServiceProvider();
        var status = await provider.GetRequiredService<IRuntimeProfilingControlService>().GetStatusAsync();
        var measurement = await provider
            .GetRequiredService<IRuntimeProfilingMeasurementService>()
            .BeginAsync("disabled");
        var query = await provider.GetRequiredService<IRuntimeProfilingQueryService>().ListSessionsAsync();

        // Assert
        services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldHaveSingleItem();
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingStore));
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IRuntimeProfilingCollector)
        );
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IRuntimeProfilingSnapshotProbe)
        );
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IRuntimeProfilingBroadcastService)
        );
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingControlService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingMeasurementService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingEvaluationService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingPerfettoExportService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingQueryService))
            .ShouldBe(1);
        status.IsSuccess.ShouldBeTrue();
        status.Value.Enabled.ShouldBeFalse();
        status.Value.Available.ShouldBeFalse();
        measurement.IsFailure.ShouldBeTrue();
        measurement.Errors.ShouldContain(error => error is ProfilingDisabledError);
        query.IsFailure.ShouldBeTrue();
        query.Errors.ShouldContain(error => error is ProfilingDisabledError);
    }

    [Fact]
    public void CreateIdentities_AlwaysUseEightCharacterLowercaseKeys()
    {
        // Act
        var session = ProfilingIdentityFactory.CreateRuntimeSession();
        var node = ProfilingIdentityFactory.CreateNode();
        var snapshot = ProfilingIdentityFactory.CreateRuntimeSnapshot();

        // Assert
        AssertIdentity(session.Id, session.Key);
        AssertIdentity(node.Id, node.Key);
        AssertIdentity(snapshot.Id, snapshot.Key);
    }

    [Fact]
    public void Identity_InvalidPublicKey_ThrowsArgumentException()
    {
        // Act
        var action = () =>
        {
            _ = new RuntimeProfilingSessionIdentity(Guid.NewGuid(), "ABC-1234");
        };

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Identity_InternalIdentifier_IsExcludedFromJson()
    {
        // Arrange
        var identity = new RuntimeProfilingSessionIdentity(
            Guid.Parse("52de217d-ca84-442e-ac83-c8c328586b21"),
            "a1b2c3d4"
        );

        // Act
        var json = JsonSerializer.Serialize(identity);

        // Assert
        json.ShouldContain("\"Key\":\"a1b2c3d4\"");
        json.ShouldNotContain("52de217d");
        json.ShouldNotContain("\"Id\"");
    }

    [Fact]
    public void InvalidKeyError_AlwaysUsesFixedSafeMessage()
    {
        // Act
        var error = new ProfilingInvalidKeyError("session");

        // Assert
        error.Message.ShouldBe("The session key is invalid.");
    }

    [Fact]
    public void ProfilingNode_PrivateBroadcastCorrelation_IsExcludedFromJson()
    {
        // Arrange
        var node = new ProfilingNode
        {
            Identity = ProfilingIdentityFactory.CreateNode(),
            Correlation = new RuntimeProfilingNodeCorrelation(
                "private-host:1234",
                DateTimeOffset.Parse("2026-08-07T10:00:00Z")
            ),
            HostName = "host",
            ProcessId = 1234,
        };

        // Act
        var json = JsonSerializer.Serialize(node);

        // Assert
        json.ShouldNotContain("private-host");
        json.ShouldNotContain("Correlation");
        json.ShouldContain("\"HostName\":\"host\"");
    }

    [Fact]
    public void SessionState_AllApprovedStates_AreRepresented()
    {
        Enum.GetValues<RuntimeProfilingSessionState>()
            .ShouldBe([
                RuntimeProfilingSessionState.Running,
                RuntimeProfilingSessionState.Completed,
                RuntimeProfilingSessionState.CompletedWithWarnings,
                RuntimeProfilingSessionState.Stopped,
                RuntimeProfilingSessionState.Failed,
            ]);
    }

    [Fact]
    public void NodeRole_ExpectedAndAdHoc_AreRepresented()
    {
        Enum.GetValues<RuntimeProfilingNodeRole>()
            .ShouldBe([RuntimeProfilingNodeRole.ExpectedParticipant, RuntimeProfilingNodeRole.AdHocContributor]);
    }

    [Fact]
    public void EvaluationResult_ContainsOnlyApprovedTopLevelGroups()
    {
        // Arrange
        var properties = typeof(RuntimeProfilingEvaluationResult)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        // Assert
        properties.ShouldBe(["Scope", "DataQuality", "KPIs", "Signals", "Limitations"]);
    }

    private static void AssertIdentity(Guid id, string key)
    {
        id.ShouldNotBe(Guid.Empty);
        key.Length.ShouldBe(8);
        key.All(character =>
                character >= 'a' && character <= 'z' || character >= '0' && character <= '9'
            )
            .ShouldBeTrue();
    }
}
