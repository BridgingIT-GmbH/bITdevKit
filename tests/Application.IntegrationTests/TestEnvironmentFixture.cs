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
using System.Net.Http;

public class TestEnvironmentFixture : IAsyncLifetime
{
    private IServiceProvider serviceProvider;

    public TestEnvironmentFixture()
    {
        this.Services.AddLogging(c =>
            c.AddProvider(new XunitLoggerProvider(this.Output)));

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
            new CosmosDbBuilder()
                .WithNetworkAliases(this.NetworkName)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .AddCustomWaitStrategy(new WaitUntil()))
                .Build();

        this.AzuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand("--skipApiVersionCheck")
            .Build();

        this.MailHogContainer = new ContainerBuilder()
            .WithImage("mailhog/mailhog:latest")
            .WithNetworkAliases(this.NetworkName)
            .WithPortBinding(1025, 1025) // SMTP port
            .WithPortBinding(8025, 8025) // HTTP API/UI port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1025))
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

    public string MailHogSmtpConnectionString => "smtp://localhost:1025";

    public INetwork Network { get; }

    public MsSqlContainer SqlContainer { get; }

    public CosmosDbContainer CosmosContainer { get; }

    public AzuriteContainer AzuriteContainer { get; }

    public RabbitMqContainer RabbitMQContainer { get; }

    public IContainer MailHogContainer { get; }

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

    private static bool IsCIEnvironment => Environment.GetEnvironmentVariable("AGENT_NAME") is not null;

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

        if (!IsCIEnvironment)
        {
            //await this.CosmosContainer.StartAsync().AnyContext();
        }

        await this.AzuriteContainer.StartAsync()
            .AnyContext();

        await this.MailHogContainer.StartAsync()
            .AnyContext();

        //await this.RabbitMQContainer.StartAsync().AnyContext();
    }

    public async Task DisposeAsync()
    {
        await this.SqlContainer.DisposeAsync()
            .AnyContext();

        await this.CosmosContainer.DisposeAsync()
            .AnyContext();

        await this.AzuriteContainer.DisposeAsync()
            .AnyContext();

        await this.MailHogContainer.DisposeAsync()
            .AnyContext();

        //await this.RabbitMQContainer.DisposeAsync().AnyContext();

        await this.Network.DeleteAsync()
            .AnyContext();
    }

    public HttpClient GetMailHogApiClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:8025")
        };
    }

    public StubDbContext EnsureSqlServerDbContext(ITestOutputHelper output = null, bool forceNew = false)
    {
        if (this.SqlServerDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();
            optionsBuilder.UseSqlServer(this.SqlConnectionString);
            var context = new StubDbContext(optionsBuilder.Options);
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
            optionsBuilder.UseSqlite($"Data Source=.\\_tests_{nameof(StubDbContext)}_sqlite.db");
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

    private sealed class WaitUntil : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
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