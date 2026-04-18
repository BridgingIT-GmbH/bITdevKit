// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public abstract class EntityFrameworkQueueBrokerTestsBase
{
    protected abstract EntityFrameworkQueueBrokerTestSupport Support { get; }

    [Fact]
    public virtual async Task ProcessAsync_WhenWorkerRuns_ProcessesPersistedMessageSafely()
    {
        RelationalQueueMessageHandler.Reset();
        var options = this.CreateOptions();
        var broker = this.CreateBroker(options);
        var worker = this.CreateWorker(broker, options);
        var message = new RelationalQueueMessage("persist-me");

        await broker.Subscribe<RelationalQueueMessage, RelationalQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        var storedMessage = await this.Support.ExecuteDbContextAsync(context =>
            context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId));

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.AttemptCount.ShouldBe(1);
        storedMessage.LockedBy.ShouldBeNull();
        storedMessage.LockedUntil.ShouldBeNull();
        storedMessage.RegisteredHandlerType.ShouldBe(typeof(RelationalQueueMessageHandler).FullName);
        RelationalQueueMessageHandler.Processed.ShouldBeTrue();
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenAutoArchiveAfterIsReached_ArchivesTerminalMessage()
    {
        RelationalQueueMessageHandler.Reset();
        var options = this.CreateOptions();
        options.AutoArchiveAfter = TimeSpan.Zero;
        var broker = this.CreateBroker(options);
        var worker = this.CreateWorker(broker, options);
        var message = new RelationalQueueMessage("archive-me");

        await broker.Subscribe<RelationalQueueMessage, RelationalQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        var storedMessage = await this.Support.ExecuteDbContextAsync(context =>
            context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId));

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.IsArchived.ShouldBeTrue();
        storedMessage.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public virtual async Task ProcessAsync_WhenAutoArchiveStatusDoesNotMatch_LeavesTerminalMessageActive()
    {
        RelationalQueueMessageHandler.Reset();
        var options = this.CreateOptions();
        options.AutoArchiveAfter = TimeSpan.Zero;
        options.AutoArchiveStatuses = [QueueMessageStatus.DeadLettered];
        var broker = this.CreateBroker(options);
        var worker = this.CreateWorker(broker, options);
        var message = new RelationalQueueMessage("stay-active");

        await broker.Subscribe<RelationalQueueMessage, RelationalQueueMessageHandler>();

        await broker.EnqueueAndWait(message);
        await worker.ProcessAsync();

        var storedMessage = await this.Support.ExecuteDbContextAsync(context =>
            context.QueueMessages.SingleAsync(item => item.MessageId == message.MessageId));

        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.IsArchived.ShouldBeFalse();
        storedMessage.ArchivedDate.ShouldBeNull();
    }

    [Fact]
    public virtual async Task GetWaitingMessagesAsync_ReturnsOldestMessagesFirst()
    {
        var options = this.CreateOptions();

        await this.Support.ExecuteDbContextAsync(async context =>
        {
            context.QueueMessages.AddRange(
            [
                this.CreateStoredQueueMessage(options, "older-message", QueueMessageStatus.WaitingForHandler, DateTimeOffset.UtcNow.AddMinutes(-5)),
                this.CreateStoredQueueMessage(options, "newer-message", QueueMessageStatus.WaitingForHandler, DateTimeOffset.UtcNow.AddMinutes(-1))
            ]);

            await context.SaveChangesAsync();
        });

        var brokerService = this.CreateBrokerService(options);
        var messages = (await brokerService.GetWaitingMessagesAsync()).ToList();

        messages.Select(item => item.MessageId).ToArray().ShouldBe(["older-message", "newer-message"]);
    }

    [Fact]
    public virtual async Task RetryMessageAsync_WhenRetryingArchivedMessage_ClearsOperationalState()
    {
        var options = this.CreateOptions();
        var brokerService = this.CreateBrokerService(options);
        var message = this.CreateStoredQueueMessage(options, "retry-me", QueueMessageStatus.DeadLettered, DateTimeOffset.UtcNow.AddMinutes(-10));
        message.AttemptCount = 3;
        message.RegisteredHandlerType = typeof(RelationalQueueMessageHandler).FullName;
        message.LastError = "boom";
        message.ProcessedDate = DateTimeOffset.UtcNow.AddMinutes(-5);
        message.ProcessingStartedDate = DateTimeOffset.UtcNow.AddMinutes(-6);
        message.LockedBy = "worker-1";
        message.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        message.IsArchived = true;
        message.ArchivedDate = DateTimeOffset.UtcNow.AddMinutes(-4);

        await this.Support.ExecuteDbContextAsync(async context =>
        {
            context.QueueMessages.Add(message);
            await context.SaveChangesAsync();
        });

        await brokerService.RetryMessageAsync(message.Id);

        var storedMessage = await this.Support.ExecuteDbContextAsync(context =>
            context.QueueMessages.SingleAsync(item => item.Id == message.Id));

        storedMessage.Status.ShouldBe(QueueMessageStatus.Pending);
        storedMessage.AttemptCount.ShouldBe(0);
        storedMessage.RegisteredHandlerType.ShouldBeNull();
        storedMessage.LastError.ShouldBeNull();
        storedMessage.ProcessedDate.ShouldBeNull();
        storedMessage.ProcessingStartedDate.ShouldBeNull();
        storedMessage.LockedBy.ShouldBeNull();
        storedMessage.LockedUntil.ShouldBeNull();
        storedMessage.IsArchived.ShouldBeFalse();
        storedMessage.ArchivedDate.ShouldBeNull();
    }

    [Fact]
    public virtual async Task ProcessAsync_WithCompetingWorkers_OnlyOneLeaseIsAcquired()
    {
        RelationalCoordinatedQueueMessageHandler.Reset();
        var options = this.CreateOptions();
        options.LeaseDuration = TimeSpan.FromSeconds(10);
        options.LeaseRenewalInterval = TimeSpan.Zero;
        var broker = this.CreateBroker(options);
        var worker1 = this.CreateWorker(broker, options);
        var worker2 = this.CreateWorker(broker, options);

        await broker.Subscribe<RelationalQueueMessage, RelationalCoordinatedQueueMessageHandler>();
        await broker.EnqueueAndWait(new RelationalQueueMessage("race"));

        var task1 = worker1.ProcessAsync();
        var task2 = worker2.ProcessAsync();
        await RelationalCoordinatedQueueMessageHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        RelationalCoordinatedQueueMessageHandler.Release();
        await Task.WhenAll(task1, task2);

        var storedMessage = await this.Support.ExecuteDbContextAsync(context => context.QueueMessages.SingleAsync());

        RelationalCoordinatedQueueMessageHandler.ProcessCount.ShouldBe(1);
        storedMessage.Status.ShouldBe(QueueMessageStatus.Succeeded, storedMessage.LastError);
        storedMessage.LockedBy.ShouldBeNull();
        storedMessage.LockedUntil.ShouldBeNull();
    }

    private EntityFrameworkQueueBroker<QueueBrokerTestDbContext> CreateBroker(
        EntityFrameworkQueueBrokerOptions options,
        QueueBrokerControlState controlState = null)
    {
        return new EntityFrameworkQueueBroker<QueueBrokerTestDbContext>(
            this.Support.ServiceProvider,
            options,
            controlState ?? new QueueBrokerControlState());
    }

    private EntityFrameworkQueueBrokerWorker<QueueBrokerTestDbContext> CreateWorker(
        EntityFrameworkQueueBroker<QueueBrokerTestDbContext> broker,
        EntityFrameworkQueueBrokerOptions options)
    {
        return new EntityFrameworkQueueBrokerWorker<QueueBrokerTestDbContext>(
            this.Support.LoggerFactory,
            this.Support.ServiceProvider,
            broker,
            options);
    }

    private EntityFrameworkQueueBrokerService<QueueBrokerTestDbContext> CreateBrokerService(
        EntityFrameworkQueueBrokerOptions options,
        QueueingRegistrationStore registrationStore = null,
        QueueBrokerControlState controlState = null)
    {
        return new EntityFrameworkQueueBrokerService<QueueBrokerTestDbContext>(
            this.Support.ServiceProvider,
            options,
            registrationStore ?? new QueueingRegistrationStore(),
            controlState ?? new QueueBrokerControlState());
    }

    private EntityFrameworkQueueBrokerOptions CreateOptions(IQueueMessageHandlerFactory handlerFactory = null)
    {
        return new EntityFrameworkQueueBrokerOptions
        {
            LoggerFactory = this.Support.LoggerFactory,
            HandlerFactory = handlerFactory ?? new QueueBrokerTestHandlerFactory(),
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

    private QueueMessage CreateStoredQueueMessage(
        EntityFrameworkQueueBrokerOptions options,
        string messageId,
        QueueMessageStatus status,
        DateTimeOffset createdDate)
    {
        return new QueueMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            QueueName = this.CreateQueueName(options, typeof(RelationalQueueMessage)),
            Type = typeof(RelationalQueueMessage).PrettyName(false),
            Content = "{}",
            CreatedDate = createdDate,
            Status = status
        };
    }

    private string CreateQueueName(EntityFrameworkQueueBrokerOptions options, Type messageType)
    {
        return string.Concat(options.QueueNamePrefix, messageType.PrettyName(false), options.QueueNameSuffix);
    }
}

public sealed class EntityFrameworkQueueBrokerTestSupport : IDisposable
{
    public EntityFrameworkQueueBrokerTestSupport(
        ITestOutputHelper output,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        this.LoggerFactory = XunitLoggerFactory.Create(output);

        var services = new ServiceCollection();
        services.AddSingleton(this.LoggerFactory);
        services.AddDbContext<QueueBrokerTestDbContext>(configureDbContext);

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QueueBrokerTestDbContext>();
        EnsureQueueTablesCreated(dbContext);
        dbContext.QueueMessages.RemoveRange(dbContext.QueueMessages);
        dbContext.SaveChanges();
    }

    public ILoggerFactory LoggerFactory { get; }

    public ServiceProvider ServiceProvider { get; }

    public async Task ExecuteDbContextAsync(Func<QueueBrokerTestDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QueueBrokerTestDbContext>();
        await action(dbContext);
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<QueueBrokerTestDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QueueBrokerTestDbContext>();
        return await action(dbContext);
    }

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.LoggerFactory.Dispose();
    }

    private static void EnsureQueueTablesCreated(QueueBrokerTestDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (TableExists(dbContext, dbContext.Model.FindEntityType(typeof(QueueMessage))?.GetTableName()))
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

public class QueueBrokerTestDbContext(DbContextOptions<QueueBrokerTestDbContext> options) : DbContext(options), IQueueingContext
{
    public DbSet<QueueMessage> QueueMessages { get; set; }
}

public sealed class RelationalQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class RelationalQueueMessageHandler : IQueueMessageHandler<RelationalQueueMessage>
{
    public static bool Processed { get; private set; }

    public static void Reset()
    {
        Processed = false;
    }

    public Task Handle(RelationalQueueMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        return Task.CompletedTask;
    }
}

public sealed class RelationalCoordinatedQueueMessageHandler : IQueueMessageHandler<RelationalQueueMessage>
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

    public async Task Handle(RelationalQueueMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref processCount);
        Started.TrySetResult(true);
        await ContinueProcessing.Task.WaitAsync(cancellationToken);
    }
}

public sealed class QueueBrokerTestHandlerFactory : IQueueMessageHandlerFactory
{
    public QueueMessageHandlerFactoryResult Create(Type messageHandlerType)
    {
        if (messageHandlerType == typeof(RelationalQueueMessageHandler))
        {
            return QueueMessageHandlerFactoryResult.Create(new RelationalQueueMessageHandler());
        }

        if (messageHandlerType == typeof(RelationalCoordinatedQueueMessageHandler))
        {
            return QueueMessageHandlerFactoryResult.Create(new RelationalCoordinatedQueueMessageHandler());
        }

        return null;
    }
}
