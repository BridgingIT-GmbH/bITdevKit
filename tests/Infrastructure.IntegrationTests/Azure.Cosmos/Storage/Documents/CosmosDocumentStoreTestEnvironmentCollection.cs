// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.CosmosDb;

[CollectionDefinition(nameof(CosmosDocumentStoreTestEnvironmentCollection), DisableParallelization = true)]
public class CosmosDocumentStoreTestEnvironmentCollection : ICollectionFixture<CosmosDocumentStoreTestEnvironmentFixture>;

public sealed class CosmosDocumentStoreTestEnvironmentFixture : IAsyncLifetime
{
    private IServiceProvider serviceProvider;
    private CosmosClient cosmosClient;
    private CosmosDocumentStoreProvider cosmosDocumentStoreProvider;

    public CosmosDocumentStoreTestEnvironmentFixture()
    {
        this.CosmosContainer = new CosmosDbBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new WaitUntil()))
            .Build();

        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(this.Output)));
    }

    public IServiceCollection Services { get; } = new ServiceCollection();

    public ITestOutputHelper Output { get; private set; }

    public CosmosDbContainer CosmosContainer { get; }

    public string ClientBuilderDatabaseName { get; } = "test_di_" + Guid.NewGuid().ToString("N");

    public string CosmosConnectionString => this.CosmosContainer.GetConnectionString();

    public IServiceProvider ServiceProvider
    {
        get
        {
            this.serviceProvider ??= this.Services.BuildServiceProvider();

            return this.serviceProvider;
        }
    }

    private static bool IsCIEnvironment =>
        Environment.GetEnvironmentVariable("AGENT_NAME") is not null;

    public CosmosDocumentStoreTestEnvironmentFixture WithOutput(ITestOutputHelper output)
    {
        this.Output = output;

        return this;
    }

    public async Task InitializeAsync()
    {
        if (IsCIEnvironment)
        {
            return;
        }

        await this.CosmosContainer.StartAsync().AnyContext();
        await this.WaitUntilCosmosAcceptsSdkRequestsAsync().AnyContext();
    }

    public async Task DisposeAsync()
    {
        this.cosmosClient?.Dispose();
        await this.CosmosContainer.DisposeAsync().AnyContext();
    }

    public CosmosClient EnsureCosmosClient(bool forceNew = false)
    {
        if (this.cosmosClient is null || forceNew)
        {
            this.cosmosClient?.Dispose();
            this.cosmosClient = new CosmosClient(
                this.CosmosConnectionString,
                new Microsoft.Azure.Cosmos.CosmosClientOptions
                {
                    ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
                    HttpClientFactory = () => this.CosmosContainer.HttpClient,
                    RequestTimeout = TimeSpan.FromSeconds(20),
                    Serializer = new CosmosJsonNetSerializer(DefaultNewtonsoftSerializerSettings.Create())
                });
        }

        return this.cosmosClient;
    }

    public CosmosDocumentStoreProvider EnsureCosmosDocumentStoreProvider(bool forceNew = false)
    {
        if (this.cosmosDocumentStoreProvider is null || forceNew)
        {
            this.cosmosDocumentStoreProvider = new CosmosDocumentStoreProvider(
                new CosmosSqlProvider<CosmosStorageDocument>(
                    new CosmosSqlProviderOptions<CosmosStorageDocument>
                    {
                        Client = this.EnsureCosmosClient(forceNew),
                        Database = "test",
                        Container = "storage_documents",
                        PartitionKey = "/type",
                        PartitionKeyStringExpression = e => e.Type,
                        DatabaseThroughPut = 0,
                        ThroughPut = 0
                    }),
                options: new DocumentStoreOptions { AllowFullScans = true });
        }

        return this.cosmosDocumentStoreProvider;
    }

    private async Task WaitUntilCosmosAcceptsSdkRequestsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var client = this.EnsureCosmosClient(forceNew: true);

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                await client.CreateDatabaseIfNotExistsAsync("test", cancellationToken: timeout.Token).AnyContext();

                return;
            }
            catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token).AnyContext();
            }
        }

        throw new TimeoutException("Cosmos document store emulator did not accept SDK requests before the readiness timeout.");
    }

    private sealed class WaitUntil : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            try
            {
                var result = await container.ExecAsync(
                    ["/bin/sh", "-c", "curl -fsS http://localhost:8080/ready | grep -q '\"ready\": true'"],
                    CancellationToken.None);

                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
