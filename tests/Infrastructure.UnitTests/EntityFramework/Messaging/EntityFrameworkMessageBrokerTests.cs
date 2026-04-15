// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Messaging;

using Application.Messaging;
using Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.DependencyInjection;

public class EntityFrameworkMessageBrokerTests(StubDbContextFixture fixture) : IClassFixture<StubDbContextFixture>
{
    private readonly StubDbContextFixture fixture = fixture;

    [Fact]
    public async Task Publish_WithSubscriptions_PersistsMessageAndHandlerStates()
    {
        // Arrange
        await this.ResetContext();
        var message = new StubMessage { FirstName = "John", LastName = "Doe" };
        message.Properties["tenant"] = "test";
        var sut = this.CreateSut();
        await sut.Subscribe<StubMessage, PersistedStubMessageHandler>();
        await sut.Subscribe<StubMessage, AnotherPersistedStubMessageHandler>();

        // Act
        await sut.Publish(message, CancellationToken.None);

        // Assert
        var stored = this.fixture.Context.BrokerMessages.Single();
        stored.MessageId.ShouldBe(message.MessageId);
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.HandlerStates.Count.ShouldBe(2);
        stored.HandlerStates.All(state => state.Status == BrokerMessageHandlerStatus.Pending).ShouldBeTrue();
        stored.Properties["tenant"].ShouldBe("test");
    }

    [Fact]
    public async Task Publish_WithoutSubscriptions_PersistsCompletedMessage()
    {
        // Arrange
        await this.ResetContext();
        var message = new StubMessage { FirstName = "Jane", LastName = "Doe" };
        var sut = this.CreateSut();

        // Act
        await sut.Publish(message, CancellationToken.None);

        // Assert
        var stored = this.fixture.Context.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.ProcessedDate.ShouldNotBeNull();
        stored.HandlerStates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithPendingMessage_ExecutesHandlerAndMarksMessageSucceeded()
    {
        // Arrange
        await this.ResetContext();
        ProcessingStubMessageHandler.Reset();
        var handlerFactory = new TestMessageHandlerFactory();
        var options = this.CreateOptions(handlerFactory);
        var broker = new EntityFrameworkMessageBroker<StubDbContext>(this.fixture.Context, options);
        await broker.Subscribe<StubMessage, ProcessingStubMessageHandler>();
        var worker = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            this.fixture.Context,
            broker,
            options);
        var message = new StubMessage { FirstName = "Joe", LastName = "Bloggs" };
        await broker.Publish(message, CancellationToken.None);

        // Act
        await worker.ProcessAsync(CancellationToken.None);

        // Assert
        ProcessingStubMessageHandler.Processed.ShouldBeTrue();
        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.ProcessedDate.ShouldNotBeNull();
        stored.HandlerStates.Single().Status.ShouldBe(BrokerMessageHandlerStatus.Succeeded);
    }

