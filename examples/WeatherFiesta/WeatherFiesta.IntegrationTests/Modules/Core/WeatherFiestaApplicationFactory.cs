// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

/// <summary>
/// Custom WebApplicationFactory for WeatherFiesta integration tests.
/// Uses a test EF Core database so real handlers, ActiveEntity, and IRequester pipeline
/// run against isolated data. Only external HTTP services are mocked.
/// </summary>
public class WeatherFiestaApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string dbName = $"WeatherFiestaTestDb_{Guid.NewGuid():N}";
    private readonly MsSqlContainer sqlContainer;
    private string sqlServerConnectionString;
    private bool seeded;
    private ITestOutputHelper output;

    public WeatherFiestaApplicationFactory()
    {
        this.sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    }

    internal WeatherFiestaApplicationFactory(string sqlServerConnectionString)
    {
        this.sqlServerConnectionString = sqlServerConnectionString;
    }

    /// <summary>Gets the mocked weather agent (external HTTP service).</summary>
    public IWeatherAgent WeatherAgent { get; } = Substitute.For<IWeatherAgent>();

    /// <summary>Gets the mocked geocoding client (external HTTP service).</summary>
    public IWeatherGeocodingClient GeocodingClient { get; } = Substitute.For<IWeatherGeocodingClient>();

    /// <summary>Gets the fake current user accessor.</summary>
    public ICurrentUserAccessor CurrentUserAccessor { get; private set; }

    /// <summary>
    /// Sets the xUnit test output helper for logging integration test output.
    /// Must be called before any service resolution or client creation.
    /// </summary>
    public void SetOutput(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (this.sqlContainer is not null)
        {
            await this.sqlContainer.StartAsync();
            this.sqlServerConnectionString = new SqlConnectionStringBuilder(this.sqlContainer.GetConnectionString())
            {
                InitialCatalog = $"WeatherFiesta_{Guid.NewGuid():N}"
            }.ConnectionString;
        }

        await SeedAsync();
    }

    /// <inheritdoc/>
    public new async Task DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(this.sqlServerConnectionString))
            {
                using var scope = this.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
                await dbContext.Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            await base.DisposeAsync();
            if (this.sqlContainer is not null)
            {
                await this.sqlContainer.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Seeds the test database with test data (idempotent per factory instance).
    /// </summary>
    public async Task SeedAsync()
    {
        if (this.seeded)
        {
            return;
        }

        using var scope = this.Services.CreateScope();
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.Services);
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await TestData.SeedAsync(dbContext);
        this.seeded = true;
    }

    /// <summary>
    /// Resets the test database by deleting and recreating it.
    /// Each factory has a unique DB name so this only affects this instance.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = this.Services.CreateScope();
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.Services);
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await TestData.SeedAsync(dbContext);
        this.seeded = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove DbContext registrations from the application host.
            // Remove by index (backwards) because ServiceDescriptor.Equals checks
            // factory delegates, making Remove(descriptor) fail for factory-registered services.
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var st = services[i].ServiceType;
                var it = services[i].ImplementationType;
                var useInMemory = string.IsNullOrWhiteSpace(this.sqlServerConnectionString);
                var isSqlServerProviderRegistration = it?.Assembly?.GetName().Name == "Microsoft.EntityFrameworkCore.SqlServer" ||
                    (st.IsGenericType && st.GenericTypeArguments.Any(
                        t => t?.Assembly?.GetName().Name == "Microsoft.EntityFrameworkCore.SqlServer"));

                if (st == typeof(DbContextOptions<CoreDbContext>) ||
                    st == typeof(DbContextOptions) ||
                    st == typeof(CoreDbContext) ||
                    st == typeof(IDbContextResolver) ||
                    (!string.IsNullOrWhiteSpace(this.sqlServerConnectionString) && it == typeof(DbContextResolver)) ||
                    (useInMemory && isSqlServerProviderRegistration))
                {
                    services.RemoveAt(i);
                }
            }

            // Remove ALL hosted services. In integration tests we don't need
            // DatabaseCreatorService, OutboxDomainEventService, or StartupTasks.
            // These are registered via factory delegates (ImplementationType is null),
            // so we cannot filter by type name — just remove all IHostedService registrations.
            for (var i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == typeof(IHostedService))
                {
                    services.RemoveAt(i);
                }
            }

            // Remove startup task definitions (seeders)
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var d = services[i];
                if (d.ImplementationType?.Name?.Contains("Seeder") == true ||
                    d.ImplementationType?.Name?.Contains("StartupTaskDefinition") == true ||
                    d.ServiceType?.Name?.Contains("StartupTaskDefinition") == true)
                {
                    services.RemoveAt(i);
                }
            }

            if (string.IsNullOrWhiteSpace(this.sqlServerConnectionString))
            {
                // Register InMemory DbContext with a unique DB name per factory instance.
                // Using manual registration (singleton options + scoped context) instead of
                // AddDbContext to avoid EF Core's IDatabaseProvider singleton conflict.
                // AddDbContext registers provider services that clash when SqlServer was registered first.
                var inMemoryOptions = new DbContextOptionsBuilder<CoreDbContext>()
                    .UseInMemoryDatabase(this.dbName).Options;
                services.AddSingleton(inMemoryOptions);
                services.AddScoped<CoreDbContext>();
            }
            else
            {
                services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(this.sqlServerConnectionString));
            }

            // Register IDbContextResolver (needed by DatabaseTransactionPipelineBehavior).
            // AddSqlServerDbContext normally registers this, but we removed SqlServer.
            services.AddScoped<IDbContextResolver, DbContextResolver>();

            if (!string.IsNullOrWhiteSpace(this.sqlServerConnectionString))
            {
                RemoveActiveEntityRegistrations(services);
                services.AddActiveEntities();
            }

            // Mock external HTTP services only
            ReplaceService(services, this.WeatherAgent);
            ReplaceService(services, this.GeocodingClient);

            // Fake current user accessor
            this.CurrentUserAccessor = CreateFakeCurrentUserAccessor();
            ReplaceService(services, this.CurrentUserAccessor);

            // Add test authentication so [RequireAuthorization] passes
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                "TestScheme", _ => { });

            // Configure logging with xUnit output
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                if (this.output is not null)
                {
                    logging.AddProvider(new XunitLoggerProvider(this.output));
                }
            });
        });
    }

    /// <summary>
    /// Removes ActiveEntity registrations so SQL tests can re-add the shared Core module registrations.
    /// </summary>
    private static void RemoveActiveEntityRegistrations(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceType = services[i].ServiceType;
            if (serviceType.IsGenericType &&
                (serviceType.GetGenericTypeDefinition() == typeof(IActiveEntityEntityProvider<,>) ||
                 serviceType.GetGenericTypeDefinition() == typeof(IActiveEntityBehavior<>)))
            {
                services.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Replaces all registrations of TService with a singleton instance.
    /// </summary>
    private static void ReplaceService<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                services.RemoveAt(i);
            }
        }

        services.AddSingleton(instance);
    }

    /// <summary>
    /// Creates a fake ICurrentUserAccessor that returns a test user.
    /// </summary>
    private static ICurrentUserAccessor CreateFakeCurrentUserAccessor()
    {
        var fake = Substitute.For<ICurrentUserAccessor>();
        fake.UserId.Returns(TestData.TestUserId);
        fake.UserName.Returns("Test User");
        fake.IsAuthenticated.Returns(true);
        return fake;
    }
}

/// <summary>
/// Test authentication handler that returns a fully authenticated user
/// with both regular and admin roles for integration testing.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestData.TestUserId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, Role.Administrators)
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
