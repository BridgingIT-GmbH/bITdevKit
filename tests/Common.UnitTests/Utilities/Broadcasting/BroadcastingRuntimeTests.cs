// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class BroadcastingRuntimeTests
{
    [Fact]
    public async Task PublishAsync_WithoutScopes_RegistersAndTargetsDefaultScope()
    {
        // Arrange
        var handled = new TaskCompletionSource<TestBroadcast>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(handled);
                services
                    .AddBroadcasting(options => options.NodeIdentity("node-default"))
                    .AddHandler<TestBroadcast, TestBroadcastHandler>();
            })
            .Build();
        await host.StartAsync();
        var sut = host.Services.GetRequiredService<IBroadcastService>();

        // Act
        var result = await sut.PublishAsync(new TestBroadcast("default"));
        var received = await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetScopes.ShouldBe([BroadcastingOptions.DefaultScope]);
        result.Value.TargetCount.ShouldBe(1);
        received.Value.ShouldBe("default");
        await host.StopAsync();
    }

    [Fact]
    public async Task PublishAsync_InMemoryHost_UsesReceiverAndExecutesTypedHandler()
    {
        // Arrange
        var handled = new TaskCompletionSource<TestBroadcast>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(handled);
                services
                    .AddBroadcasting(options => options.Scopes("Alpha").NodeIdentity("node-a"))
                    .AddHandler<TestBroadcast, TestBroadcastHandler>();
            })
            .Build();
        await host.StartAsync();
        var sut = host.Services.GetRequiredService<IBroadcastService>();

        // Act
        var result = await sut.PublishAsync(new TestBroadcast("value"), ["alpha"]);
        var received = await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetCount.ShouldBe(1);
        result.Value.ResponseCount.ShouldBe(1);
        result.Value.AcceptedCount.ShouldBe(1);
        result.Value.TargetScopes.ShouldBe(["alpha"]);
        result.Value.StartedUtc.ShouldNotBe(default);
        result.Value.CompletedUtc.ShouldBeGreaterThanOrEqualTo(result.Value.StartedUtc);
        received.Value.ShouldBe("value");
        await host.StopAsync();
    }

    [Fact]
    public async Task Diagnostics_DefaultAuthorizer_DeniesRemovalAndGroupsScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBroadcasting(options => options.Scopes("Alpha"));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IBroadcastRegistryStore>();
        var now = DateTimeOffset.UtcNow;
        await registry.UpsertAsync(new("node-a", null, ["Alpha", "Beta"], now, now, null));
        var sut = provider.GetRequiredService<IBroadcastingDiagnostics>();

        // Act
        var snapshot = await sut.GetAsync();
        var removal = await sut.RemoveAsync("node-a");

        // Assert
        snapshot.Enabled.ShouldBeTrue();
        snapshot.Scopes.Select(scope => scope.Scope).ShouldBe(["Alpha", "Beta"]);
        removal.IsFailure.ShouldBeTrue();
        removal.Errors.ShouldContain(error => error is BroadcastOperationalAuthorizationError);
        (await registry.FindAsync("node-a")).ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAsync_WithAmbientCorrelation_PropagatesCorrelationInsteadOfTraceId()
    {
        // Arrange
        var handled = new TaskCompletionSource<CorrelationCapture>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(handled);
                services
                    .AddBroadcasting(options => options.Scopes("Alpha").NodeIdentity("node-a"))
                    .AddHandler<CorrelationBroadcast, CorrelationBroadcastHandler>();
            })
            .Build();
        await host.StartAsync();
        var sut = host.Services.GetRequiredService<IBroadcastService>();
        using var activity = new Activity("broadcast-correlation-test").Start();
        using var correlationScope = CorrelationId.BeginScope("correlation-123");

        // Act
        var result = await sut.PublishAsync(new CorrelationBroadcast(), ["Alpha"]);
        var captured = await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        captured.Context.CorrelationId.ShouldBe("correlation-123");
        captured.Context.SenderNodeIdentity.ShouldBe("node-a");
        captured.Context.CorrelationId.ShouldNotBe(activity.TraceId.ToString());
        captured.AmbientCorrelationId.ShouldBe("correlation-123");
        await host.StopAsync();
    }

    [Fact]
    public async Task PublishAsync_WithInvalidAmbientCorrelation_DoesNotTransportIt()
    {
        // Arrange
        var handled = new TaskCompletionSource<CorrelationCapture>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(handled);
                services
                    .AddBroadcasting(options => options.Scopes("Alpha").NodeIdentity("node-a"))
                    .AddHandler<CorrelationBroadcast, CorrelationBroadcastHandler>();
            })
            .Build();
        await host.StartAsync();
        var sut = host.Services.GetRequiredService<IBroadcastService>();
        using var correlationScope = CorrelationId.BeginScope("invalid\r\nheader");

        // Act
        var result = await sut.PublishAsync(new CorrelationBroadcast(), ["Alpha"]);
        var captured = await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        captured.Context.CorrelationId.ShouldBeNull();
        captured.AmbientCorrelationId.ShouldBeNull();
        await host.StopAsync();
    }

    public sealed record TestBroadcast(string Value);

    public sealed record CorrelationBroadcast;

    public sealed record CorrelationCapture(
        BroadcastContext Context,
        string AmbientCorrelationId
    );

    public sealed class TestBroadcastHandler(TaskCompletionSource<TestBroadcast> handled)
        : IBroadcastHandler<TestBroadcast>
    {
        public Task HandleAsync(
            TestBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        )
        {
            handled.TrySetResult(payload);
            return Task.CompletedTask;
        }
    }

    public sealed class CorrelationBroadcastHandler(
        TaskCompletionSource<CorrelationCapture> handled
    ) : IBroadcastHandler<CorrelationBroadcast>
    {
        public Task HandleAsync(
            CorrelationBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        )
        {
            handled.TrySetResult(new(context, CorrelationId.Current));
            return Task.CompletedTask;
        }
    }
}