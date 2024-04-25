// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class TestEnvironmentTests
{
    private readonly TestEnvironmentFixture fixture;

    public TestEnvironmentTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
    }

    // MsSql container ===================================================================
    [Fact]
    public async Task MsSql_EstablishesConnection_ReturnsSuccessful()
    {
        // Arrange
        using DbConnection connection = new Microsoft.Data.SqlClient.SqlConnection(this.fixture.SqlConnectionString);

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
        var result = await this.fixture.SqlContainer.ExecScriptAsync(scriptContent).AnyContext();

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
        using var client = new Microsoft.Azure.Cosmos.CosmosClient(
            this.fixture.CosmosConnectionString,
            new Microsoft.Azure.Cosmos.CosmosClientOptions
            {
                SerializerOptions = new Microsoft.Azure.Cosmos.CosmosSerializationOptions { PropertyNamingPolicy = Microsoft.Azure.Cosmos.CosmosPropertyNamingPolicy.CamelCase },
                ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
                HttpClientFactory = () => this.fixture.CosmosContainer.HttpClient
            });

        // Act
        var result = await client.ReadAccountAsync().AnyContext();

        // Assert
        result.Id.ShouldBe("localhost");
    }

    // Azurite (Storage) container ======================================================
    [Fact]
    public async Task AzuriteBlob_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new global::Azure.Storage.Blobs.BlobServiceClient(
            this.fixture.AzuriteConnectionString,
            new global::Azure.Storage.Blobs.BlobClientOptions(global::Azure.Storage.Blobs.BlobClientOptions.ServiceVersion.V2023_01_03));

        // When
        var result = await client.GetPropertiesAsync().AnyContext();

        // Then
        HasError(result).ShouldBeFalse();
    }

    [Fact]
    public async Task AzuriteQueue_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new global::Azure.Storage.Queues.QueueServiceClient(this.fixture.AzuriteConnectionString);

        // When
        var result = await client.GetPropertiesAsync().AnyContext();

        // Then
        HasError(result).ShouldBeFalse();
    }

    [Fact]
    public async Task AzuriteTable_EstablishesConnection_ReturnsSuccessful()
    {
        // Give
        var client = new global::Azure.Data.Tables.TableServiceClient(this.fixture.AzuriteConnectionString);

        // When
        var result = await client.GetPropertiesAsync().AnyContext();

        // Then
        HasError(result).ShouldBeFalse();
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

    private static bool HasError<TResponseEntity>(global::Azure.NullableResponse<TResponseEntity> response)
    {
        using var rawResponse = response.GetRawResponse();
        return rawResponse.IsError;
    }
}