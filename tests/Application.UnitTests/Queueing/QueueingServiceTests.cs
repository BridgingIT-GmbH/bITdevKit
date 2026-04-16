// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

public class QueueingServiceTests
{
    [Fact]
    public async Task StartAsync_WhenApplicationStarts_AppliesSubscriptionsAndRunsBackgroundProcessor()
    {
        // Arrange
        var broker = Substitute.For<IQueueBroker>();
        var backgroundProcessor = Substitute.For<IQueueBrokerBackgroundProcessor>();
        backgroundProcessor.RunAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(broker);
        services.AddSingleton(backgroundProcessor);
        using var provider = services.BuildServiceProvider();

        var registrationStore = new QueueingRegistrationStore();
        registrationStore.Add(typeof(TestQueueMessage), typeof(TestQueueMessageHandler));

        var applicationLifetime = new TestHostApplicationLifetime();
        var sut = new QueueingService(
            NullLoggerFactory.Instance,
            applicationLifetime,
            provider,
            registrationStore,
            new QueueingOptions { Enabled = true, StartupDelay = TimeSpan.Zero });

        // Act
        await sut.StartAsync(CancellationToken.None);
        applicationLifetime.NotifyStarted();

        var started = await WaitForAsync(async () =>
        {
            try
            {
                await broker.Received(1).Subscribe(typeof(TestQueueMessage), typeof(TestQueueMessageHandler));
                await backgroundProcessor.Received(1).RunAsync(Arg.Any<CancellationToken>());
                return true;
            }
            catch
            {
                return false;
            }
        });

        await sut.StopAsync(CancellationToken.None);

        // Assert
        started.ShouldBeTrue();
        await broker.Received(1).Unsubscribe();
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, int attempts = 40, int delayMilliseconds = 25)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(delayMilliseconds);
        }

        return false;
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.started.Token;

        public CancellationToken ApplicationStopping => this.stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication()
        {
            this.stopping.Cancel();
            this.stopped.Cancel();
        }

        public void NotifyStarted()
        {
            this.started.Cancel();
        }
    }

    private sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }

    private sealed class TestQueueMessageHandler : IQueueMessageHandler<TestQueueMessage>
    {
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}