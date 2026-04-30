// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public abstract class EntityFrameworkMessageBrokerTestsBase
{
    protected abstract EntityFrameworkMessageBrokerTestSupport Support { get; }

    [Fact]
    public virtual async Task Publish_WithSubscriptions_PersistsMessageAndHandlerStates()
    {
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var message = new RelationalBrokerMessage("John");
        message.Properties["tenant"] = "test";

        await broker.Subscribe<RelationalBrokerMessage, PersistedRelationalBrokerMessageHandler>();
        await broker.Subscribe<RelationalBrokerMessage, AnotherPersistedRelationalBrokerMessageHandler>();

        await broker.Publish(message, CancellationToken.None);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        stored.MessageId.ShouldBe(message.MessageId);
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.HandlerStates.Count.ShouldBe(2);
        stored.HandlerStates.All(state => state.Status == BrokerMessageHandlerStatus.Pending).ShouldBeTrue();
        stored.Properties["tenant"].ToString().ShouldBe("test");
    }

    [Fact]
    public virtual async Task ProcessAsync_WithPendingMessage_ExecutesHandlerAndMarksMessageSucceeded()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var worker = this.CreateWorker(broker, options);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("process-me"), CancellationToken.None);

        await worker.ProcessAsync(CancellationToken.None);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        ProcessingRelationalBrokerMessageHandler.Processed.ShouldBeTrue();
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.ProcessedDate.ShouldNotBeNull();
        stored.HandlerStates.Single().Status.ShouldBe(BrokerMessageHandlerStatus.Succeeded);
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenAutoArchiveAfterIsReached_ArchivesTerminalMessage()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        options.AutoArchiveAfter = TimeSpan.Zero;
        var broker = this.CreateBroker(options);
        var worker = this.CreateWorker(broker, options);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("archive-me"), CancellationToken.None);

        await worker.ProcessAsync(CancellationToken.None);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.IsArchived.ShouldBeTrue();
        stored.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public virtual async Task RetryMessageHandlerAsync_ResetsOnlyRequestedHandler()
    {
        var message = new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = "msg-2",
            Type = typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort(),
            Content = "{}",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = BrokerMessageStatus.DeadLettered,
            HandlerStates =
            [
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = $"{typeof(RelationalBrokerMessage).PrettyName(false)}:{typeof(FailingRelationalBrokerMessageHandler).FullName}",
                    HandlerType = typeof(FailingRelationalBrokerMessageHandler).FullName,
                    Status = BrokerMessageHandlerStatus.DeadLettered,
                    AttemptCount = 3,
                    LastError = "boom",
                    ProcessedDate = DateTimeOffset.UtcNow
                },
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = $"{typeof(RelationalBrokerMessage).PrettyName(false)}:{typeof(ProcessingRelationalBrokerMessageHandler).FullName}",
                    HandlerType = typeof(ProcessingRelationalBrokerMessageHandler).FullName,
                    Status = BrokerMessageHandlerStatus.Succeeded,
                    AttemptCount = 1,
                    ProcessedDate = DateTimeOffset.UtcNow
                }
            ]
        };

        await this.Support.ExecuteDbContextAsync(async context =>
        {
            context.BrokerMessages.Add(message);
            await context.SaveChangesAsync();
        });

        var brokerService = this.CreateBrokerService();

        await brokerService.RetryMessageHandlerAsync(message.Id, typeof(FailingRelationalBrokerMessageHandler).FullName);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.HandlerStates.Single(handler => handler.HandlerType == typeof(FailingRelationalBrokerMessageHandler).FullName).Status.ShouldBe(BrokerMessageHandlerStatus.Pending);
        stored.HandlerStates.Single(handler => handler.HandlerType == typeof(FailingRelationalBrokerMessageHandler).FullName).AttemptCount.ShouldBe(0);
        stored.HandlerStates.Single(handler => handler.HandlerType == typeof(ProcessingRelationalBrokerMessageHandler).FullName).Status.ShouldBe(BrokerMessageHandlerStatus.Succeeded);
    }

    [Fact]
    public virtual async Task RetryMessageAsync_WithExpiredMessage_ExtendsExpirationAndResetsState()
    {
        var createdDate = DateTimeOffset.UtcNow.AddMinutes(-10);
        var message = new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = "msg-expired",
            Type = typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort(),
            Content = "{}",
            CreatedDate = createdDate,
            ExpiresOn = createdDate.AddMinutes(5),
            Status = BrokerMessageStatus.Expired,
            HandlerStates =
            [
                new BrokerMessageHandlerState
                {
                    SubscriptionKey = $"{typeof(RelationalBrokerMessage).PrettyName(false)}:{typeof(FailingRelationalBrokerMessageHandler).FullName}",
                    HandlerType = typeof(FailingRelationalBrokerMessageHandler).FullName,
                    Status = BrokerMessageHandlerStatus.Expired,
                    AttemptCount = 2,
                    LastError = "expired",
                    ProcessedDate = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ]
        };

        await this.Support.ExecuteDbContextAsync(async context =>
        {
            context.BrokerMessages.Add(message);
            await context.SaveChangesAsync();
        });

        var brokerService = this.CreateBrokerService();

        await brokerService.RetryMessageAsync(message.Id);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.ProcessedDate.ShouldBeNull();
        stored.ExpiresOn.ShouldNotBeNull();
        stored.ExpiresOn.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        stored.HandlerStates.Single().Status.ShouldBe(BrokerMessageHandlerStatus.Pending);
        stored.HandlerStates.Single().AttemptCount.ShouldBe(0);
    }

    [Fact]
    public virtual async Task ProcessAsync_WithCompetingWorkers_OnlyOneLeaseIsAcquired()
    {
        CoordinatedRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        options.LeaseDuration = TimeSpan.FromSeconds(10);
        options.LeaseRenewalInterval = TimeSpan.Zero;
        var broker = this.CreateBroker(options);
        var worker1 = this.CreateWorker(broker, options);
        var worker2 = this.CreateWorker(broker, options);

        await broker.Subscribe<RelationalBrokerMessage, CoordinatedRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("worker-race"), CancellationToken.None);

        var task1 = worker1.ProcessAsync(CancellationToken.None);
        var task2 = worker2.ProcessAsync(CancellationToken.None);
        await CoordinatedRelationalBrokerMessageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CoordinatedRelationalBrokerMessageHandler.Release();
        await Task.WhenAll(task1, task2);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());

        CoordinatedRelationalBrokerMessageHandler.ProcessCount.ShouldBe(1);
        stored.Status.ShouldBe(BrokerMessageStatus.Succeeded);
        stored.LockedBy.ShouldBeNull();
        stored.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenTypeIsPaused_MessageRemainsPending()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var controlState = new MessageBrokerControlState();
        var worker = this.CreateWorker(broker, options, controlState);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("paused"), CancellationToken.None);

        controlState.PauseMessageType(typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort());
        await worker.ProcessAsync(CancellationToken.None);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());
        ProcessingRelationalBrokerMessageHandler.Processed.ShouldBeFalse();
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.LockedBy.ShouldBeNull();
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenTypeIsResumed_ProcessesPreviouslyPausedMessage()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var controlState = new MessageBrokerControlState();
        var worker = this.CreateWorker(broker, options, controlState);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("paused-then-resumed"), CancellationToken.None);

        controlState.PauseMessageType(typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort());
        await worker.ProcessAsync(CancellationToken.None);

        var storedAfterPause = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());
        storedAfterPause.Status.ShouldBe(BrokerMessageStatus.Pending);
        ProcessingRelationalBrokerMessageHandler.Processed.ShouldBeFalse();

        controlState.ResumeMessageType(typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort());
        await worker.ProcessAsync(CancellationToken.None);

        var storedAfterResume = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());
        ProcessingRelationalBrokerMessageHandler.Processed.ShouldBeTrue();
        storedAfterResume.Status.ShouldBe(BrokerMessageStatus.Succeeded);
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenTypeIsPausedByShortName_MessageRemainsPending()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var controlState = new MessageBrokerControlState();
        var worker = this.CreateWorker(broker, options, controlState);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("paused-by-short-name"), CancellationToken.None);

        // Pause using PrettyName(false) — the short form that the UI/API uses
        controlState.PauseMessageType(typeof(RelationalBrokerMessage).PrettyName(false));
        await worker.ProcessAsync(CancellationToken.None);

        var stored = await this.Support.ExecuteDbContextAsync(context => context.BrokerMessages.SingleAsync());
        ProcessingRelationalBrokerMessageHandler.Processed.ShouldBeFalse();
        stored.Status.ShouldBe(BrokerMessageStatus.Pending);
        stored.LockedBy.ShouldBeNull();
    }

    [Fact]
    public virtual async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        ProcessingRelationalBrokerMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var controlState = new MessageBrokerControlState();
        var worker = this.CreateWorker(broker, options, controlState);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Publish(new RelationalBrokerMessage("msg1"), CancellationToken.None);
        await broker.Publish(new RelationalBrokerMessage("msg2"), CancellationToken.None);

        await worker.ProcessAsync(CancellationToken.None);

        var brokerService = this.CreateBrokerService(controlState);
        var summary = await brokerService.GetSummaryAsync();

        summary.Total.ShouldBe(2);
        summary.Succeeded.ShouldBe(2);
        summary.Capabilities.SupportsDurableStorage.ShouldBeTrue();
        summary.Capabilities.SupportsPauseResume.ShouldBeTrue();
    }

    [Fact]
    public virtual async Task GetSubscriptionsAsync_ReturnsRegisteredSubscriptions()
    {
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        await broker.Subscribe<RelationalBrokerMessage, AnotherPersistedRelationalBrokerMessageHandler>();

        // Populate the static subscriptions list (normally done at DI time via WithSubscription)
        ServiceCollectionMessagingExtensions.Subscriptions.Add((typeof(RelationalBrokerMessage), typeof(ProcessingRelationalBrokerMessageHandler)));
        ServiceCollectionMessagingExtensions.Subscriptions.Add((typeof(RelationalBrokerMessage), typeof(AnotherPersistedRelationalBrokerMessageHandler)));

        try
        {
            var brokerService = this.CreateBrokerService();
            var subscriptions = (await brokerService.GetSubscriptionsAsync()).ToList();

            subscriptions.ShouldContain(s => s.HandlerType == typeof(ProcessingRelationalBrokerMessageHandler).FullName);
            subscriptions.ShouldContain(s => s.HandlerType == typeof(AnotherPersistedRelationalBrokerMessageHandler).FullName);
        }
        finally
        {
            // Clean up static state
            ServiceCollectionMessagingExtensions.Subscriptions.RemoveAll(s =>
                s.handler == typeof(ProcessingRelationalBrokerMessageHandler) ||
                s.handler == typeof(AnotherPersistedRelationalBrokerMessageHandler));
        }
    }

    [Fact]
    public virtual async Task GetSubscriptionsAsync_ReflectsPausedState()
    {
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var controlState = new MessageBrokerControlState();

        await broker.Subscribe<RelationalBrokerMessage, ProcessingRelationalBrokerMessageHandler>();
        ServiceCollectionMessagingExtensions.Subscriptions.Add((typeof(RelationalBrokerMessage), typeof(ProcessingRelationalBrokerMessageHandler)));

        try
        {
            var brokerService = this.CreateBrokerService(controlState);

            // Pause using PrettyName(false) — the short form that the UI/API uses
            controlState.PauseMessageType(typeof(RelationalBrokerMessage).PrettyName(false));
            var subscriptions = (await brokerService.GetSubscriptionsAsync()).ToList();
            subscriptions.ShouldContain(s => s.IsMessageTypePaused == true);

            // Resume and pause using AssemblyQualifiedNameShort() — also supported
            controlState.ResumeMessageType(typeof(RelationalBrokerMessage).PrettyName(false));
            controlState.PauseMessageType(typeof(RelationalBrokerMessage).AssemblyQualifiedNameShort());
            subscriptions = (await brokerService.GetSubscriptionsAsync()).ToList();
            subscriptions.ShouldContain(s => s.IsMessageTypePaused == true);
        }
        finally
        {
            ServiceCollectionMessagingExtensions.Subscriptions.RemoveAll(s =>
                s.handler == typeof(ProcessingRelationalBrokerMessageHandler));
        }
    }

    [Fact]
    public virtual async Task GetWaitingMessagesAsync_ReturnsMessagesWithNoHandlers()
    {
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);

        // Do NOT subscribe before publishing
        await broker.Publish(new RelationalBrokerMessage("waiting-msg"), CancellationToken.None);

        var brokerService = this.CreateBrokerService();
        var waiting = (await brokerService.GetWaitingMessagesAsync()).ToList();

        waiting.Count.ShouldBe(1);
        waiting[0].MessageId.ShouldNotBeNull();
    }

    private EntityFrameworkMessageBroker<BrokerMessageTestDbContext> CreateBroker(EntityFrameworkMessageBrokerOptions options)
    {
        return new EntityFrameworkMessageBroker<BrokerMessageTestDbContext>(
            this.Support.ServiceProvider,
            options);
    }

    private EntityFrameworkMessageBrokerWorker<BrokerMessageTestDbContext> CreateWorker(
        EntityFrameworkMessageBroker<BrokerMessageTestDbContext> broker,
        EntityFrameworkMessageBrokerOptions options,
        MessageBrokerControlState controlState = null)
    {
        return new EntityFrameworkMessageBrokerWorker<BrokerMessageTestDbContext>(
            this.Support.LoggerFactory,
            this.Support.ServiceProvider,
            broker,
            options,
            controlState);
    }

    private EntityFrameworkMessageBrokerStoreService<BrokerMessageTestDbContext> CreateBrokerService(MessageBrokerControlState controlState = null)
    {
        return new EntityFrameworkMessageBrokerStoreService<BrokerMessageTestDbContext>(this.Support.ServiceProvider, controlState ?? new MessageBrokerControlState());
    }

    private EntityFrameworkMessageBrokerOptions CreateOptions(IMessageHandlerFactory handlerFactory = null)
    {
        return new EntityFrameworkMessageBrokerOptions
        {
            LoggerFactory = this.Support.LoggerFactory,
            HandlerFactory = handlerFactory ?? new MessageBrokerTestHandlerFactory(),
            Serializer = new SystemTextJsonSerializer(),
            AutoSave = true,
            StartupDelay = TimeSpan.Zero,
            ProcessingInterval = TimeSpan.FromMilliseconds(50),
            ProcessingDelay = TimeSpan.Zero,
            ProcessingCount = 10,
            LeaseDuration = TimeSpan.FromSeconds(30),
            LeaseRenewalInterval = TimeSpan.FromSeconds(10),
            MaxDeliveryAttempts = 3,
            MessageExpiration = TimeSpan.FromMinutes(5)
        };
    }
}

