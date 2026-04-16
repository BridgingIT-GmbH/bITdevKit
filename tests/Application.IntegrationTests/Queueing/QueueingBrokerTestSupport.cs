namespace BridgingIT.DevKit.Application.IntegrationTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class QueueingBrokerTestSupport
{
    public static async Task<bool> WaitForAsync(Func<Task<bool>> condition, int attempts = 80, int delayMilliseconds = 25)
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

    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());

        return services;
    }

    public static IServiceCollection AddTestQueueDbContext(this IServiceCollection services, string databaseName, InMemoryDatabaseRoot databaseRoot)
    {
        services.AddDbContext<TestQueueDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));

        return services;
    }
}

public sealed class FirstQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class FirstQueueMessageHandler : IQueueMessageHandler<FirstQueueMessage>
{
    public Task Handle(FirstQueueMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class SecondQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class SecondQueueMessageHandler : IQueueMessageHandler<SecondQueueMessage>
{
    public Task Handle(SecondQueueMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class InProcessQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class InProcessQueueMessageHandler : IQueueMessageHandler<InProcessQueueMessage>
{
    public static bool Processed { get; private set; }

    public static int ProcessCount { get; private set; }

    public static string LastProcessedMessageId { get; private set; }

    public static void Reset()
    {
        Processed = false;
        ProcessCount = 0;
        LastProcessedMessageId = null;
    }

    public Task Handle(InProcessQueueMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        ProcessCount++;
        LastProcessedMessageId = message.MessageId;

        return Task.CompletedTask;
    }
}

public sealed class EntityFrameworkQueueMessage(string value) : QueueMessageBase
{
    public string Value { get; } = value;
}

public sealed class EntityFrameworkQueueMessageHandler : IQueueMessageHandler<EntityFrameworkQueueMessage>
{
    public static bool Processed { get; private set; }

    public static int ProcessCount { get; private set; }

    public static void Reset()
    {
        Processed = false;
        ProcessCount = 0;
    }

    public Task Handle(EntityFrameworkQueueMessage message, CancellationToken cancellationToken)
    {
        Processed = true;
        ProcessCount++;
        return Task.CompletedTask;
    }
}

public class TestQueueDbContext(DbContextOptions<TestQueueDbContext> options) : DbContext(options), IQueueingContext
{
    public DbSet<QueueMessage> QueueMessages { get; set; }
}