// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class DocumentStoreClientBuilderContextTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;

    public DocumentStoreClientBuilderContextTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.output = output;

        // register the services
        this.fixture.Services.AddMediatR();

        this.fixture.Services.AddSqlServerDbContext<StubDbContext>(this.fixture.SqlConnectionString)
            .WithDatabaseCreatorService(o => o.DeleteOnStartup())
            .WithOutboxMessageService(o => o
                .ProcessingInterval("00:00:10")
                .StartupDelay("00:00:30")
                .PurgeOnStartup())
            .WithOutboxDomainEventService(o => o
                .ProcessingInterval("00:00:10")
                .StartupDelay("00:00:05")
                .PurgeOnStartup());

        this.fixture.Services.AddEntityFrameworkDocumentStoreClient<PersonStubDocument, StubDbContext>()
            .WithBehavior<LoggingDocumentStoreClientBehavior<PersonStubDocument>>()
            .WithBehavior((inner, sp) =>
                new TimeoutDocumentStoreClientBehavior<PersonStubDocument>(sp.GetRequiredService<ILoggerFactory>(),
                    inner,
                    new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));
    }

    //[Fact]
    //public void GetDbContext_WhenRequested_ShouldNotBeNull()
    //{
    //    // Arrange
    //    // Act
    //    var sut = this.fixture.ServiceProvider.GetService<StubDbContext>();

    //    // Assert
    //    sut.ShouldNotBeNull();
    //}

    [Fact]
    public void GetDocumentStoreClient_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.fixture.ServiceProvider.GetService<IDocumentStoreClient<PersonStubDocument>>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetDocumentStoreClient_WithSingletonLifetime_ShouldResolveScopeSafeClient()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StubDbContext>(options => options.UseSqlite(connection));
        services.AddEntityFrameworkDocumentStoreClient<PersonStubDocument, StubDbContext>(
            lifetime: ServiceLifetime.Singleton,
            configure: options => options.LeaseDuration = TimeSpan.FromSeconds(5));

        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StubDbContext>().Database.EnsureCreatedAsync();
        }

        var sut = serviceProvider.GetRequiredService<IDocumentStoreClient<PersonStubDocument>>();
        var documentKey = new DocumentKey("people", "42");
        var document = new PersonStubDocument
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Age = 36
        };

        // Act
        await sut.UpsertAsync(documentKey, document);
        var result = await sut.FindAsync(documentKey);

        // Assert
        result.Count().ShouldBe(1);
        result.First().FirstName.ShouldBe("Ada");
    }
}
