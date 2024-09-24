// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Microsoft.Extensions.Logging;

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
            .WithHealthChecks()
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
}