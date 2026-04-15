// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using System.Diagnostics;
using Application.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Application")]
public class MessageBrokerProcessingTests(ITestOutputHelper output) : TestsBase(output,
    services =>
    {
        services.AddMessaging()
            .WithInProcessBroker(new InProcessMessageBrokerConfiguration
            {
                ProcessDelay = 0,
                MessageExpiration = new TimeSpan(0, 1, 0)
            });
    })
{
    [Fact]
    public async Task Publish_WhenHandlerIsDelayed_WaitsForHandlerCompletion()
    {
        // Arrange
        DelayedProcessingHandler.Reset();
        var message = new DelayedProcessingMessage("wait-for-handler");
        var sut = this.ServiceProvider.GetRequiredService<IMessageBroker>();
        await sut.Subscribe<DelayedProcessingMessage, DelayedProcessingHandler>();
        var watch = Stopwatch.StartNew();

        // Act
        await sut.Publish(message, CancellationToken.None);
        watch.Stop();

        // Assert
        DelayedProcessingHandler.Processed.ShouldBeTrue();
        DelayedProcessingHandler.ProcessedAt.ShouldNotBeNull();
        watch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(150);
    }

    [Fact]
    public async Task Publish_WhenHandlerFails_StopsRemainingHandlers()
    {
        // Arrange
        FailingProcessingHandler.Reset();
        SkippedProcessingHandler.Reset();
        var message = new DelayedProcessingMessage("stop-after-failure");
        var sut = this.ServiceProvider.GetRequiredService<IMessageBroker>();
        await sut.Subscribe<DelayedProcessingMessage, FailingProcessingHandler>();
        await sut.Subscribe<DelayedProcessingMessage, SkippedProcessingHandler>();

        // Act
        await sut.Publish(message, CancellationToken.None);

        // Assert
        FailingProcessingHandler.Attempts.ShouldBe(1);
        SkippedProcessingHandler.Processed.ShouldBeFalse();
    }
}

public class DelayedProcessingMessage(string value) : MessageBase
{
    public string Value { get; } = value;
}

public class DelayedProcessingHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<DelayedProcessingMessage>(loggerFactory)
{
    public static bool Processed { get; private set; }

    public static DateTimeOffset? ProcessedAt { get; private set; }

    public static void Reset()
    {
        Processed = false;
        ProcessedAt = null;
    }

    public override async Task Handle(DelayedProcessingMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        Processed = true;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}

public class FailingProcessingHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<DelayedProcessingMessage>(loggerFactory)
{
    public static int Attempts { get; private set; }

    public static void Reset()
    {
        Attempts = 0;
    }

    public override Task Handle(DelayedProcessingMessage message, CancellationToken cancellationToken)
    {
        Attempts++;

        throw new InvalidOperationException("boom");
    }
}

public class SkippedProcessingHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<DelayedProcessingMessage>(loggerFactory)
{
    public static bool Processed { get; private set; }

    public static void Reset()
    {
        Processed = false;
    }

    public override Task Handle(DelayedProcessingMessage message, CancellationToken cancellationToken)
    {
        Processed = true;

        return Task.CompletedTask;
    }
}