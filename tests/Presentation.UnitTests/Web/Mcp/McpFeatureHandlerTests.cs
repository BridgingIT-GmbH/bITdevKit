namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Mcp;

using System.Text.Json;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Jobs;
using BridgingIT.DevKit.Presentation.Web.Messaging;
using BridgingIT.DevKit.Presentation.Web.Orchestrations;
using BridgingIT.DevKit.Presentation.Web.Queueing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[UnitTest("Presentation")]
public sealed class McpFeatureHandlerTests
{
    [Fact]
    public void AddMcpHandler_WhenCalledTwice_RegistersHandlerOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMcpHandler<TestMcpHandler>();
        services.AddMcpHandler<TestMcpHandler>();

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(IMcpHandler)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(McpStartupDiagnosticsService)).ShouldBe(1);
    }

    [Fact]
    public void AddMcpHandlersFromAssembly_WhenCalled_RegistersHandlersAndStartupDiagnostics()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMcpHandlersFromAssembly<TestMcpHandler>();

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) &&
            descriptor.ImplementationType == typeof(TestMcpHandler));
        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(McpStartupDiagnosticsService)).ShouldBe(1);
    }

    [Fact]
    public void AddMcp_WhenLocalMcpIsEnabled_RegistersConfiguredHandlers()
    {
        // Arrange
        var sut = DevKitWebApplication.CreateBuilder([], options => options
            .Cli(cli => cli.AllowOutsideDevelopmentForTests()));

        // Act
        sut.AddMcp(mcp => mcp.WithHandler<TestMcpHandler>());

        // Assert
        sut.Services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) &&
            descriptor.ImplementationType == typeof(TestMcpHandler));
    }

    [Fact]
    public void AddMcp_WhenLocalMcpIsDisabled_DoesNotRegisterConfiguredHandlers()
    {
        // Arrange
        var sut = DevKitWebApplication.CreateBuilder([], options => options
            .Cli(cli => cli
                .AllowOutsideDevelopmentForTests()
                .Mcp(false)));

        // Act
        sut.AddMcp(mcp => mcp.WithHandler<TestMcpHandler>());

        // Assert
        sut.Services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) &&
            descriptor.ImplementationType == typeof(TestMcpHandler));
    }

    [Fact]
    public void AddMcp_WhenEnabledOverrideIsTrue_RegistersConfiguredHandlers()
    {
        // Arrange
        var sut = DevKitWebApplication.CreateBuilder([], options => options
            .Cli(cli => cli
                .AllowOutsideDevelopmentForTests()
                .Mcp(false)));

        // Act
        sut.AddMcp(mcp => mcp
            .Enabled()
            .WithHandler<TestMcpHandler>());

        // Assert
        sut.Services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) &&
            descriptor.ImplementationType == typeof(TestMcpHandler));
    }

    [Fact]
    public async Task McpStartupDiagnosticsService_WhenStarted_LogsHandlersUnderBdkCategoryAtDebug()
    {
        // Arrange
        var loggerFactory = new RecordingLoggerFactory();
        var services = new ServiceCollection()
            .AddTransient<IMcpHandler, TestMcpHandler>()
            .BuildServiceProvider();
        var sut = new McpStartupDiagnosticsService(services.GetRequiredService<IServiceScopeFactory>(), loggerFactory);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var entry = loggerFactory.Entries.ShouldHaveSingleItem();
        entry.Category.ShouldBe("BDK");
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.Message.ShouldContain("mcp handlers registered");
        entry.Message.ShouldContain($"{nameof(TestMcpHandler)}[");
        entry.Message.ShouldNotContain(typeof(TestMcpHandler).FullName);
        entry.Message.ShouldContain("test.operation:diagnostics:test");
    }

    [Fact]
    public async Task McpStartupDiagnosticsService_WhenHandlerHasScopedDependency_StartsWithoutLifetimeViolation()
    {
        // Arrange
        var loggerFactory = new RecordingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddScoped<ScopedDependency>();
        services.AddMcpHandler<ScopedDependencyMcpHandler>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var sut = provider.GetServices<IHostedService>().OfType<McpStartupDiagnosticsService>().ShouldHaveSingleItem();

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var entry = loggerFactory.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldContain($"{nameof(ScopedDependencyMcpHandler)}[");
        entry.Message.ShouldContain("scoped.operation:diagnostics:test");
    }

    [Fact]
    public void McpHostFeatureEndpointContributor_WhenHandlerHasScopedDependency_ReturnsEndpointMetadata()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new RecordingLoggerFactory());
        services.AddScoped<ScopedDependency>();
        services.AddMcpHandler<ScopedDependencyMcpHandler>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var sut = new McpHostFeatureEndpointContributor(new LocalIpcEndpointState(new HostDescriptorOptions
        {
            RuntimeId = "test-runtime",
            RegistryPath = Path.GetTempPath()
        }));

        // Act
        var result = sut.GetEndpointMetadata(provider);

        // Assert
        result.ShouldNotBeNull();
        result.Transport.ShouldBe(OperatingSystem.IsWindows() ? "named-pipe" : "unix-socket");
    }

    [Fact]
    public async Task McpDispatcher_WhenHandlerHasScopedDependency_DispatchesFromScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ScopedDependency>();
        services.AddMcpHandler<ScopedDependencyMcpHandler>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var sut = new McpDispatcher(provider, provider.GetRequiredService<ILogger<McpDispatcher>>());

        // Act
        var capabilities = await sut.DispatchAsync(new McpRequest("mcp.capabilities", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);
        var operation = await sut.DispatchAsync(new McpRequest("scoped.operation", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        capabilities.Available.ShouldBeTrue();
        JsonSerializer.Serialize(capabilities.Data).ShouldContain("scoped.operation");
        operation.Available.ShouldBeTrue();
        operation.Summary.ShouldBe("Handled.");
    }

    [Fact]
    public async Task RuntimeDiagnosticsMcpHandler_WhenProjectSummaryIsRequested_ReturnsModulesAndCapabilities()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.ApplicationName.Returns("TestApp");
        environment.EnvironmentName.Returns("Development");
        environment.ContentRootPath.Returns(Path.GetTempPath());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(environment);
        services.AddSingleton<IModule>(new TestModule());
        services.AddMcpHandler<RuntimeDiagnosticsMcpHandler>();
        services.AddMcpHandler<ProjectMcpHandler>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var sut = new McpDispatcher(provider, provider.GetRequiredService<ILogger<McpDispatcher>>());

        // Act
        var response = await sut.DispatchAsync(new McpRequest("project.summary", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("TestApp");
        json.ShouldContain("orders");
        json.ShouldContain("orders_inspect_customer");
        json.ShouldContain("projectOperationCount");
    }

    [Fact]
    public void McpDashboardPageProvider_WhenMcpBridgeIsMissing_HidesPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new DashboardEndpointsOptions());
        services.AddScoped<ScopedDependency>();
        services.AddMcpHandler<ScopedDependencyMcpHandler>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var context = new DefaultHttpContext { RequestServices = provider };
        var sut = new BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard.McpDashboardPageProvider(provider.GetRequiredService<DashboardEndpointsOptions>());

        // Act
        var pages = sut.GetPages(context).ToArray();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public void McpDashboardPageProvider_WhenMcpBridgeAndHandlersAreRegistered_ShowsPage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new DashboardEndpointsOptions());
        services.AddSingleton(new HostDescriptorOptions
        {
            RegistryPath = Path.GetTempPath(),
            RuntimeId = "test-runtime",
            StartedAt = DateTimeOffset.UtcNow
        });
        services.AddSingleton<LocalIpcEndpointState>();
        services.AddSingleton<McpDispatcher>();
        services.AddScoped<ScopedDependency>();
        services.AddMcpHandler<ScopedDependencyMcpHandler>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var context = new DefaultHttpContext { RequestServices = provider };
        var sut = new BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard.McpDashboardPageProvider(provider.GetRequiredService<DashboardEndpointsOptions>());

        // Act
        var page = sut.GetPages(context).ShouldHaveSingleItem();

        // Assert
        page.Title.ShouldBe("MCP");
        page.Url.ShouldBe("/_bdk/dashboard/mcp");
    }

    [Fact]
    public async Task MessagingMcpHandler_WhenPurgeLacksConfirmation_ReturnsConfirmationRequired()
    {
        // Arrange
        var broker = Substitute.For<IMessageBrokerService>();
        var sut = new MessagingMcpHandler(broker);

        // Act
        var response = await sut.HandleAsync(new McpRequest("messages.purge", McpToolset.Admin, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe("confirmation_required");
        await broker.DidNotReceiveWithAnyArgs().PurgeMessagesAsync(default, default, default, default);
    }

    [Fact]
    public async Task QueueingMcpHandler_WhenPurgeLacksConfirmation_ReturnsConfirmationRequired()
    {
        // Arrange
        var broker = Substitute.For<IQueueBrokerService>();
        var sut = new QueueingMcpHandler(broker);

        // Act
        var response = await sut.HandleAsync(new McpRequest("queueing.purge", McpToolset.Admin, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe("confirmation_required");
        await broker.DidNotReceiveWithAnyArgs().PurgeMessagesAsync(default, default, default, default);
    }

    [Fact]
    public async Task JobSchedulerMcpHandler_WhenPurgeRunsLacksConfirmation_ReturnsConfirmationRequired()
    {
        // Arrange
        var query = Substitute.For<IJobSchedulerQueryService>();
        var scheduler = Substitute.For<IJobSchedulerService>();
        var maintenance = Substitute.For<IJobSchedulerMaintenanceService>();
        var sut = new JobSchedulerMcpHandler(query, scheduler, maintenance);

        // Act
        var response = await sut.HandleAsync(new McpRequest("jobs.purgeRuns", McpToolset.Admin, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe("confirmation_required");
        await maintenance.DidNotReceiveWithAnyArgs().PurgeOccurrencesAsync(default, default);
    }

    [Fact]
    public async Task OrchestrationMcpHandler_WhenPurgeLacksConfirmation_ReturnsConfirmationRequired()
    {
        // Arrange
        var query = Substitute.For<IOrchestrationQueryService>();
        var runtime = Substitute.For<IOrchestrationService>();
        var administration = Substitute.For<IOrchestrationAdministrationService>();
        var sut = new OrchestrationMcpHandler(query, runtime, administration);

        // Act
        var response = await sut.HandleAsync(new McpRequest("orchestrations.purge", McpToolset.Admin, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe("confirmation_required");
        await administration.DidNotReceiveWithAnyArgs().PurgeAsync(default, default);
    }

    [Fact]
    public async Task RuntimeDiagnosticsMcpHandler_WhenMetricsAreMissing_ReturnsFeatureUnavailable()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new RuntimeDiagnosticsMcpHandler(services);

        // Act
        var response = await sut.HandleAsync(new McpRequest("metrics.snapshot", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.FeatureUnavailable);
    }

    private static JsonElement EmptyJson()
        => JsonDocument.Parse("{}").RootElement;

    private sealed class TestMcpHandler : IMcpHandler
    {
        public IReadOnlyCollection<McpCapability> Capabilities { get; } =
        [
            new("test.operation", McpToolset.Diagnostics, "test", "Test operation.")
        ];

        public ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(McpResponse.Success("Handled."));
    }

    private sealed class ScopedDependency
    {
    }

    private sealed class TestModule : ModuleBase
    {
        public TestModule()
            : base("orders", 10)
        {
            this.IsRegistered = true;
        }

        public override IServiceCollection Register(IServiceCollection services, IConfiguration configuration = null, Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment = null)
            => services;

        public override IApplicationBuilder Use(IApplicationBuilder app, IConfiguration configuration = null, Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment = null)
            => app;
    }

    private sealed class ProjectMcpHandler : IMcpHandler
    {
        public IReadOnlyCollection<McpCapability> Capabilities { get; } =
        [
            new("orders_inspect_customer", McpToolset.Diagnostics, "project", "Inspects customer order state.")
            {
                Owner = "orders",
                Category = "inspect"
            }
        ];

        public ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(McpResponse.Success("Handled."));
    }

    private sealed class ScopedDependencyMcpHandler(ScopedDependency dependency) : IMcpHandler
    {
        public IReadOnlyCollection<McpCapability> Capabilities { get; } =
        [
            new("scoped.operation", McpToolset.Diagnostics, "test", $"Uses {dependency.GetType().Name}.")
        ];

        public ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(McpResponse.Success("Handled."));
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(categoryName, this.Entries);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(string category, List<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            entries.Add(new LogEntry(category, logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