    [Fact]
    public async Task ProcessAsync_WithCompetingWorkers_OnlyOneLeaseIsAcquired()
    {
        // Arrange
        await this.ResetContext();
        CoordinatedProcessingStubMessageHandler.Reset();
        await using var provider = this.CreateProvider();
        var options = this.CreateOptions(new TestMessageHandlerFactory());
        var broker = new EntityFrameworkMessageBroker<StubDbContext>(provider, options);
        await broker.Subscribe<StubMessage, CoordinatedProcessingStubMessageHandler>();
        await broker.Publish(new StubMessage { FirstName = "Worker", LastName = "Race" }, CancellationToken.None);

        var worker1 = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            provider,
            broker,
            options);
        var worker2 = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            provider,
            broker,
            options);

        // Act
        var task1 = worker1.ProcessAsync(CancellationToken.None);
        var task2 = worker2.ProcessAsync(CancellationToken.None);
        await CoordinatedProcessingStubMessageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CoordinatedProcessingStubMessageHandler.Release();
        await Task.WhenAll(task1, task2);

        // Assert
        CoordinatedProcessingStubMessageHandler.ProcessCount.ShouldBe(1);
        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.LockedBy.ShouldBeNull();
        stored.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WithLongRunningHandler_RenewsLease()
    {
        // Arrange
        await this.ResetContext();
        CoordinatedLeaseRenewalStubMessageHandler.Reset();
        await using var provider = this.CreateProvider();
        var options = this.CreateOptions(new TestMessageHandlerFactory());
        options.LeaseDuration = TimeSpan.FromMilliseconds(80);
        options.LeaseRenewalInterval = TimeSpan.FromMilliseconds(20);
        var broker = new EntityFrameworkMessageBroker<StubDbContext>(provider, options);
        await broker.Subscribe<StubMessage, CoordinatedLeaseRenewalStubMessageHandler>();
        await broker.Publish(new StubMessage { FirstName = "Lease", LastName = "Renewal" }, CancellationToken.None);
        var worker = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            provider,
            broker,
            options);

        // Act
        var processingTask = worker.ProcessAsync(CancellationToken.None);
        await CoordinatedLeaseRenewalStubMessageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);

        // Assert during processing
        using (var inspectContext = this.fixture.CreateContext())
        {
            var storedDuringProcessing = inspectContext.BrokerMessages.Single();
            storedDuringProcessing.LockedBy.ShouldNotBeNullOrWhiteSpace();
            storedDuringProcessing.LockedUntil.ShouldNotBeNull();
            storedDuringProcessing.LockedUntil.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        }

        CoordinatedLeaseRenewalStubMessageHandler.Release();
        await processingTask;

        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.LockedBy.ShouldBeNull();
        stored.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WithRetryableHandlerFailure_LeavesMessagePendingForRetry()
    {
        // Arrange
        await this.ResetContext();
        var options = this.CreateOptions(new TestMessageHandlerFactory());
        options.MaxDeliveryAttempts = 3;
        var broker = new EntityFrameworkMessageBroker<StubDbContext>(this.fixture.Context, options);
        await broker.Subscribe<StubMessage, FailingStubMessageHandler>();
        var worker = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            this.fixture.Context,
            broker,
            options);
        await broker.Publish(new StubMessage { FirstName = "Retry", LastName = "Pending" }, CancellationToken.None);

        // Act
        await worker.ProcessAsync(CancellationToken.None);

        // Assert
        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.ProcessedDate.ShouldBeNull();
        stored.HandlerStates.Single().Status.ShouldBe(BrokerMessageHandlerStatus.Failed);
        stored.HandlerStates.Single().AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_WithLongRunningHandler_UsingOwnedContext_RenewsLease()
    {
        // Arrange
        await this.ResetContext();
        CoordinatedLeaseRenewalStubMessageHandler.Reset();
        var options = this.CreateOptions(new TestMessageHandlerFactory());
        options.LeaseDuration = TimeSpan.FromMilliseconds(80);
        options.LeaseRenewalInterval = TimeSpan.FromMilliseconds(20);
        var broker = new EntityFrameworkMessageBroker<StubDbContext>(this.fixture.Context, options);
        await broker.Subscribe<StubMessage, CoordinatedLeaseRenewalStubMessageHandler>();
        await broker.Publish(new StubMessage { FirstName = "Owned", LastName = "Context" }, CancellationToken.None);
        var worker = new EntityFrameworkMessageBrokerWorker<StubDbContext>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            this.fixture.Context,
            broker,
            options);

        // Act
        var processingTask = worker.ProcessAsync(CancellationToken.None);
        await CoordinatedLeaseRenewalStubMessageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);

        // Assert during processing
        using (var inspectContext = this.fixture.CreateContext())
        {
            var storedDuringProcessing = inspectContext.BrokerMessages.Single();
            storedDuringProcessing.LockedBy.ShouldNotBeNullOrWhiteSpace();
            storedDuringProcessing.LockedUntil.ShouldNotBeNull();
            storedDuringProcessing.LockedUntil.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        }

        CoordinatedLeaseRenewalStubMessageHandler.Release();
        await processingTask;

        using var assertContext = this.fixture.CreateContext();
        var stored = assertContext.BrokerMessages.Single();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.LockedBy.ShouldBeNull();
        stored.LockedUntil.ShouldBeNull();
    }

    private EntityFrameworkMessageBroker<StubDbContext> CreateSut()
    {
        var options = this.CreateOptions(Substitute.For<IMessageHandlerFactory>());

        return new EntityFrameworkMessageBroker<StubDbContext>(this.fixture.Context, options);
    }

    private EntityFrameworkMessageBrokerOptions CreateOptions(IMessageHandlerFactory handlerFactory)
    {
        return new EntityFrameworkMessageBrokerOptions
        {
            LoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            HandlerFactory = handlerFactory,
            Serializer = new SystemTextJsonSerializer(),
            MaxDeliveryAttempts = 2,
            LeaseDuration = TimeSpan.FromSeconds(1),
            LeaseRenewalInterval = TimeSpan.FromMilliseconds(100)
        };
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => this.fixture.CreateContext());

        return services.BuildServiceProvider();
    }

    private async Task ResetContext()
    {
        this.fixture.Context.ChangeTracker.Clear();

        using var context = this.fixture.CreateContext();
        context.BrokerMessages.RemoveRange(context.BrokerMessages.ToList());
        await context.SaveChangesAsync();
    }

    private class PersistedStubMessageHandler : IMessageHandler<StubMessage>
    {
        public Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherPersistedStubMessageHandler : IMessageHandler<StubMessage>
    {
        public Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class ProcessingStubMessageHandler : IMessageHandler<StubMessage>
    {
        public static bool Processed { get; private set; }

        public static void Reset()
        {
            Processed = false;
        }

        public Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            Processed = true;
            return Task.CompletedTask;
        }
    }

    private class CoordinatedProcessingStubMessageHandler : IMessageHandler<StubMessage>
    {
        public static int ProcessCount => processCount;

        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static TaskCompletionSource<bool> ContinueProcessing { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static int processCount;

        public static void Reset()
        {
            processCount = 0;
            Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ContinueProcessing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public static void Release()
        {
            ContinueProcessing.TrySetResult(true);
        }

        public async Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref processCount);
            Started.TrySetResult(true);
            await ContinueProcessing.Task.WaitAsync(cancellationToken);
        }
    }

    private class CoordinatedLeaseRenewalStubMessageHandler : IMessageHandler<StubMessage>
    {
        public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static TaskCompletionSource<bool> ContinueProcessing { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset()
        {
            Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ContinueProcessing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public static void Release()
        {
            ContinueProcessing.TrySetResult(true);
        }

        public async Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await ContinueProcessing.Task.WaitAsync(cancellationToken);
        }
    }

    private class FailingStubMessageHandler : IMessageHandler<StubMessage>
    {
        public Task Handle(StubMessage message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("simulated handler failure");
        }
    }

    private class TestMessageHandlerFactory : IMessageHandlerFactory
    {
        public object Create(Type messageHandlerType)
        {
            if (messageHandlerType == typeof(ProcessingStubMessageHandler))
            {
                return new ProcessingStubMessageHandler();
            }

            if (messageHandlerType == typeof(CoordinatedProcessingStubMessageHandler))
            {
                return new CoordinatedProcessingStubMessageHandler();
            }

            if (messageHandlerType == typeof(CoordinatedLeaseRenewalStubMessageHandler))
            {
                return new CoordinatedLeaseRenewalStubMessageHandler();
            }

            if (messageHandlerType == typeof(FailingStubMessageHandler))
            {
                return new FailingStubMessageHandler();
            }

            return null;
        }
    }
}