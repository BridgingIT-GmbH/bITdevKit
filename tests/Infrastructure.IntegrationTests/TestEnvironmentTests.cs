// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using System.Data;
using System.Data.Common;
using DotNet.Testcontainers.Containers;
using global::Azure;
using global::Azure.Data.Tables;
using global::Azure.Storage.Blobs;
using global::Azure.Storage.Queues;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class TestEnvironmentTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    // MsSql container ===================================================================
    [Fact]
    public async Task MsSql_EstablishesConnection_ReturnsSuccessful()
    {
        // Arrange
        using DbConnection connection = new SqlConnection(this.fixture.SqlConnectionString);

        // Act
        await connection.OpenAsync();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task MsSql_ExecScript_ReturnsSuccessful()
    {
        // Arrange
        const string scriptContent = "SELECT 1;";

        // Act
        var result = await this.fixture.SqlContainer.ExecScriptAsync(scriptContent)
            .AnyContext();

        // When
        Assert.True(0L.Equals(result.ExitCode), result.Stderr);
    }

    // Cosmos container ===============================================================
    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task Cosmos_EstablishesConnection_ReturnsSuccessful()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        using var client = new CosmosClient(this.fixture.CosmosConnectionString,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => this.fixture.CosmosContainer.HttpClient
            });

        // Act
        var result = await client.ReadAccountAsync()
            .AnyContext();

        // Assert
        result.Id.ShouldBe("localhost");
    }

    // Azurite (Storage) container ======================================================
    [Fact]
    public async Task AzuriteBlob_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new BlobServiceClient(this.fixture.AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_01_03));

        // When
        var result = await client.GetPropertiesAsync()
            .AnyContext();

        // Then
        HasError(result)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task AzuriteQueue_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new QueueServiceClient(this.fixture.AzuriteConnectionString);

        // When
        var result = await client.GetPropertiesAsync()
            .AnyContext();

        // Then
        HasError(result)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task AzuriteTable_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new TableServiceClient(this.fixture.AzuriteConnectionString);

        // When
        var result = await client.GetPropertiesAsync()
            .AnyContext();

        // Then
        HasError(result)
            .ShouldBeFalse();
    }

    // RabbitMq container ===============================================================
    //[Fact]
    //public void RabbitMQ_EstablishesConnection_ReturnsSuccessful()
    //{
    //    // Arrange
    //    var factory = new global::RabbitMQ.Client.ConnectionFactory
    //    {
    //        Uri = new Uri(this.fixture.RabbitMQConnectionString),
    //        AutomaticRecoveryEnabled = true,
    //        DispatchConsumersAsync = true
    //    };

    //    // Act
    //    using var connection = factory.CreateConnection();

    //    // Assert
    //    connection.IsOpen.ShouldBeTrue();
    //}

    private static bool HasError<TResponseEntity>(NullableResponse<TResponseEntity> response)
    {
        using var rawResponse = response.GetRawResponse();
        return rawResponse.IsError;
    }
}