// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class EndpointRegistrationTests
{
    [Fact]
    public void AddEndpoints_InstanceEnabled_RegistersEndpointInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var endpoint = new CountingEndpoints("test");

        // Act
        services.AddEndpoints(endpoint);

        // Assert
        var registered = services
            .BuildServiceProvider()
            .GetServices<IEndpoints>()
            .Single();

        registered.ShouldBeSameAs(endpoint);
    }

    [Fact]
    public void AddEndpoints_Disabled_DoesNotRegisterEndpointInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var endpoint = new CountingEndpoints("test");

        // Act
        services.AddEndpoints(endpoint, enabled: false);

        // Assert
        services.BuildServiceProvider().GetServices<IEndpoints>().ShouldBeEmpty();
    }

    [Fact]
    public void AddEndpoints_TypeEnabled_RegistersConcreteEndpointType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpoints<RegisteredEndpoints>();

        // Assert
        services
            .BuildServiceProvider()
            .GetServices<IEndpoints>()
            .Single()
            .ShouldBeOfType<RegisteredEndpoints>();
    }

    [Fact]
    public void AddEndpoints_MultipleCalls_RegistersOneStartupDiagnosticsService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpoints<RegisteredEndpoints>();
        services.AddEndpoints<AdditionalEndpoints>();

        // Assert
        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(EndpointStartupDiagnosticsService))
            .ShouldBe(1);
    }

    [Fact]
    public async Task EndpointStartupDiagnosticsService_WhenStarted_LogsOneEndpointSummary()
    {
        // Arrange
        var loggerFactory = new RecordingLoggerFactory();
        IEndpoints[] endpoints = [new RegisteredEndpoints(), new AdditionalEndpoints()];
        var sut = new EndpointStartupDiagnosticsService(endpoints, loggerFactory);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var entry = loggerFactory.Entries.ShouldHaveSingleItem();
        entry.Category.ShouldBe("REQ");
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.Message.ShouldBe(
            "[REQ] api endpoints added (count=2, endpoints=AdditionalEndpoints,RegisteredEndpoints)");
    }

    [Fact]
    public async Task EndpointStartupDiagnosticsService_DuplicateTypeNames_QualifiesEndpointNames()
    {
        // Arrange
        var loggerFactory = new RecordingLoggerFactory();
        IEndpoints[] endpoints =
        [
            new FirstEndpointGroup.DashboardEndpoints(),
            new SecondEndpointGroup.DashboardEndpoints()
        ];
        var sut = new EndpointStartupDiagnosticsService(endpoints, loggerFactory);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var message = loggerFactory.Entries.ShouldHaveSingleItem().Message;
        message.ShouldContain("FirstEndpointGroup.DashboardEndpoints");
        message.ShouldContain("SecondEndpointGroup.DashboardEndpoints");
    }

    [Fact]
    public async Task EndpointStartupDiagnosticsService_DevKitEndpoints_LogsSeparateShortenedSummary()
    {
        // Arrange
        var loggerFactory = new RecordingLoggerFactory();
        IEndpoints[] endpoints =
        [
            new RegisteredEndpoints(),
            new SystemEndpoints(),
            new BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardEndpoints(
                new BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions())
        ];
        var sut = new EndpointStartupDiagnosticsService(endpoints, loggerFactory);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        loggerFactory.Entries.Count.ShouldBe(2);
        loggerFactory.Entries.ShouldContain(entry =>
            entry.Category == "REQ" &&
            entry.Message == "[REQ] api endpoints added (count=1, endpoints=RegisteredEndpoints)");
        loggerFactory.Entries.ShouldContain(entry =>
            entry.Category == "REQ" &&
            entry.Message ==
            "[REQ] system api endpoints added (count=2, endpoints=Storage.Blobs.Dashboard.DashboardEndpoints,SystemEndpoints)");
        loggerFactory.Entries.ShouldAllBe(entry =>
            !entry.Message.Contains("BridgingIT.DevKit.Presentation.Web", StringComparison.Ordinal));
    }

    [Fact]
    public void MapEndpoints_RegisteredEndpoints_MapsOnlyEnabledAndUnregisteredEndpointsOnce()
    {
        // Arrange
        var enabledEndpoint = new CountingEndpoints("enabled");
        var disabledEndpoint = new CountingEndpoints("disabled") { Enabled = false };
        var alreadyRegisteredEndpoint = new CountingEndpoints("registered") { IsRegistered = true };
        var app = CreateApplication(services => services.AddEndpoints([enabledEndpoint, disabledEndpoint, alreadyRegisteredEndpoint]));

        // Act
        app.MapEndpoints();
        app.MapEndpoints();

        // Assert
        enabledEndpoint.MapCount.ShouldBe(1);
        enabledEndpoint.IsRegistered.ShouldBeTrue();
        disabledEndpoint.MapCount.ShouldBe(0);
        disabledEndpoint.IsRegistered.ShouldBeFalse();
        alreadyRegisteredEndpoint.MapCount.ShouldBe(0);
        alreadyRegisteredEndpoint.IsRegistered.ShouldBeTrue();
    }

    [Fact]
    public void MapEndpoints_RouteGroupBuilderProvided_MapsEndpointsIntoProvidedGroup()
    {
        // Arrange
        var endpoint = new CountingEndpoints("ping");
        var app = CreateApplication(services => services.AddEndpoints(endpoint));
        var group = app.MapGroup("api/test");

        // Act
        app.MapEndpoints(group);

        // Assert
        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();

        route.RoutePattern.RawText.ShouldBe("api/test/ping");
        endpoint.MapCount.ShouldBe(1);
        endpoint.IsRegistered.ShouldBeTrue();
    }

    private static WebApplication CreateApplication(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        configureServices(builder.Services);

        return builder.Build();
    }

    private sealed class CountingEndpoints(string route) : IEndpoints
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; }

        public int MapCount { get; private set; }

        public void Map(IEndpointRouteBuilder app)
        {
            this.MapCount++;
            app.MapGet(route, () => Results.Ok());
        }
    }

    private sealed class RegisteredEndpoints : IEndpoints
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; }

        public void Map(IEndpointRouteBuilder app)
        {
        }
    }

    private sealed class AdditionalEndpoints : IEndpoints
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; }

        public void Map(IEndpointRouteBuilder app)
        {
        }
    }

    private static class FirstEndpointGroup
    {
        public sealed class DashboardEndpoints : IEndpoints
        {
            public bool Enabled { get; set; } = true;

            public bool IsRegistered { get; set; }

            public void Map(IEndpointRouteBuilder app)
            {
            }
        }
    }

    private static class SecondEndpointGroup
    {
        public sealed class DashboardEndpoints : IEndpoints
        {
            public bool Enabled { get; set; } = true;

            public bool IsRegistered { get; set; }

            public void Map(IEndpointRouteBuilder app)
            {
            }
        }
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
