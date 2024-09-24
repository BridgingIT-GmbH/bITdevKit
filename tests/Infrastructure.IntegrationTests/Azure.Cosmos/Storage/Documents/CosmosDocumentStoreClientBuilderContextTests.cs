// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosDocumentStoreClientBuilderContextTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;

    public CosmosDocumentStoreClientBuilderContextTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            this.output = output;

            // register the services
            this.fixture.Services.AddMediatR();

            this.fixture.Services.AddCosmosClient(o => o
                    .UseConnectionString(this.fixture.CosmosConnectionString))
                .WithHealthChecks();

            this.fixture.Services.AddCosmosDocumentStoreClient<PersonStubDocument>(o => o.Database("test")) // no need to setup the client+provider (sql)
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
}