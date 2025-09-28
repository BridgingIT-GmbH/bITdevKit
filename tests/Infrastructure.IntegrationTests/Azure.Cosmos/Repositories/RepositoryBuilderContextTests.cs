// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using Domain.Repositories;
using Infrastructure.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextTests(ITestOutputHelper output) : TestsBase(output,
    s =>
    {
        s.AddMediatR();
        s.AddCosmosClient(o => o
                .UseConnectionString("AccountEndpoint=https://dummy.documents.azure.com:443/;AccountKey=accountkey==;"));

        s.AddCosmosSqlRepository<PersonStub>(o => o.PartitionKey(e => e.LastName))
            //s.AddCosmosSqlRepository<PersonStub>(sp => new CosmosSqlProvider<PersonStub>(o => o.Client(sp.GetRequiredService<CosmosClient>()).PartitionKey(e => e.Nationality)));
            .WithBehavior<RepositoryTracingBehavior<PersonStub>>()
            .WithBehavior<RepositoryLoggingBehavior<PersonStub>>()
            .WithBehavior<RepositoryDomainEventBehavior<PersonStub>>()
            .WithBehavior<RepositoryDomainEventMediatorPublisherBehavior<PersonStub>>();
    })
{
    [Fact]
    public void GetCosmosClient_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<CosmosClient>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public void GetCosmosProvider_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<ICosmosSqlProvider<PersonStub>>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<IGenericRepository<PersonStub>>();

        // Assert
        sut.ShouldNotBeNull();
    }
}