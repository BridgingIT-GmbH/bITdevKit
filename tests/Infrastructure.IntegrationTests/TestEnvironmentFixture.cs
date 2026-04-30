// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Infrastructure.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.PostgreSql;
using CosmosClientOptions = Microsoft.Azure.Cosmos.CosmosClientOptions;

public class TestEnvironmentFixture : IAsyncLifetime
{
    private IServiceProvider serviceProvider;
    private CosmosSqlProvider<PersonStub> cosmosSqlProviderPersonStub;
    private CosmosDocumentStoreProvider cosmosDocumentStoreProvider;

    public TestEnvironmentFixture()
    {
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(this.Output)));

        this.Network = new NetworkBuilder()
            .WithName(this.NetworkName)
            .Build();

        this.SqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            //.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetworkAliases(this.NetworkName)
            .WithExposedPort(1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();

        this.PostgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
             //.WithImage("postgres:16-alpine")
             .WithDatabase("testdb")
             .WithUsername("postgres")
             .WithPassword("postgres")
             .WithNetworkAliases(this.NetworkName)
             .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
             .Build();

        this.CosmosContainer = new CosmosDbBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview") // INFO: remove docker image when container fails with 'The evaluation period has expired.' https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/60
            .WithNetworkAliases(this.NetworkName)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new WaitUntil()))
            //.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
            .Build();

        // this.CosmosContainer = new CosmosDbBuilder()
        //     .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator")
        //     .WithName("azure-cosmos-emulator")
        //     .WithExposedPort(8081)
        //     .WithExposedPort(10251)
        //     .WithExposedPort(10252)
        //     .WithExposedPort(10253)
        //     .WithExposedPort(10254)
        //     .WithPortBinding(8081, true)
        //     .WithPortBinding(10251, true)
        //     .WithPortBinding(10252, true)
        //     .WithPortBinding(10253, true)
        //     .WithPortBinding(10254, true)
        //     .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "1")
        //     .WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1")
        //     .WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false")
        //     .WithWaitStrategy(Wait.ForUnixContainer()
        //         .UntilPortIsAvailable(8081))
        //     .Build();

        this.AzuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
                //.WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
                .WithCommand("--skipApiVersionCheck")
                .Build();
        //new AzuriteBuilder()
        //    .WithNetworkAliases(this.NetworkName)
        //    .Build();

        this.RabbitMQContainer = new RabbitMqBuilder("rabbitmq:3.13-alpine")
            .WithNetworkAliases(this.NetworkName)
            .Build();

        this.ServiceBusEmulatorConfigPath = this.CreateServiceBusEmulatorConfig();

        this.ServiceBusSqlContainer = new ContainerBuilder("mcr.microsoft.com/azure-sql-edge:latest")
            .WithName($"sb-sql-{Guid.NewGuid():N}")
            .WithNetwork(this.Network)
            .WithNetworkAliases("sb-sql")
            .WithPortBinding(1433, true)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrongPassword123!")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();

        this.ServiceBusEmulatorContainer = new ContainerBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithName($"sb-emulator-{Guid.NewGuid():N}")
            .WithNetwork(this.Network)
            .WithPortBinding(5672, true)
            .WithPortBinding(8900, true)
            .WithEnvironment("SQL_SERVER", "sb-sql")
            .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrongPassword123!")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithBindMount(this.ServiceBusEmulatorConfigPath, "/ServiceBus_Emulator/ConfigFiles/Config.json")
            .Build();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();

    public ITestOutputHelper Output { get; private set; }

    public string NetworkName => HashHelper.Compute(DateTime.UtcNow.Ticks);

    public string SqlConnectionString => this.SqlContainer.GetConnectionString();

    public string PostgresConnectionString => this.PostgresContainer.GetConnectionString();

    public string SqliteConnectionString => $"Data Source=.\\_tests_{nameof(StubDbContext)}_sqlite.db";

    public string AzuriteConnectionString => this.AzuriteContainer.GetConnectionString();

    public string CosmosConnectionString => this.CosmosContainer.GetConnectionString();

    public string RabbitMQConnectionString => this.RabbitMQContainer.GetConnectionString();

    public INetwork Network { get; }

    public MsSqlContainer SqlContainer { get; }

    public PostgreSqlContainer PostgresContainer { get; }

    public CosmosDbContainer CosmosContainer { get; }

    public AzuriteContainer AzuriteContainer { get; }

    public RabbitMqContainer RabbitMQContainer { get; }

    public IContainer ServiceBusSqlContainer { get; }

    public IContainer ServiceBusEmulatorContainer { get; }

    public string ServiceBusEmulatorConfigPath { get; }

    public string ServiceBusEmulatorConnectionString
    {
        get
        {
            var port = this.ServiceBusEmulatorContainer?.GetMappedPublicPort(5672) ?? 5672;
            return $"Endpoint=sb://localhost:{port};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        }
    }

    public async Task<bool> WaitForServiceBusEmulatorReadyAsync(int timeoutSeconds = 90)
    {
        if (this.ServiceBusEmulatorContainer?.State != TestcontainersStates.Running)
        {
            return false;
        }

        var port = this.ServiceBusEmulatorContainer.GetMappedPublicPort(5672);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("localhost", port).WaitAsync(TimeSpan.FromSeconds(2), cts.Token);
                return true;
            }
            catch
            {
                try
                {
                    await Task.Delay(1000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return false;
    }

    public IServiceProvider ServiceProvider
    {
        get
        {
            this.serviceProvider ??= this.Services.BuildServiceProvider();

            return this.serviceProvider;
        }
    }

    public StubDbContext SqlServerDbContext { get; set; }

    public StubDbContext PostgresDbContext { get; private set; }

    public StubDbContext SqliteDbContext { get; set; }

    public StubDbContext CosmosDbContext { get; set; }

    public StubDbContext InMemoryDbContext { get; set; }

    public CosmosClient CosmosClient { get; private set; }

    private static bool IsCIEnvironment =>
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

        await this.PostgresContainer.StartAsync().AnyContext();

        if (!IsCIEnvironment) // the cosmos docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
        {
            //await this.CosmosContainer.StartAsync().AnyContext(); // not started due to issues with the typedid cosmos tests
        }

        await this.AzuriteContainer.StartAsync().AnyContext();

        await this.RabbitMQContainer.StartAsync().AnyContext();

        try
        {
            await this.ServiceBusSqlContainer.StartAsync().AnyContext();
            await this.ServiceBusEmulatorContainer.StartAsync().AnyContext();

            // The Service Bus emulator waits 20s before checking SQL, then retries every 15s.
            // It needs ~45-60s total before AMQP is ready. Wait 50s to give it time to initialize.
            await Task.Delay(TimeSpan.FromSeconds(50));
        }
        catch
        {
            // Emulator or SQL Edge may fail to start (e.g., port 5672 in use); tests will skip.
        }
    }

    public async Task DisposeAsync()
    {
        //this.Context?.Dispose();

        await this.SqlContainer.DisposeAsync().AnyContext();

        await this.PostgresContainer.DisposeAsync().AnyContext();

        await this.CosmosContainer.DisposeAsync().AnyContext();

        await this.AzuriteContainer.DisposeAsync().AnyContext();

        await this.RabbitMQContainer.DisposeAsync().AnyContext();

        await this.ServiceBusEmulatorContainer.DisposeAsync().AnyContext();
        await this.ServiceBusSqlContainer.DisposeAsync().AnyContext();

        try
        {
            if (File.Exists(this.ServiceBusEmulatorConfigPath))
            {
                File.Delete(this.ServiceBusEmulatorConfigPath);
            }
        }
        catch
        {
            // Best-effort cleanup of temp config file.
        }

        await this.Network.DeleteAsync().AnyContext();
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

    public StubDbContext EnsurePostgresDbContext(ITestOutputHelper output = null, bool forceNew = false)
    {
        if (this.PostgresDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();

            //if (output is not null)
            //{
            //    optionsBuilder = new DbContextOptionsBuilder<StubDbContext>()
            //        .LogTo(output.WriteLine);
            //}

            optionsBuilder.UseNpgsql(this.PostgresConnectionString);
            var context = new StubDbContext(optionsBuilder.Options);
            //context.Database.Migrate();
            context.Database.EnsureCreated();

            if (forceNew)
            {
                return context;
            }

            this.PostgresDbContext = context;
        }

        return this.PostgresDbContext;
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

            // TODO: remove file if exists?
            optionsBuilder.UseSqlite(this.SqliteConnectionString); // _{DateOnly.FromDateTime(DateTime.Now)}
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

    public StubDbContext EnsureCosmosDbContext(
        ITestOutputHelper output = null,
        string connectionString = null,
        bool forceNew = false)
    {
        if (this.CosmosDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();

            //if (output is not null)
            //{
            //    optionsBuilder = new DbContextOptionsBuilder<StubDbContext>()
            //        .LogTo(output.WriteLine);
            //}

            if (connectionString.IsNullOrEmpty())
            {
                optionsBuilder.UseCosmos(this.CosmosConnectionString, "test_ef",
                    o =>
                    {
                        o.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
                        o.HttpClientFactory(() => this.CosmosContainer.HttpClient);
                    });
            }
            else
            {
                optionsBuilder.UseCosmos(connectionString,
                    "test_ef",
                    o =>
                    {
                        o.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Direct);
                        o.HttpClientFactory(() =>
                        {
                            return new HttpClient(new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                            });
                        });
                    });
            }

            var context = new StubDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            if (forceNew)
            {
                return context;
            }

            this.CosmosDbContext = context;
        }

        return this.CosmosDbContext;
    }

    public StubDbContext EnsureInMemoryDbContext(ITestOutputHelper output = null, bool forceNew = false)
    {
        if (this.InMemoryDbContext is null || forceNew)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StubDbContext>();

            //if (output is not null)
            //{
            //    optionsBuilder = new DbContextOptionsBuilder<StubDbContext>()
            //        .LogTo(output.WriteLine);
            //}

            optionsBuilder.UseInMemoryDatabase(nameof(StubDbContext));
            var context = new StubDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            if (forceNew)
            {
                return context;
            }

            this.InMemoryDbContext = context;
        }

        return this.InMemoryDbContext;
    }

    public CosmosClient EnsureCosmosClient(string connectionString = null, bool forceNew = false)
    {
        if (this.CosmosClient is null || forceNew)
        {
            if (connectionString.IsNullOrEmpty())
            {
                // local mode (testcontainers)
                this.CosmosClient = new CosmosClient( // TODO: singleton here
                    this.CosmosConnectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway,
                        HttpClientFactory = () => this.CosmosContainer.HttpClient,
                        // TODO: systemtextjson still has issues deserializing types with no public or multiple constructors, that is an issue for ValueObjects.
                        //Serializer = new CosmosSystemTextJsonSerializer(
                        //    new()
                        //    {
                        //        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                        //        WriteIndented = true,
                        //        PropertyNameCaseInsensitive = true,
                        //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        //    }),
                        Serializer = new CosmosJsonNetSerializer(DefaultNewtonsoftSerializerSettings.Create())
                        //SerializerOptions = new CosmosSerializationOptions
                        //{
                        //    Indented = true,
                        //    IgnoreNullValues = false,
                        //    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                        //}
                    });
            }
            else
            {
                // online mode (azure)
                this.CosmosClient = new CosmosClient( // TODO: singleton here
                    connectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Direct,
                        // TODO: systemtextjson still has issues deserializing types with no public or multiple constructors, that is an issue for ValueObjects.
                        //HttpClientFactory = () => this.CosmosDbContainer.HttpClient,
                        //Serializer = new CosmosSystemTextJsonSerializer(
                        //    new()
                        //    {
                        //        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                        //        WriteIndented = true,
                        //        PropertyNameCaseInsensitive = true,
                        //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        //    }),
                        Serializer = new CosmosJsonNetSerializer(DefaultNewtonsoftSerializerSettings.Create())
                        //SerializerOptions = new CosmosSerializationOptions
                        //{
                        //    Indented = true,
                        //    IgnoreNullValues = false,
                        //    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        //}
                    });
            }
        }

        return this.CosmosClient;
    }

    public ICosmosSqlProvider<PersonStub> EnsureCosmosSqlProviderPersonStub(
        string connectionString = null,
        bool foreceNew = false)
    {
        if (this.cosmosSqlProviderPersonStub is null || foreceNew)
        {
            this.cosmosSqlProviderPersonStub = new CosmosSqlProvider<PersonStub>(o => o
                //.LoggerFactory(XunitLoggerFactory.Create(this.Output))
                .Client(this.EnsureCosmosClient(connectionString, foreceNew))
                .Database("test")
                .Container("provider_persons")
                .PartitionKey(e => e.Id)
                .DatabaseThroughPut(1000)
                .Autoscale());
            //.PartitionKey(e => e.Nationality)
            //.PartitionKey("/nationality"))
        }

        return this.cosmosSqlProviderPersonStub;
    }

    public CosmosDocumentStoreProvider EnsureCosmosDocumentStoreProvider(
        string connectionString = null,
        bool foreceNew = false)
    {
        if (this.cosmosDocumentStoreProvider is null || foreceNew)
        {
            this.cosmosDocumentStoreProvider =
                new CosmosDocumentStoreProvider(new CosmosSqlProvider<CosmosStorageDocument>(o => o
                    //.LoggerFactory(XunitLoggerFactory.Create(this.Output))
                    .Client(this.EnsureCosmosClient(connectionString, foreceNew))
                    .Database("test")
                    .Container("storage_documents")
                    .PartitionKey(e => e.Type)
                    .DatabaseThroughPut(1000)
                    .Autoscale()));
        }

        return this.cosmosDocumentStoreProvider;
    }

    private string CreateServiceBusEmulatorConfig()
    {
        var configJson = """
                         {
                           "UserConfig": {
                             "Namespaces": [
                               {
                                 "Name": "sbemulatorns",
                                  "Queues": [
                                    {
                                      "Name": "test-servicebusqueuetestmessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 3,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    },
                                    {
                                      "Name": "test-servicebusqueuebeforesubmessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 3,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    },
                                    {
                                      "Name": "test-servicebusqueueothermessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 3,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    },
                                    {
                                      "Name": "test-servicebusqueuefailmessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 3,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    },
                                    {
                                      "Name": "test-servicebusqueuepausemessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 10,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    },
                                    {
                                      "Name": "test-servicebusqueuetrackmessage",
                                      "Properties": {
                                        "DeadLetteringOnMessageExpiration": false,
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "LockDuration": "PT1M",
                                        "MaxDeliveryCount": 3,
                                        "RequiresDuplicateDetection": false,
                                        "RequiresSession": false
                                      }
                                    }
                                  ],
                                  "Topics": [
                                    {
                                      "Name": "messagestub_test",
                                      "Properties": {
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "DuplicateDetectionHistoryTimeWindow": "PT5M"
                                      },
                                      "Subscriptions": [
                                        {
                                          "Name": "messagestub",
                                          "Properties": {
                                            "DeadLetteringOnMessageExpiration": false,
                                            "DefaultMessageTimeToLive": "PT1H",
                                            "LockDuration": "PT1M",
                                            "MaxDeliveryCount": 3,
                                            "RequiresSession": false
                                          }
                                        }
                                      ]
                                    },
                                    {
                                      "Name": "servicebustestmessage_test",
                                      "Properties": {
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "DuplicateDetectionHistoryTimeWindow": "PT5M"
                                      },
                                      "Subscriptions": [
                                        {
                                          "Name": "servicebustestmessage",
                                          "Properties": {
                                            "DeadLetteringOnMessageExpiration": false,
                                            "DefaultMessageTimeToLive": "PT1H",
                                            "LockDuration": "PT1M",
                                            "MaxDeliveryCount": 3,
                                            "RequiresSession": false
                                          }
                                        }
                                      ]
                                    },
                                    {
                                      "Name": "servicebusothertestmessage_test",
                                      "Properties": {
                                        "DefaultMessageTimeToLive": "PT1H",
                                        "DuplicateDetectionHistoryTimeWindow": "PT5M"
                                      },
                                      "Subscriptions": [
                                        {
                                          "Name": "servicebusothertestmessage",
                                          "Properties": {
                                            "DeadLetteringOnMessageExpiration": false,
                                            "DefaultMessageTimeToLive": "PT1H",
                                            "LockDuration": "PT1M",
                                            "MaxDeliveryCount": 3,
                                            "RequiresSession": false
                                          }
                                        }
                                      ]
                                    }
                                  ]
                               }
                             ],
                             "Logging": {
                               "Type": "Console"
                             }
                           }
                         }
                         """;

        var tempPath = Path.Combine(Path.GetTempPath(), $"sb-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, configJson);
        return tempPath;
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