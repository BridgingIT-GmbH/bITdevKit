// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

/// <summary>
/// SQL Server-backed application test host for direct requester tests.
/// It wires application services without starting ASP.NET Core or endpoint infrastructure.
/// </summary>
public sealed class WeatherFiestaApplicationTestHost : IAsyncDisposable
{
    private readonly string connectionString;
    private readonly ITestOutputHelper output;
    private ServiceProvider serviceProvider;

    public WeatherFiestaApplicationTestHost(string connectionString, ITestOutputHelper output)
    {
        this.connectionString = connectionString;
        this.output = output;
    }

    /// <summary>Gets the application service provider.</summary>
    public IServiceProvider Services => this.serviceProvider;

    /// <summary>Gets the requester used by direct application tests.</summary>
    public IRequester Requester => this.Services.GetRequiredService<IRequester>();

    /// <summary>Gets the mocked weather agent.</summary>
    public IWeatherAgent WeatherAgent { get; } = Substitute.For<IWeatherAgent>();

    /// <summary>Gets the mocked geocoding client.</summary>
    public IWeatherGeocodingClient GeocodingClient { get; } = Substitute.For<IWeatherGeocodingClient>();

    /// <summary>Builds the application-only service provider.</summary>
    public void Build()
    {
        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            logging.AddProvider(new XunitLoggerProvider(this.output));
        });

        services.AddOptions();
        services.Configure<CoreModuleConfiguration>(options =>
        {
            options.ConnectionStrings = new Dictionary<string, string> { ["Default"] = this.connectionString };
        });

        services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(this.connectionString));
        services.AddScoped<IDbContextResolver, DbContextResolver>();

        services.AddMapping().WithMapster<CoreModuleMapperRegister>();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(MetricsRequestBehavior<,>))
            .WithBehavior(typeof(TracingBehavior<,>))
            .WithBehavior(typeof(ModuleScopeBehavior<,>))
            .WithBehavior(typeof(DatabaseTransactionPipelineBehavior<,>))
            .WithBehavior(typeof(ValidationPipelineBehavior<,>))
            .WithBehavior(typeof(RetryPipelineBehavior<,>))
            .WithBehavior(typeof(TimeoutPipelineBehavior<,>));

        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(MetricsNotificationBehavior<,>))
            .WithBehavior(typeof(MetricsNotificationHandlerBehavior<,>))
            .WithBehavior(typeof(TracingBehavior<,>))
            .WithBehavior(typeof(ModuleScopeBehavior<,>))
            .WithBehavior(typeof(DatabaseTransactionPipelineBehavior<,>))
            .WithBehavior(typeof(ValidationPipelineBehavior<,>))
            .WithBehavior(typeof(RetryPipelineBehavior<,>))
            .WithBehavior(typeof(TimeoutPipelineBehavior<,>));

        services.AddActiveEntities();

        services.AddSingleton(this.WeatherAgent);
        services.AddSingleton(this.GeocodingClient);
        services.AddScoped(_ => CreateFakeCurrentUserAccessor());

        this.serviceProvider = services.BuildServiceProvider();
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.serviceProvider);
    }

    /// <summary>Resets and seeds the SQL Server database.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = this.Services.CreateScope();
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.Services);
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await TestData.SeedAsync(dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.serviceProvider is null)
        {
            return;
        }

        try
        {
            using var scope = this.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }
        finally
        {
            await this.serviceProvider.DisposeAsync();
        }
    }

    private static ICurrentUserAccessor CreateFakeCurrentUserAccessor()
    {
        var fake = Substitute.For<ICurrentUserAccessor>();
        fake.UserId.Returns(TestData.TestUserId);
        fake.UserName.Returns("Test User");
        fake.IsAuthenticated.Returns(true);
        return fake;
    }
}
