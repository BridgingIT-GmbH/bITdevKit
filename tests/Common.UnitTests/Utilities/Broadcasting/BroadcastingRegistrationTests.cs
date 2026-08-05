// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

using Microsoft.Extensions.DependencyInjection;

public class BroadcastingRegistrationTests
{
    [Fact]
    public void AddBroadcasting_Defaults_UseDocumentedLimits()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBroadcasting(options => options.Scopes("Alpha"));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BroadcastingOptions>();

        // Assert
        options.Enabled.ShouldBeTrue();
        options.StartupDelay.ShouldBe(TimeSpan.Zero);
        options.WaitForDatabaseReady.ShouldBeFalse();
        options.DatabaseReadyName.ShouldBeNull();
        options.DatabaseReadyTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        options.MaximumPayloadBytes.ShouldBe(ByteSize.Kilobytes(64));
        options.DeliveryTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        options.MaximumConcurrentDeliveries.ShouldBe(16);
        options.DefaultLifetime.ShouldBe(TimeSpan.FromSeconds(5));
        options.DuplicateCapacity.ShouldBe(1024);
        options.DuplicateRetention.ShouldBe(TimeSpan.FromMinutes(10));
        options.HandlerQueueCapacity.ShouldBe(32);
        options.UnreachableFailureThreshold.ShouldBe(3);
        options.RegistrationLeaseEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AddBroadcasting_WithoutScopes_UsesDefaultScope()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBroadcasting();
        using var provider = services.BuildServiceProvider();

        // Assert
        provider
            .GetRequiredService<BroadcastingOptions>()
            .Scopes.ShouldBe([BroadcastingOptions.DefaultScope]);
    }

    [Fact]
    public void AddBroadcasting_ExplicitScopesAfterImplicitDefault_ReplacesDefaultScope()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBroadcasting();
        services.AddBroadcasting(options => options.Scopes("Alpha", "Beta"));
        using var provider = services.BuildServiceProvider();

        // Assert
        provider
            .GetRequiredService<BroadcastingOptions>()
            .Scopes.ShouldBe(["Alpha", "Beta"]);
    }

    [Fact]
    public void AddBroadcasting_ExplicitDefaultScope_IsRetainedWithLaterScopes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBroadcasting(options => options.Scopes(BroadcastingOptions.DefaultScope));
        services.AddBroadcasting(options => options.Scopes("Alpha"));
        using var provider = services.BuildServiceProvider();

        // Assert
        provider
            .GetRequiredService<BroadcastingOptions>()
            .Scopes.ShouldBe([BroadcastingOptions.DefaultScope, "Alpha"]);
    }

    [Fact]
    public void StartupConfiguration_UsesFluentDelayAndOptionalDatabaseReadiness()
    {
        // Arrange
        var options = new BroadcastingOptions();
        var sut = new BroadcastingOptionsBuilder(options);

        // Act
        sut.StartupDelay("00:00:15")
            .DatabaseReadiness("AppDbContext", TimeSpan.FromMinutes(3));

        // Assert
        options.StartupDelay.ShouldBe(TimeSpan.FromSeconds(15));
        options.WaitForDatabaseReady.ShouldBeTrue();
        options.DatabaseReadyName.ShouldBe("AppDbContext");
        options.DatabaseReadyTimeout.ShouldBe(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void AddBroadcasting_RepeatedCalls_ComposesOneSharedRuntime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services
            .AddBroadcasting(options => options.Scopes("Alpha"))
            .AddHandler<TestBroadcast, TestBroadcastHandler>();
        services
            .AddBroadcasting(options => options.Scopes("beta", "ALPHA"))
            .AddHandler<TestBroadcast, TestBroadcastHandler>();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BroadcastingOptions>();
        var state = provider.GetRequiredService<BroadcastingRegistrationState>();

        // Assert
        options.Scopes.ShouldBe(["Alpha", "beta"]);
        provider.GetServices<IBroadcastService>().Count().ShouldBe(1);
        services.Count(x => x.ServiceType == typeof(TestBroadcastHandler)).ShouldBe(1);
        state.Handlers.ShouldContain(x =>
            x.PayloadType == typeof(BroadcastProbe)
            && x.HandlerType == typeof(BroadcastProbeHandler)
        );
    }

    [Fact]
    public void AddHandler_DifferentHandlerForSameType_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services
            .AddBroadcasting(options => options.Scopes("Alpha"))
            .AddHandler<TestBroadcast, TestBroadcastHandler>();

        // Act
        var action = () => context.AddHandler<TestBroadcast, ConflictingBroadcastHandler>();

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddBroadcasting_RepeatedEnabledConfiguration_UsesLatestValue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBroadcasting(options => options.Enabled(false));
        services.AddBroadcasting(options => options.Enabled().Scopes("Alpha"));
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<BroadcastingOptions>().Enabled.ShouldBeTrue();
    }

    [Fact]
    public void Scopes_OverEntityStorageLimit_ThrowsArgumentException()
    {
        // Arrange
        var builder = new BroadcastingOptionsBuilder(new BroadcastingOptions());

        // Act
        var action = () => builder.Scopes(new string('a', 257));

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void UseRegistryProvider_DifferentExplicitProviders_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = new ServiceCollection()
            .AddBroadcasting(options => options.Scopes("Alpha"))
            .UseRegistryProvider(typeof(FirstRegistryStore));

        // Act
        var action = () => context.UseRegistryProvider(typeof(SecondRegistryStore));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void UseTransport_RepeatedSameProvider_IsIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddBroadcasting(options => options.Scopes("Alpha"));

        // Act
        context.UseTransport(typeof(TestTransport));
        context.UseTransport(typeof(TestTransport));

        // Assert
        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IBroadcastTransport)
                && descriptor.ImplementationType == typeof(TestTransport)
            )
            .ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_DisabledRuntime_ReturnsTypedFailureWithoutRegistryUse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBroadcasting(options => options.Enabled(false).Scopes("Alpha"));
        using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IBroadcastService>();

        // Act
        var result = await sut.PublishAsync(new TestBroadcast("value"), ["Alpha"]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(x => x is BroadcastingDisabledError);
    }

    public sealed record TestBroadcast(string Value);

    public sealed class TestBroadcastHandler : IBroadcastHandler<TestBroadcast>
    {
        public Task HandleAsync(
            TestBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    public sealed class ConflictingBroadcastHandler : IBroadcastHandler<TestBroadcast>
    {
        public Task HandleAsync(
            TestBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    public sealed class FirstRegistryStore : StubRegistryStore
    {
    }

    public sealed class SecondRegistryStore : StubRegistryStore
    {
    }

    public abstract class StubRegistryStore : IBroadcastRegistryStore
    {
        public BroadcastRegistryCapabilities Capabilities { get; } = new(false, false);

        public Task UpsertAsync(
            BroadcastNodeRegistrationRequest request,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>([]);

        public Task<BroadcastNodeRegistration> FindAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<BroadcastNodeRegistration>(null);

        public Task RecordDeliveryAsync(
            string nodeIdentity,
            bool succeeded,
            string failure,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task RenewLeaseAsync(
            string nodeIdentity,
            DateTimeOffset leaseExpiresUtc,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task ExpireLeasesAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<BroadcastNodeRegistration>>([]);
    }

    public sealed class TestTransport : IBroadcastTransport
    {
        public Task<BroadcastNodeDeliveryResult> SendAsync(
            BroadcastNodeRegistration target,
            BroadcastEnvelope envelope,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new BroadcastNodeDeliveryResult(
                    target.NodeIdentity,
                    BroadcastDeliveryOutcome.Accepted
                )
            );
    }
}