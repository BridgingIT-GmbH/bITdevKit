namespace BridgingIT.DevKit.Application.IntegrationTests.Orchestration;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Application")]
public class OrchestrationRegistrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AddOrchestrations_WithEntityFramework_RegistersEntityFrameworkPersistenceProvider()
    {
        var services = new ServiceCollection();
        var serializer = new SystemTextJsonSerializer();
        var databaseRoot = new InMemoryDatabaseRoot();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddProvider(new XunitLoggerProvider(output));
        });
        services.AddDbContext<TestRegistrationOrchestrationDbContext>(options =>
            options.UseInMemoryDatabase($"orchestration-registration-{Guid.NewGuid():N}", databaseRoot));

        services.AddOrchestrations()
            .WithOrchestration<TestRegistrationOrchestration>()
            .WithEntityFramework<TestRegistrationOrchestrationDbContext>(options => options.UseSerializer(serializer));

        using var provider = services.BuildServiceProvider();
        var persistence = provider.GetRequiredService<IOrchestrationStorageProvider>();
        var registrations = provider.GetRequiredService<OrchestrationRegistrationStore>();

        persistence.ShouldBeOfType<EntityFrameworkOrchestrationStorageProvider<TestRegistrationOrchestrationDbContext>>();
        persistence.Serializer.ShouldBeSameAs(serializer);
        provider.GetRequiredService<IOrchestrationInstanceStore>().ShouldNotBeNull();
        provider.GetRequiredService<IOrchestrationQueryStore>().ShouldNotBeNull();
        provider.GetRequiredService<IOrchestrationQueryService>().ShouldNotBeNull();
        provider.GetRequiredService<IOrchestrationAdministrationStore>().ShouldNotBeNull();
        provider.GetRequiredService<IOrchestrationAdministrationService>().ShouldNotBeNull();
        registrations.Contains(typeof(TestRegistrationOrchestration)).ShouldBeTrue();
    }
}

public sealed class TestRegistrationOrchestration
{
}

public class TestRegistrationOrchestrationDbContext(DbContextOptions<TestRegistrationOrchestrationDbContext> options) : DbContext(options), IOrchestrationContext
{
    public DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    public DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    public DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    public DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}
