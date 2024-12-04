// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests;

using Core.Infrastructure;
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
                .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new WaitUntil()))
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

    public CoreDbContext StubContext { get; private set; }

    private static bool IsCiEnvironment =>
        Environment.GetEnvironmentVariable("AGENT_NAME") is not null; // check if running on Microsoft's CI environment

    public TestEnvironmentFixture WithOutput(ITestOutputHelper output)
    {
        this.Output = output;

        return this;
    }

    public async Task InitializeAsync()
    {
        await this.Network.CreateAsync().AnyContext();

        await this.SqlContainer.StartAsync().AnyContext();

        if (!IsCiEnvironment) // the cosmos docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
        {
            //await this.CosmosContainer.StartAsync().AnyContext();
        }

        await this.AzuriteContainer.StartAsync().AnyContext();

        //await this.RabbitMQContainer.StartAsync().AnyContext();

        this.StubContext = this.CreateSqlServerDbContext();
        await this.StubContext.Database.MigrateAsync(); // ensure migrations are applied
    }

    public async Task DisposeAsync()
    {
        this.StubContext?.Dispose();

        await this.SqlContainer.DisposeAsync().AnyContext();

        await this.CosmosContainer.DisposeAsync().AnyContext();

        await this.AzuriteContainer.DisposeAsync().AnyContext();

        //await this.RabbitMQContainer.DisposeAsync().AnyContext();

        await this.Network.DeleteAsync().AnyContext();
    }

    public CoreDbContext CreateSqlServerDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();

        if (this.Output is not null)
        {
            optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>()
                .LogTo(this.Output.WriteLine);
        }

        optionsBuilder.UseSqlServer(this.SqlConnectionString);

        return new CoreDbContext(optionsBuilder.Options);
    }

    public CoreDbContext CreateSqliteDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();

        if (this.Output is not null)
        {
            optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>()
                .LogTo(this.Output.WriteLine);
        }

        optionsBuilder.UseSqlite($"Data Source=.\\_test_{nameof(CoreDbContext)}_sqlite.db");

        var context = new CoreDbContext(optionsBuilder.Options);
        context.Database.EnsureCreated();

        return context;
    }

    public CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(this.CosmosConnectionString,
            new CosmosClientOptions
            {
                SerializerOptions =
                    new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                ConnectionMode = ConnectionMode.Gateway,
                HttpClientFactory = () => this.CosmosContainer.HttpClient
            });
    }

    private sealed class
        WaitUntil
        : IWaitUntil // TODO: obsolete in next testcontainers (>3.7.0) release  https://github.com/testcontainers/testcontainers-dotnet/pull/1109
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