public sealed class EntityFrameworkMessageBrokerTestSupport : IDisposable
{
    public EntityFrameworkMessageBrokerTestSupport(
        ITestOutputHelper output,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        this.LoggerFactory = XunitLoggerFactory.Create(output);

        var services = new ServiceCollection();
        services.AddSingleton(this.LoggerFactory);
        services.AddDbContext<BrokerMessageTestDbContext>(configureDbContext);

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerMessageTestDbContext>();
        EnsureBrokerTablesCreated(dbContext);
        dbContext.BrokerMessages.RemoveRange(dbContext.BrokerMessages);
        dbContext.SaveChanges();
    }

    public ILoggerFactory LoggerFactory { get; }

    public ServiceProvider ServiceProvider { get; }

    public async Task ExecuteDbContextAsync(Func<BrokerMessageTestDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerMessageTestDbContext>();
        await action(dbContext);
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<BrokerMessageTestDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BrokerMessageTestDbContext>();
        return await action(dbContext);
    }

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.LoggerFactory.Dispose();
    }

    private static void EnsureBrokerTablesCreated(BrokerMessageTestDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (TableExists(dbContext, dbContext.Model.FindEntityType(typeof(BrokerMessage))?.GetTableName()))
        {
            return;
        }

        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    private static bool TableExists(DbContext dbContext, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                string name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.tables WHERE name = @name",
                string name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = @name",
                string name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name",
                _ => throw new InvalidOperationException($"Unsupported provider '{providerName}' for broker table checks.")
            };

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return command.ExecuteScalar() is not null and not DBNull;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }
}

