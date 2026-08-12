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
        ProfilingOptions.MinimumSamplingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(1));
        options.Duration.ShouldBe(TimeSpan.FromSeconds(30));
        options.AutomaticStop.ShouldBeTrue();
        options.MaximumRetainedSessions.ShouldBe(20);
        options.MaximumSessionAge.ShouldBe(TimeSpan.FromDays(7));
        options.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.ParticipationDeadline.ShouldBe(TimeSpan.FromSeconds(1));
        options.FinalizationGracePeriod.ShouldBe(TimeSpan.FromSeconds(1));
        ProfilingOptions.DefaultSessionNameFormat.ShouldBe("O");
    }

    [Fact]
    public void SamplingInterval_BelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new ProfilingOptionsBuilder(new ProfilingOptions());

        // Act
        var action = () => builder.SamplingInterval(TimeSpan.FromMilliseconds(499));

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Duration_NonPositive_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new ProfilingOptionsBuilder(new ProfilingOptions());

        // Act
        var action = () => builder.Duration(TimeSpan.Zero);

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_EnabledWithoutAutomaticStop_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, AutomaticStop = false };

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
        var first = services.AddProfiling(options => options.Enabled());
        var second = services.AddProfiling(options =>
            options.SamplingInterval(TimeSpan.FromSeconds(2)).Duration(TimeSpan.FromMinutes(1))
        );
        using var provider = services.BuildServiceProvider();

        // Assert
        first.Options.ShouldBeSameAs(second.Options);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(ProfilingOptions))
            .ShouldBe(1);
        provider.GetRequiredService<ProfilingOptions>().Enabled.ShouldBeTrue();
        provider
            .GetRequiredService<ProfilingOptions>()
            .SamplingInterval.ShouldBe(TimeSpan.FromSeconds(2));
        provider.GetRequiredService<ProfilingOptions>().Duration.ShouldBe(TimeSpan.FromMinutes(1));
        provider.GetServices<IProfilingStore>().ShouldHaveSingleItem();
        provider.GetRequiredService<IProfilingStore>().ShouldBeOfType<InMemoryProfilingStore>();
        provider
            .GetServices<IProfilingNodeIdentityProvider>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingNodeIdentityProvider>();
        provider
            .GetServices<IProfilingRuntimeContextFactory>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingRuntimeContextFactory>();
        provider
            .GetServices<IProfilingSnapshotProbe>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingSnapshotProbe>();
        provider
            .GetServices<IProfilingCollector>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingCollector>();
        services
            .Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ProfilingCollectorHostedService)
            )
            .ShouldBe(1);
        provider
            .GetServices<IProfilingControlService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingControlService>();
        provider
            .GetServices<IProfilingBroadcastService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingBroadcastService>();
        provider
            .GetServices<IProfilingMeasurementService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingMeasurementService>();
        provider
            .GetServices<IProfilingEvaluationService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingEvaluator>();
        provider
            .GetServices<IProfilingPerfettoExportService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingPerfettoExportService>();
        provider
            .GetServices<IProfilingQueryService>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<ProfilingQueryService>();
        provider.GetRequiredService<ProfilingActiveSessionContext>().ShouldNotBeNull();
        provider.GetRequiredService<ProfilingSegmentContext>().ShouldNotBeNull();
        provider.GetRequiredService<ProfilingCustomMetricListener>().ShouldNotBeNull();
        services
            .Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ProfilingCustomMetricHostedService)
            )
            .ShouldBe(1);
        var handlers = provider.GetRequiredService<BroadcastingRegistrationState>().Handlers;
        handlers
            .Count(handler => handler.PayloadType == typeof(ProfilingStartBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(ProfilingStopBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(ProfilingSnapshotBroadcast))
            .ShouldBe(1);
        handlers
            .Count(handler => handler.PayloadType == typeof(ProfilingGarbageCollectionBroadcast))
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
        var status = await provider.GetRequiredService<IProfilingControlService>().GetStatusAsync();
        var measurement = await provider
            .GetRequiredService<IProfilingMeasurementService>()
            .BeginAsync("disabled");
        var query = await provider.GetRequiredService<IProfilingQueryService>().ListSessionsAsync();

        // Assert
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IProfilingStore));
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IProfilingCollector)
        );
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IProfilingSnapshotProbe)
        );
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IProfilingBroadcastService)
        );
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IProfilingControlService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IProfilingMeasurementService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IProfilingEvaluationService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IProfilingPerfettoExportService))
            .ShouldBe(1);
        services
            .Count(descriptor => descriptor.ServiceType == typeof(IProfilingQueryService))
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
        var session = ProfilingSessionIdentity.Create();
        var node = ProfilingNodeIdentity.Create();
        var snapshot = ProfilingSnapshotIdentity.Create();

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
            _ = new ProfilingSessionIdentity(Guid.NewGuid(), "ABC-1234");
        };

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Identity_InternalIdentifier_IsExcludedFromJson()
    {
        // Arrange
        var identity = new ProfilingSessionIdentity(
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
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = new ProfilingNodeCorrelation(
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
        Enum.GetValues<ProfilingSessionState>()
            .ShouldBe([
                ProfilingSessionState.Running,
                ProfilingSessionState.Completed,
                ProfilingSessionState.CompletedWithWarnings,
                ProfilingSessionState.Stopped,
                ProfilingSessionState.Failed,
            ]);
    }

    [Fact]
    public void NodeRole_ExpectedAndAdHoc_AreRepresented()
    {
        Enum.GetValues<ProfilingNodeRole>()
            .ShouldBe([ProfilingNodeRole.ExpectedParticipant, ProfilingNodeRole.AdHocContributor]);
    }

    [Fact]
    public void EvaluationResult_ContainsOnlyApprovedTopLevelGroups()
    {
        // Arrange
        var properties = typeof(ProfilingEvaluationResult)
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
