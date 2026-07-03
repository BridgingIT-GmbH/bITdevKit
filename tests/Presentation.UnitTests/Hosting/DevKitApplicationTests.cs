namespace BridgingIT.DevKit.Presentation.UnitTests.Hosting;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

[UnitTest("Presentation")]
public class DevKitApplicationTests
{
    [Fact]
    public void CreateBuilder_ReturnsDevKitApplicationBuilder()
    {
        // Arrange & Act
        var sut = DevKitApplication.CreateBuilder([]);

        // Assert
        sut.Services.ShouldNotBeNull();
        sut.Configuration.ShouldNotBeNull();
        sut.Environment.ShouldNotBeNull();
        sut.Logging.ShouldNotBeNull();
        sut.HostApplicationBuilder.ShouldNotBeNull();
        sut.Properties.ContainsKey(DevKitBuilderProperties.HostApplicationBuilder).ShouldBeTrue();
        sut.Properties.ContainsKey(DevKitBuilderProperties.LoggingBuilder).ShouldBeTrue();
        sut.Properties.ContainsKey(DevKitBuilderProperties.HostBuilder).ShouldBeFalse();
        sut.Properties[DevKitBuilderProperties.HostApplicationBuilder].ShouldBeAssignableTo<HostApplicationBuilder>();
        sut.Properties[DevKitBuilderProperties.LoggingBuilder].ShouldBeAssignableTo<ILoggingBuilder>();
        sut.ShouldBeAssignableTo<IDevKitApplicationBuilder>();
    }

    [Fact]
    public void CreateBuilder_WhenConfigured_AppliesCliOptions()
    {
        // Arrange & Act
        var sut = DevKitApplication.CreateBuilder([], options => options
            .Cli(cli => cli
                .Enabled()
                .ConsoleCommands(false)
                .Mcp(mcp => mcp
                    .Enabled(false)
                    .DisableFeature("jobs")
                    .WorkspacePathFromContentRoot())));

        // Assert
        sut.Options.Cli.Enabled.ShouldBeTrue();
        sut.Options.Cli.ConsoleCommandsEnabled.ShouldBeFalse();
        sut.Options.Cli.McpEnabled.ShouldBeFalse();
        sut.Options.Cli.DisabledMcpFeatures.ShouldContain("jobs");
        sut.Options.Cli.UseContentRootAsWorkspacePath.ShouldBeTrue();
    }

    [Fact]
    public void CreateBuilder_ByDefault_DisablesCliIntegration()
    {
        // Arrange & Act
        var sut = DevKitApplication.CreateBuilder([]);

        // Assert
        sut.Options.Cli.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void CreateBuilder_WhenBuilderConfigured_AppliesConfiguration()
    {
        // Arrange & Act
        var sut = DevKitApplication.CreateBuilder([], builder => builder
            .AddConsoleCommands(commands => commands.AddCommand<TestConsoleCommand>()));

        // Assert
        using var host = sut.Build();
        var commands = host.Services.GetServices<IConsoleCommand>().ToArray();
        commands.ShouldContain(command => command is TestConsoleCommand);
    }

    [Fact]
    public void Configure_InvokesCallbackAndReturnsSameBuilder()
    {
        // Arrange
        var sut = DevKitApplication.CreateBuilder([]);

        // Act
        var result = sut.Configure(builder => builder.Properties["test"] = "configured");

        // Assert
        result.ShouldBeSameAs(sut);
        sut.Properties["test"].ShouldBe("configured");
    }

    [Fact]
    public void Build_ReturnsHost()
    {
        // Arrange
        var sut = DevKitApplication.CreateBuilder([]);

        // Act
        using var host = sut.Build();

        // Assert
        host.ShouldBeAssignableTo<IHost>();
    }

    [Fact]
    public void AddConsoleCommands_RegistersConfiguredCommand()
    {
        // Arrange
        var sut = DevKitApplication.CreateBuilder([])
            .AddConsoleCommands(commands => commands.AddCommand<TestConsoleCommand>());

        // Act
        using var host = sut.Build();
        var commands = host.Services.GetServices<IConsoleCommand>().ToArray();

        // Assert
        commands.ShouldContain(command => command is TestConsoleCommand);
    }

    private sealed class TestConsoleCommand() : ConsoleCommandBase("test", "Test command")
    {
        public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}