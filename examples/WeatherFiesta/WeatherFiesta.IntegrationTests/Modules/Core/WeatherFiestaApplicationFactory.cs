// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for WeatherFiesta integration tests.
/// Uses InMemory EF Core so real handlers, ActiveEntity, and IRequester pipeline
/// run against a test database. Only external HTTP services are mocked.
/// Each factory instance gets a unique InMemory DB name for isolation.
/// </summary>
public class WeatherFiestaApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string dbName = $"WeatherFiestaTestDb_{Guid.NewGuid():N}";
    private bool seeded;
    private ITestOutputHelper output;

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
        await SeedAsync();
    }

    /// <inheritdoc/>
    public new async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Seeds the InMemory database with test data (idempotent per factory instance).
    /// </summary>
    public async Task SeedAsync()
    {
        if (this.seeded)
        {
            return;
        }

        using var scope = this.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await TestData.SeedAsync(dbContext);
        this.seeded = true;
    }

    /// <summary>
    /// Resets the InMemory database by deleting and recreating it.
    /// Each factory has a unique DB name so this only affects this instance.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = this.Services.CreateScope();
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
            // Remove SqlServer-related registrations.
            // Remove by index (backwards) because ServiceDescriptor.Equals checks
            // factory delegates, making Remove(descriptor) fail for factory-registered services.
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var st = services[i].ServiceType;
                var it = services[i].ImplementationType;
                if (st == typeof(DbContextOptions<CoreDbContext>) ||
                    st == typeof(DbContextOptions) ||
                    st == typeof(CoreDbContext) ||
                    it?.Assembly?.GetName().Name == "Microsoft.EntityFrameworkCore.SqlServer" ||
                    (st.IsGenericType &&
                     st.GenericTypeArguments.Any(
                         t => t?.Assembly?.GetName().Name == "Microsoft.EntityFrameworkCore.SqlServer")))
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

            // Remove Quartz job scheduling
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var d = services[i];
                if (d.ServiceType.Name.Contains("Quartz") ||
                    d.ImplementationType?.Name?.Contains("Quartz") == true ||
                    d.ImplementationType?.FullName?.Contains("Quartz") == true)
                {
                    services.RemoveAt(i);
                }
            }

            // Register InMemory DbContext with a unique DB name per factory instance.
            // Using manual registration (singleton options + scoped context) instead of
            // AddDbContext to avoid EF Core's IDatabaseProvider singleton conflict.
            // AddDbContext registers provider services that clash when SqlServer was registered first.
            var inMemoryOptions = new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(this.dbName).Options;
            services.AddSingleton(inMemoryOptions);
            services.AddScoped<CoreDbContext>();

            // Register IDbContextResolver (needed by DatabaseTransactionPipelineBehavior).
            // AddSqlServerDbContext normally registers this, but we removed SqlServer.
            services.AddScoped<IDbContextResolver, DbContextResolver>();

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
            new Claim(ClaimTypes.Role, "CoreAdmin")
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
