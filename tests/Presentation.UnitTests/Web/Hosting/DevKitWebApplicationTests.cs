namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Hosting;

using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Presentation")]
public class DevKitWebApplicationTests
{
    [Fact]
    public void CreateBuilder_ReturnsDevKitApplicationBuilder()
    {
        // Arrange & Act
        var sut = DevKitWebApplication.CreateBuilder([]);

        // Assert
        sut.Services.ShouldNotBeNull();
        sut.Configuration.ShouldNotBeNull();
        sut.Environment.ShouldNotBeNull();
        sut.WebApplicationBuilder.ShouldNotBeNull();
        sut.Properties.ContainsKey(DevKitBuilderProperties.HostBuilder).ShouldBeTrue();
        sut.Properties.ContainsKey(DevKitBuilderProperties.LoggingBuilder).ShouldBeTrue();
        sut.Properties[DevKitBuilderProperties.HostBuilder].ShouldBeAssignableTo<IHostBuilder>();
        sut.ShouldBeAssignableTo<IDevKitApplicationBuilder>();
    }

    [Fact]
    public void CreateBuilder_WhenConfigured_AppliesCliOptions()
    {
        // Arrange & Act
        var sut = DevKitWebApplication.CreateBuilder([], options => options
            .Cli(cli => cli
                .ConsoleCommands(false)
                .Mcp(false)));

        // Assert
        sut.Options.Cli.ConsoleCommandsEnabled.ShouldBeFalse();
        sut.Options.Cli.McpEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Configure_InvokesCallbackAndReturnsSameBuilder()
    {
        // Arrange
        var sut = DevKitWebApplication.CreateBuilder([]);

        // Act
        var result = sut.Configure(builder => builder.Properties["test"] = "configured");

        // Assert
        result.ShouldBeSameAs(sut);
        sut.Properties["test"].ShouldBe("configured");
    }

    [Fact]
    public void CreateBuilder_WhenLocalToolingEligible_RegistersDescriptorAndIpcServices()
    {
        // Arrange & Act
        var sut = DevKitWebApplication.CreateBuilder([], options => options
            .Cli(cli => cli.AllowOutsideDevelopmentForTests()));

        using var host = sut.Build();
        var hostedServices = host.Services.GetServices<IHostedService>().ToArray();

        // Assert
        host.Services.GetService<HostRuntimeDescriptorWriter>().ShouldNotBeNull();
        hostedServices.ShouldContain(service => service is HostDescriptorLifecycleService);
        hostedServices.ShouldContain(service => service is HostConsoleCommandIpcServer);
        hostedServices.ShouldContain(service => service is McpIpcServer);
    }

    [Fact]
    public async Task HostRuntimeDescriptorWriter_WritesAndRemovesDescriptor()
    {
        // Arrange
        var registryPath = Path.Combine(Path.GetTempPath(), "bdk-tests", Guid.NewGuid().ToString("N"));
        var options = new HostDescriptorOptions
        {
            RegistryPath = registryPath,
            RuntimeId = "test-runtime",
            WorkspacePath = registryPath,
            ProjectPath = Path.Combine(registryPath, "Test.csproj"),
            StartedAt = DateTimeOffset.UtcNow
        };
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ApplicationName.Returns("TestApp");
        environment.EnvironmentName.Returns(Environments.Development);
        environment.ContentRootPath.Returns(registryPath);
        var endpoint = new HostFeatureEndpointMetadata
        {
            ProtocolVersion = 1,
            Transport = "named-pipe",
            Endpoint = "bdk-test-runtime-console",
            Nonce = "nonce"
        };
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var sut = new HostRuntimeDescriptorWriter(
            environment,
            options,
            [new TestEndpointContributor(endpoint)],
            NullLogger<HostRuntimeDescriptorWriter>.Instance);

        // Act
        await sut.WriteAsync(services);
        var descriptorPath = HostDescriptorPath.GetDescriptorPath(options);
        var descriptor = JsonSerializer.Deserialize<HostRuntimeDescriptor>(await File.ReadAllTextAsync(descriptorPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sut.Remove();

        // Assert
        descriptor.ShouldNotBeNull();
        descriptor.RuntimeId.ShouldBe("test-runtime");
        descriptor.ApplicationName.ShouldBe("TestApp");
        descriptor.EnvironmentName.ShouldBe(Environments.Development);
        descriptor.ProcessId.ShouldBe(Environment.ProcessId);
        descriptor.Features.ShouldContainKey("consoleCommands");
        descriptor.Features["consoleCommands"].Endpoint.ShouldBe("bdk-test-runtime-console");
        File.Exists(descriptorPath).ShouldBeFalse();
    }

    [Fact]
    public async Task McpDispatcher_WhenCapabilitiesRequested_ReturnsRegisteredHandlerCapabilities()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddTransient<IMcpHandler, TestMcpHandler>()
            .BuildServiceProvider();
        var sut = new McpDispatcher(services, services.GetRequiredService<ILogger<McpDispatcher>>());

        // Act
        var response = await sut.DispatchAsync(new McpRequest("mcp.capabilities", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        response.Summary.ShouldContain("1 MCP operation");
        JsonSerializer.Serialize(response.Data).ShouldContain("test.inspect");
    }

    [Fact]
    public async Task McpDispatcher_WhenOperationIsUnknown_ReturnsFeatureUnavailable()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddTransient<IMcpHandler, TestMcpHandler>()
            .BuildServiceProvider();
        var sut = new McpDispatcher(services, services.GetRequiredService<ILogger<McpDispatcher>>());

        // Act
        var response = await sut.DispatchAsync(new McpRequest("missing.operation", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.FeatureUnavailable);
    }

    [Fact]
    public async Task RuntimeDiagnosticsMcpHandler_WhenHealthChecksAreMissing_ReturnsFeatureUnavailable()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new RuntimeDiagnosticsMcpHandler(services);

        // Act
        var response = await sut.HandleAsync(new McpRequest("health.snapshot", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.FeatureUnavailable);
        response.Summary.ShouldContain("Health checks are not available");
    }

    [Fact]
    public async Task McpIpcServer_WhenNonceIsInvalid_ReturnsRejectedResponse()
    {
        // Arrange
        var descriptorOptions = new HostDescriptorOptions
        {
            RegistryPath = CreateTempDirectory(),
            RuntimeId = "mcp-test-" + Guid.NewGuid().ToString("N"),
            WorkspacePath = CreateTempDirectory(),
            ProjectPath = Path.Combine(CreateTempDirectory(), "Test.csproj"),
            StartedAt = DateTimeOffset.UtcNow
        };
        var endpoints = new LocalIpcEndpointState(descriptorOptions);
        var endpoint = endpoints.GetOrCreate("mcp");
        var services = new ServiceCollection()
            .AddLogging()
            .AddTransient<IMcpHandler, TestMcpHandler>()
            .BuildServiceProvider();
        var dispatcher = new McpDispatcher(services, services.GetRequiredService<ILogger<McpDispatcher>>());
        using var sut = new McpIpcServer(endpoints, dispatcher, NullLogger<McpIpcServer>.Instance);
        await sut.StartAsync(CancellationToken.None);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var request = new McpIpcRequest("invalid", 1, "mcp.capabilities", McpToolset.Diagnostics, EmptyJson());

            // Act
            var response = await SendMcpIpcRequestAsync(endpoint, request, timeout.Token);

            // Assert
            response.ShouldNotBeNull();
            response.Ok.ShouldBeFalse();
            response.Response.Code.ShouldBe(McpErrorCode.SelectedRuntimeUnavailable);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    private static JsonElement EmptyJson()
        => JsonDocument.Parse("{}").RootElement.Clone();

    private static async Task<McpIpcResponse> SendMcpIpcRequestAsync(
        HostFeatureEndpointMetadata endpoint,
        McpIpcRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(endpoint.Transport, "unix-socket", StringComparison.OrdinalIgnoreCase))
        {
            using var socket = await ConnectUnixSocketAsync(endpoint.Endpoint, cancellationToken);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            return await SendMcpIpcRequestAsync(stream, request, cancellationToken);
        }

        await using var pipe = new NamedPipeClientStream(".", endpoint.Endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(cancellationToken);

        return await SendMcpIpcRequestAsync(pipe, request, cancellationToken);
    }

    private static async Task<Socket> ConnectUnixSocketAsync(string endpoint, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (!File.Exists(endpoint))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
                continue;
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), cancellationToken);

                return socket;
            }
            catch (SocketException) when (!cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }
        }
    }

    private static async Task<McpIpcResponse> SendMcpIpcRequestAsync(
        Stream stream,
        McpIpcRequest request,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var responseJson = await reader.ReadLineAsync(cancellationToken);

        return JsonSerializer.Deserialize<McpIpcResponse>(responseJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bdk-web-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestEndpointContributor(HostFeatureEndpointMetadata endpoint) : IHostFeatureEndpointContributor
    {
        public string FeatureName => "consoleCommands";

        public HostFeatureEndpointMetadata GetEndpointMetadata(IServiceProvider services)
            => endpoint;
    }

    private sealed class TestMcpHandler : IMcpHandler
    {
        public IReadOnlyCollection<McpCapability> Capabilities { get; } =
        [
            new("test.inspect", McpToolset.Diagnostics, "project", "Inspects test state.") { Owner = "tests" }
        ];

        public ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(McpResponse.Success("Test handled."));
    }

}

[UnitTest("Presentation")]
public class DevKitLocalToolingPolicyTests
{
    [Fact]
    public void Evaluate_WhenDisabledByOptions_ReturnsDisabled()
    {
        // Arrange
        var options = new DevKitCliHostOptions { Enabled = false };

        // Act
        var sut = DevKitLocalToolingPolicy.Evaluate(
            CreateEnvironment(Environments.Development),
            CreateConfiguration(),
            options);

        // Assert
        sut.Enabled.ShouldBeFalse();
        sut.Reason.ShouldBe("Disabled by options.");
    }

    [Fact]
    public void Evaluate_WhenOutsideDevelopment_ReturnsDisabled()
    {
        // Arrange
        var options = new DevKitCliHostOptions();

        // Act
        var sut = DevKitLocalToolingPolicy.Evaluate(
            CreateEnvironment(Environments.Production),
            CreateConfiguration(),
            options);

        // Assert
        sut.Enabled.ShouldBeFalse();
        sut.Reason.ShouldBe("Host environment is not Development.");
    }

    [Fact]
    public void Evaluate_WhenDisabledByConfiguration_ReturnsDisabled()
    {
        // Arrange
        var options = new DevKitCliHostOptions();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["DevKit:Cli:Enabled"] = "false"
        });

        // Act
        var sut = DevKitLocalToolingPolicy.Evaluate(
            CreateEnvironment(Environments.Development),
            configuration,
            options);

        // Assert
        sut.Enabled.ShouldBeFalse();
        sut.Reason.ShouldBe("Disabled by configuration.");
    }

    [Fact]
    public void Evaluate_WhenDevelopment_ReturnsEnabledCapabilities()
    {
        // Arrange
        var options = new DevKitCliHostOptions();

        // Act
        var sut = DevKitLocalToolingPolicy.Evaluate(
            CreateEnvironment(Environments.Development),
            CreateConfiguration(),
            options);

        // Assert
        sut.Enabled.ShouldBeTrue();
        sut.ConsoleCommandsEnabled.ShouldBeTrue();
        sut.McpEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_WhenCapabilityDisabledByConfiguration_ReturnsEnabledWithoutCapability()
    {
        // Arrange
        var options = new DevKitCliHostOptions();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["DevKit:Cli:ConsoleCommands"] = "false",
            ["DevKit:Cli:Mcp"] = "false"
        });

        // Act
        var sut = DevKitLocalToolingPolicy.Evaluate(
            CreateEnvironment(Environments.Development),
            configuration,
            options);

        // Assert
        sut.Enabled.ShouldBeTrue();
        sut.ConsoleCommandsEnabled.ShouldBeFalse();
        sut.McpEnabled.ShouldBeFalse();
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string> values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return environment;
    }
}
