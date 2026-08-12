// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ProfilingBroadcastServiceTests
{
    [Fact]
    public void BroadcastService_PublicContract_RemainsStandalone()
    {
        // Act
        var methods = typeof(IBroadcastService)
            .GetMethods()
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Assert
        methods.ShouldBe([nameof(IBroadcastService.PublishAsync)]);
    }

    [Fact]
    public async Task PublishAsync_LateRegistration_TargetsProfilingPreparedSetOnly()
    {
        // Arrange
        var handled = new TaskCompletionSource<TestProfilingBroadcast>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(handled);
                services
                    .AddBroadcasting(options => options.NodeIdentity("node-a"))
                    .AddHandler<TestProfilingBroadcast, TestProfilingBroadcastHandler>();
                services.AddProfiling(options => options.Enabled());
            })
            .Build();
        await host.StartAsync();
        var sut = host.Services.GetRequiredService<IProfilingBroadcastService>();
        var registry = host.Services.GetRequiredService<IBroadcastRegistryStore>();
        var prepared = await sut.PrepareTargetsAsync();
        var now = DateTimeOffset.UtcNow;
        await registry.UpsertAsync(
            new(
                "node-b",
                new Uri("https://node-b.test"),
                [BroadcastingOptions.DefaultScope],
                now,
                now,
                null
            )
        );

        // Act
        var result = await sut.PublishAsync(new TestProfilingBroadcast("fixed"), prepared.Value);
        var received = await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        prepared.IsSuccess.ShouldBeTrue();
        prepared.Value.TargetCount.ShouldBe(1);
        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetCount.ShouldBe(1);
        result.Value.Nodes.ShouldHaveSingleItem().NodeIdentity.ShouldBe("node-a");
        received.Value.ShouldBe("fixed");
        (await registry.ListAsync()).Count.ShouldBe(2);
        await host.StopAsync();
    }

    public sealed record TestProfilingBroadcast(string Value) : IProfilingBroadcast;

    public sealed class TestProfilingBroadcastHandler(
        TaskCompletionSource<TestProfilingBroadcast> handled
    ) : IBroadcastHandler<TestProfilingBroadcast>
    {
        public Task HandleAsync(
            TestProfilingBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        )
        {
            handled.TrySetResult(payload);
            return Task.CompletedTask;
        }
    }
}