public class BrokerMessageTestDbContext(DbContextOptions<BrokerMessageTestDbContext> options) : DbContext(options), IMessagingContext
{
    public DbSet<BrokerMessage> BrokerMessages { get; set; }
}

public sealed class RelationalBrokerMessage(string value) : MessageBase
{
    public string Value { get; } = value;
}

public sealed class PersistedRelationalBrokerMessageHandler : IMessageHandler<RelationalBrokerMessage>
{
    public Task Handle(RelationalBrokerMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class AnotherPersistedRelationalBrokerMessageHandler : IMessageHandler<RelationalBrokerMessage>
{
    public Task Handle(RelationalBrokerMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class ProcessingRelationalBrokerMessageHandler : IMessageHandler<RelationalBrokerMessage>
{
    public static bool Processed { get; private set; }

    public static void Reset()
    {
        Processed = false;
    }

    public Task Handle(RelationalBrokerMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        return Task.CompletedTask;
    }
}

public sealed class CoordinatedRelationalBrokerMessageHandler : IMessageHandler<RelationalBrokerMessage>
{
    private static int processCount;

    public static int ProcessCount => processCount;

    public static TaskCompletionSource<bool> Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<bool> ContinueProcessing { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    public async Task Handle(RelationalBrokerMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref processCount);
        Started.TrySetResult(true);
        await ContinueProcessing.Task.WaitAsync(cancellationToken);
    }
}

public sealed class FailingRelationalBrokerMessageHandler : IMessageHandler<RelationalBrokerMessage>
{
    public Task Handle(RelationalBrokerMessage message, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("simulated handler failure");
    }
}

public sealed class MessageBrokerTestHandlerFactory : IMessageHandlerFactory
{
    public MessageHandlerFactoryResult Create(Type messageHandlerType)
    {
        if (messageHandlerType == typeof(PersistedRelationalBrokerMessageHandler))
        {
            return MessageHandlerFactoryResult.Create(new PersistedRelationalBrokerMessageHandler());
        }

        if (messageHandlerType == typeof(AnotherPersistedRelationalBrokerMessageHandler))
        {
            return MessageHandlerFactoryResult.Create(new AnotherPersistedRelationalBrokerMessageHandler());
        }

        if (messageHandlerType == typeof(ProcessingRelationalBrokerMessageHandler))
        {
            return MessageHandlerFactoryResult.Create(new ProcessingRelationalBrokerMessageHandler());
        }

        if (messageHandlerType == typeof(CoordinatedRelationalBrokerMessageHandler))
        {
            return MessageHandlerFactoryResult.Create(new CoordinatedRelationalBrokerMessageHandler());
        }

        if (messageHandlerType == typeof(FailingRelationalBrokerMessageHandler))
        {
            return MessageHandlerFactoryResult.Create(new FailingRelationalBrokerMessageHandler());
        }

        return null;
    }
}
