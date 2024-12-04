// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

public class TestEnvironmentFixture : IAsyncLifetime
{
    private IServiceProvider serviceProvider;

    public TestEnvironmentFixture()
    {
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(this.Output)));

        this.Network = new NetworkBuilder()
            .WithName(this.NetworkName)
            .Build();

        this.SqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetworkAliases(this.NetworkName)
            .WithExposedPort(1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();

        this.CosmosContainer =
            new CosmosDbBuilder() // INFO: remove docker image when container fails with 'The evaluation period has expired.' https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/60
                .WithNetworkAliases(this.NetworkName)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .AddCustomWaitStrategy(new WaitUntil()))
                //.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:mongodb") // prevents 'The evaluation period has expired.' for another 180 days
                .Build();

        this.AzuriteContainer = new AzuriteBuilder()
            .WithNetworkAliases(this.NetworkName)
            .Build();

        //this.RabbitMQContainer = new RabbitMqBuilder()
        //    .WithNetworkAliases(this.NetworkName)
        //    .Build();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();

    public ITestOutputHelper Output { get; private set; }

    public string NetworkName => HashHelper.Compute(DateTime.UtcNow.Ticks);

    public string SqlConnectionString => this.SqlContainer.GetConnectionString();

    public string AzuriteConnectionString => this.AzuriteContainer.GetConnectionString();

    public string CosmosConnectionString => this.CosmosContainer.GetConnectionString();

    public string RabbitMQConnectionString => this.RabbitMQContainer.GetConnectionString();

    public INetwork Network { get; }

    public MsSqlContainer SqlContainer { get; }

    public CosmosDbContainer CosmosContainer { get; }

    public AzuriteContainer AzuriteContainer { get; }

    public RabbitMqContainer RabbitMQContainer { get; }

    public IServiceProvider ServiceProvider
    {
        get
        {
            this.serviceProvider ??= this.Services.BuildServiceProvider();

            return this.serviceProvider;
        }
    }

    public StubDbContext SqlServerDbContext { get; set; }

    public StubDbContext SqliteDbContext { get; set; }

    private static bool IsCIEnvironment => Environment.GetEnvironmentVariable("AGENT_NAME") is not null; // check if running on Microsoft's CI environment

    public TestEnvironmentFixture WithOutput(ITestOutputHelper output)
    {
        this.Output = output;

        return this;
    }

    public async Task InitializeAsync()
    {
        await this.Network.CreateAsync()
            .AnyContext();

        await this.SqlContainer.StartAsync()
            .AnyContext();

        if (!IsCIEnvironment) // the cosmos docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
        {
            //await this.CosmosContainer.StartAsync().AnyContext();
        }

        await this.AzuriteContainer.StartAsync()
            .AnyContext();

        //await this.RabbitMQContainer.StartAsync().AnyContext();
    }

    public async Task DisposeAsync()
    {
        //this.Context?.Dispose();

        await this.SqlContainer.DisposeAsync()
            .AnyContext();

        await this.CosmosContainer.DisposeAsync()
            .AnyContext();

        await this.AzuriteContainer.DisposeAsync()
            .AnyContext();

        //await this.RabbitMQContainer.DisposeAsync().AnyContext();

        await this.Network.DeleteAsync()
            .AnyContext();
    }

    public StubDbContext EnsureSqlServerDbContext(ITestOutputHelper output = null, bool forceNew = false)
    {
        if (this.SqlServerDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();

            //if (output is not null)
            //{
            //    optionsBuilder = new DbContextOptionsBuilder<StubDbContext>()
            //        .LogTo(output.WriteLine);
            //}

            optionsBuilder.UseSqlServer(this.SqlConnectionString);
            var context = new StubDbContext(optionsBuilder.Options);
            //context.Database.Migrate();
            context.Database.EnsureCreated();

            if (forceNew)
            {
                return context;
            }

            this.SqlServerDbContext = context;
        }

        return this.SqlServerDbContext;
    }

    public StubDbContext EnsureSqliteDbContext(ITestOutputHelper output = null, bool forceNew = false)
    {
        if (this.SqliteDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();

            //if (output is not null)
            //{
            //    optionsBuilder = new DbContextOptionsBuilder<StubDbContext>()
            //        .LogTo(output.WriteLine);
            //}

            optionsBuilder.UseSqlite($"Data Source=.\\_tests_{nameof(StubDbContext)}_sqlite.db"); // _{DateOnly.FromDateTime(DateTime.Now)}
            var context = new StubDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            if (forceNew)
            {
                return context;
            }

            this.SqliteDbContext = context;
        }

        return this.SqliteDbContext;
    }

    public CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(this.CosmosConnectionString,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => this.CosmosContainer.HttpClient
            });
    }

    private sealed class WaitUntil : IWaitUntil // TODO: obsolete in next testcontainers (>3.7.0) release  https://github.com/testcontainers/testcontainers-dotnet/pull/1109
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            // CosmosDB's preconfigured HTTP client will redirect the request to the container.
            const string requestUri = "https://localhost/_explorer/emulator.pem";

            var httpClient = ((CosmosDbContainer)container).HttpClient;

            try
            {
                using var httpResponse = await httpClient.GetAsync(requestUri).ConfigureAwait(false);

                return httpResponse.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                httpClient.Dispose();
            }
        }
    }
}