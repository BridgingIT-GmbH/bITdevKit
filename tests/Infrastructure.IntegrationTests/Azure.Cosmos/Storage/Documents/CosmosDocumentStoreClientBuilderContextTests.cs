// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Infrastructure")]
[Collection(nameof(CosmosDocumentStoreTestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosDocumentStoreClientBuilderContextTests
{
    private readonly CosmosDocumentStoreTestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;

    public CosmosDocumentStoreClientBuilderContextTests(ITestOutputHelper output, CosmosDocumentStoreTestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            this.output = output;

            // register the services
            this.fixture.Services.AddMediatR();

            this.fixture.Services.AddSingleton(this.fixture.EnsureCosmosClient());

            this.fixture.Services.AddCosmosDocumentStoreClient<PersonStubDocument>(o => o
                    .Database(this.fixture.ClientBuilderDatabaseName)
                    .Container("storage_documents")
                    .PartitionKey(e => e.Type))
                .WithBehavior<LoggingDocumentStoreClientBehavior<PersonStubDocument>>()
                .WithBehavior((inner, sp) =>
                    new TimeoutDocumentStoreClientBehavior<PersonStubDocument>(sp.GetRequiredService<ILoggerFactory>(),
                        inner,
                        new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));
        }
        else
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    //[SkippableFact]
    //public void GetCosmosClient_WhenRequested_ShouldNotBeNull()
    //{
    //    Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

    //    // Arrange
    //    // Act
    //    var sut = this.fixture.ServiceProvider.GetService<CosmosClient>();

    //    // Assert
    //    sut.ShouldNotBeNull();
    //}

    [SkippableFact]
    public void GetDocumentStoreClient_WhenRequested_ShouldNotBeNull()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var sut = this.fixture.ServiceProvider.GetService<IDocumentStoreClient<PersonStubDocument>>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [SkippableFact]
    public async Task GetDocumentStoreClient_WhenUsed_ShouldWriteAndReadThroughCosmosProvider()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var sut = this.fixture.ServiceProvider.GetRequiredService<IDocumentStoreClient<PersonStubDocument>>();
        var key = new DocumentKey("di-partition-" + DateTime.UtcNow.Ticks, "row-1");

        var saveResult = await sut.UpsertResultAsync(
            key,
            new PersonStubDocument
            {
                FirstName = "Cosmos",
                LastName = "DI",
                Age = 41
            },
            timeout.Token);
        var getResult = await sut.GetResultAsync(key, timeout.Token);

        saveResult.IsSuccess.ShouldBeTrue(saveResult.Errors.Select(e => e.Message).ToString(", "));
        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value.FirstName.ShouldBe("Cosmos");
        getResult.Value.LastName.ShouldBe("DI");
    }
}
