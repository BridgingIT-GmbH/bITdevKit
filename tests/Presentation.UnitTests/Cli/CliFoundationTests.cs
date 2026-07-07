namespace BridgingIT.DevKit.Presentation.UnitTests.Cli;

using System.Text.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Cli;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public sealed class CliFoundationTests
{
    [Fact]
    public void McpToolCatalog_WhenCreated_UsesClientSafeToolNames()
    {
        // Arrange
        var sut = new McpToolCatalog();

        // Act
        var names = sut.Tools.Select(tool => tool.Name).ToArray();

        // Assert
        names.Length.ShouldBe(names.Distinct(StringComparer.Ordinal).Count());
        names.ShouldAllBe(name => Regex.IsMatch(name, "^[a-z0-9_-]+$"));
    }

    [Fact]
    public void CliArgumentParser_WhenQuietAndVerbose_ReturnsInvalidArguments()
    {
        // Arrange & Act
        var result = CliArgumentParser.Parse(["hosts", "list", "--quiet", "--verbose"]);

        // Assert
        result.Error.ShouldBe("--quiet and --verbose cannot be used together.");
    }

    [Fact]
    public void CliArgumentParser_WhenNoLogoAndBanner_ReturnsInvalidArguments()
    {
        // Arrange & Act
        var result = CliArgumentParser.Parse(["version", "--nologo", "--banner"]);

        // Assert
        result.Error.ShouldBe("--nologo and --banner cannot be used together.");
    }

    [Fact]
    public void CliArgumentParser_WhenBannerOptionsAreSupplied_ParsesGlobalOptions()
    {
        // Arrange & Act
        var result = CliArgumentParser.Parse(["docs", "--banner", "--non-interactive"]);

        // Assert
        result.Error.ShouldBeNull();
        result.Banner.ShouldBeTrue();
        result.NonInteractive.ShouldBeTrue();
        result.CommandArguments.ShouldBe(["docs"]);
    }

    [Fact]
    public void WorkspaceResolver_WhenExplicitPath_UsesStableHash()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var sut = new WorkspaceResolver();

        // Act
        var first = sut.Resolve(workspacePath);
        var second = sut.Resolve(workspacePath);

        // Assert
        first.Path.ShouldBe(second.Path);
        first.Hash.ShouldBe(second.Hash);
        first.Hash.Length.ShouldBe(16);
    }

    [Fact]
    public void HostRuntimeDiscovery_FiltersByWorkspaceAndFeature()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var otherWorkspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-1",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features = { ["consoleCommands"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "bdk-test", Nonce = "nonce" } }
        });
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "dofiesta-1",
            ApplicationName = "DoFiesta",
            EnvironmentName = "Development",
            WorkspacePath = otherWorkspacePath,
            ContentRootPath = otherWorkspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "DoFiesta", Version = "1.0.0" }
        });
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var sut = new HostRuntimeDiscovery(options);

        // Act
        var result = sut.Discover(workspace, featureName: "consoleCommands");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Descriptor.RuntimeId.ShouldBe("weatherfiesta-1");
        result[0].Status.ShouldBe(HostRuntimeStatus.Ready);
    }

    [Fact]
    public void HostRuntimeNaming_WhenApplicationNameIsLayeredAssemblyName_ReturnsFriendlyNameAndRuntimeId()
    {
        // Arrange
        const string applicationName = "BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server";

        // Act
        var displayName = HostRuntimeNaming.GetDisplayApplicationName(applicationName);
        var runtimeId = HostRuntimeNaming.CreateRuntimeId(applicationName, 119740);

        // Assert
        displayName.ShouldBe("WeatherFiesta");
        runtimeId.ShouldBe("weatherfiesta-119740");
    }

    [Fact]
    public void HostRuntimeTable_WhenDescriptorUsesVerboseRuntimeId_ReturnsFriendlyDisplayValues()
    {
        // Arrange
        var host = new HostRuntimeInfo
        {
            DescriptorPath = "descriptor.json",
            Status = HostRuntimeStatus.Ready,
            Descriptor = new HostRuntimeDescriptor
            {
                RuntimeId = "bridgingit-devkit-examples-weatherfiesta-presentation-web-server-119740",
                ApplicationName = "BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server",
                ProcessId = 119740
            }
        };

        // Act
        var displayRuntimeId = HostRuntimeTable.GetDisplayRuntimeId(host);
        var displayApplicationName = HostRuntimeTable.GetDisplayApplicationName(host);

        // Assert
        displayRuntimeId.ShouldBe("weatherfiesta-119740");
        displayApplicationName.ShouldBe("WeatherFiesta");
    }

    [Fact]
    public void HostSelectionStore_WritesAndReadsWorkspaceSelection()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var options = new HostRegistryOptions { RuntimePath = CreateTempDirectory(), SelectionPath = CreateTempDirectory() };
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var sut = new HostSelectionStore(options);

        // Act
        var selectionPath = sut.Write(workspace, "weatherfiesta-1");
        var selectedRuntimeId = sut.Read(workspace);

        // Assert
        File.Exists(selectionPath).ShouldBeTrue();
        selectedRuntimeId.ShouldBe("weatherfiesta-1");
    }

    [Fact]
    public void McpRuntimeTools_GetCurrentRuntime_WhenStoredSelectionIsStaleAndOneReadyRuntimeExists_AutoSelectsReadyRuntime()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, CreateMcpDescriptor("weatherfiesta-stale", workspacePath, int.MaxValue));
        WriteDescriptor(registryPath, CreateMcpDescriptor("weatherfiesta-ready", workspacePath, Environment.ProcessId));
        var selectionStore = new HostSelectionStore(options);
        selectionStore.Write(workspace, "weatherfiesta-stale");
        var sut = new McpRuntimeTools(
            new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options),
            new HostRuntimeDiscovery(options),
            selectionStore,
            new McpIpcClient());

        // Act
        var response = sut.GetCurrentRuntime();

        // Assert
        response.Available.ShouldBeTrue();
        var readyRuntimeId = $"weatherfiesta-{Environment.ProcessId}";
        selectionStore.Read(workspace).ShouldBe(readyRuntimeId);
        var json = JsonSerializer.Serialize(response, CliJson.Options);
        json.ShouldContain(readyRuntimeId);
        json.ShouldNotContain("weatherfiesta-stale");
    }

    [Fact]
    public void McpRuntimeTools_GetCurrentRuntime_WhenStoredSelectionIsStaleAndMultipleReadyRuntimesExist_ClearsSelectionAndRequiresChoice()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, CreateMcpDescriptor("weatherfiesta-stale", workspacePath, int.MaxValue));
        WriteDescriptor(registryPath, CreateMcpDescriptor("weatherfiesta-ready-1", workspacePath, Environment.ProcessId));
        WriteDescriptor(registryPath, CreateMcpDescriptor("weatherfiesta-ready-2", workspacePath, Environment.ProcessId));
        var selectionStore = new HostSelectionStore(options);
        selectionStore.Write(workspace, "weatherfiesta-stale");
        var sut = new McpRuntimeTools(
            new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options),
            new HostRuntimeDiscovery(options),
            selectionStore,
            new McpIpcClient());

        // Act
        var response = sut.GetCurrentRuntime();

        // Assert
        response.Available.ShouldBeTrue();
        selectionStore.Read(workspace).ShouldBeNull();
        var json = JsonSerializer.Serialize(response, CliJson.Options);
        json.ShouldContain("runtime_selection_required");
        json.ShouldContain("bdk_runtimes_list");
        json.ShouldNotContain("weatherfiesta-stale");
    }

    [Fact]
    public async Task CliApplication_VersionJson_WritesStructuredOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["version", "--output", "json"]);

            // Assert
            exitCode.ShouldBe(0);
            writer.ToString().ShouldContain("\"modules\"");
            writer.ToString().ShouldContain("\"exitCode\": 0");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_HelpJson_WritesStructuredCommandList()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["help", "--output", "json"]);

            // Assert
            exitCode.ShouldBe(0);
            writer.ToString().ShouldContain("\"commands\"");
            writer.ToString().ShouldContain("\"name\": \"version\"");
            writer.ToString().ShouldContain("\"name\": \"hosts\"");
            writer.ToString().ShouldContain("\"exitCode\": 0");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_HostsListJson_UsesConsoleCommandBinding()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["hosts", "list", "--feature", "consoleCommands", "--output", "json"]);

            // Assert
            exitCode.ShouldBe(0);
            writer.ToString().ShouldContain("\"hosts\"");
            writer.ToString().ShouldContain("\"exitCode\": 0");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsListCliCommand_WhenAllIsNotSpecified_HidesStaleDescriptors()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-ready",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features = { ["consoleCommands"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "ready", Nonce = "nonce" } }
        });
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-stale",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = int.MaxValue,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features = { ["consoleCommands"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "stale", Nonce = "nonce" } }
        });
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .BuildServiceProvider();
        var sut = new HostsListCliCommand();

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            writer.ToString().ShouldContain("weatherfiesta-ready");
            writer.ToString().ShouldNotContain("weatherfiesta-stale");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsListCliCommand_WhenAllIsSpecified_IncludesStaleDescriptors()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-stale",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = int.MaxValue,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features = { ["consoleCommands"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "stale", Nonce = "nonce" } }
        });
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .BuildServiceProvider();
        var sut = new HostsListCliCommand { IncludeAll = true };

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            writer.ToString().ShouldContain("weatherfiesta-stale");
            writer.ToString().ShouldContain("\"status\": \"Stale\"");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsVersionsCliCommand_WhenAllIsNotSpecified_HidesStaleDescriptors()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-ready",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" }
        });
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-stale",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = int.MaxValue,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" }
        });
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .BuildServiceProvider();
        var sut = new HostsVersionsCliCommand();

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            writer.ToString().ShouldContain("weatherfiesta-ready");
            writer.ToString().ShouldNotContain("weatherfiesta-stale");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsKillCliCommand_WhenTargetIsMissing_ReturnsInvalidArguments()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = CreateTempDirectory(), SelectionPath = CreateTempDirectory() };
        var state = new CliExecutionState();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(state)
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .AddSingleton<IHostProcessManager>(new FakeHostProcessManager())
            .BuildServiceProvider();
        var sut = new HostsKillCliCommand();

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            state.ExitCode.ShouldBe(CliExitCode.InvalidArguments);
            writer.ToString().ShouldContain("Specify a runtime id or --all.");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsKillCliCommand_WhenYesIsNotSpecified_DoesNotKillReadyHost()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-ready",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" }
        });
        var processManager = new FakeHostProcessManager();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(new CliExecutionState())
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .AddSingleton<IHostProcessManager>(processManager)
            .BuildServiceProvider();
        var sut = new HostsKillCliCommand { RuntimeId = "weatherfiesta-ready" };

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            processManager.KilledProcessIds.ShouldBeEmpty();
            writer.ToString().ShouldContain("ConfirmationRequired");
            writer.ToString().ShouldContain("weatherfiesta-ready");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HostsKillCliCommand_WhenAllAndYesAreSpecified_KillsOnlyReadyHosts()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-ready",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" }
        });
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-stale",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = int.MaxValue,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" }
        });
        var processManager = new FakeHostProcessManager();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(new CliExecutionState())
            .AddSingleton(options)
            .AddSingleton<HostRuntimeDiscovery>()
            .AddSingleton<IHostProcessManager>(processManager)
            .BuildServiceProvider();
        var sut = new HostsKillCliCommand { All = true, Yes = true };

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            processManager.KilledProcessIds.ShouldBe([Environment.ProcessId]);
            writer.ToString().ShouldContain("weatherfiesta-ready");
            writer.ToString().ShouldNotContain("weatherfiesta-stale");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_DocsUrl_WritesOfficialDocumentationUrl()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["docs", "--url"]);

            // Assert
            exitCode.ShouldBe(0);
            writer.ToString().ShouldContain(DocsConsoleCommand.OfficialDocsUrl);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_DocsJson_DoesNotOpenBrowser()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["docs", "--output", "json"]);

            // Assert
            exitCode.ShouldBe(0);
            writer.ToString().ShouldContain("\"url\":");
            writer.ToString().ShouldContain("\"opened\": false");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenCommandIsUnknown_ReturnsInvalidArguments()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["missing", "--output", "json"]);

            // Assert
            exitCode.ShouldBe((int)CliExitCode.InvalidArguments);
            writer.ToString().ShouldContain("\"code\": \"invalid_arguments\"");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenVersionIsUsedWithCommand_ReturnsInvalidArguments()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["hosts", "--version", "--output", "json"]);

            // Assert
            exitCode.ShouldBe((int)CliExitCode.InvalidArguments);
            writer.ToString().ShouldContain("--version can only be used at the root command");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_HostRunRuntimeIdSelector_MapsToHostSelector()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["host", "run", "--runtime-id", "missing-host", "--output", "json", "--", "status"]);

            // Assert
            exitCode.ShouldBe((int)CliExitCode.SelectedHostUnavailable);
            writer.ToString().ShouldContain("\"code\": \"selected_host_unavailable\"");
            writer.ToString().ShouldContain("missing-host");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ConsoleCommandExecutor_WhenCommandIsUnknown_ReturnsFailure()
    {
        // Arrange
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var writer = new StringWriter();
        var console = Spectre.Console.AnsiConsole.Create(new Spectre.Console.AnsiConsoleSettings
        {
            Out = new TextWriterAnsiConsoleOutput(writer, 120, 32)
        });
        var sut = new ConsoleCommandExecutor();

        // Act
        var result = await sut.ExecuteAsync(
            ["missing"],
            console,
            provider,
            ConsoleCommandExecutionSource.Terminal);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldContain("Unknown command");
    }

    [Fact]
    public void HostRuntimeDiscovery_WhenFeatureMetadataIsIncomplete_ReturnsFeatureUnavailable()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "weatherfiesta-ready",
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features = { ["consoleCommands"] = new HostFeatureEndpointMetadata { ProtocolVersion = 1, Transport = "named-pipe", Endpoint = "ready" } }
        });
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var sut = new HostRuntimeDiscovery(options);

        // Act
        var hosts = sut.Discover(workspace, includeAll: true, featureName: "consoleCommands");

        // Assert
        hosts.Count.ShouldBe(1);
        hosts[0].Status.ShouldBe(HostRuntimeStatus.FeatureUnavailable);
        hosts[0].Reason.ShouldContain("metadata is incomplete");
    }

    [Fact]
    public async Task HostsSelectCliCommand_WhenHostBelongsToOtherWorkspace_DoesNotStoreSelection()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var workspacePath = CreateTempDirectory();
        var otherWorkspacePath = CreateTempDirectory();
        var registryPath = CreateTempDirectory();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var options = new HostRegistryOptions { RuntimePath = registryPath, SelectionPath = CreateTempDirectory() };
        WriteDescriptor(registryPath, new HostRuntimeDescriptor
        {
            RuntimeId = "other-workspace",
            ApplicationName = "OtherApp",
            EnvironmentName = "Development",
            WorkspacePath = otherWorkspacePath,
            ContentRootPath = otherWorkspacePath,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "OtherApp", Version = "1.0.0" }
        });
        var selectionStore = new HostSelectionStore(options);
        var state = new CliExecutionState();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(workspace, new CliOutputSettings { Format = CliOutputFormat.Json }, options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(state)
            .AddSingleton(options)
            .AddSingleton(selectionStore)
            .AddSingleton<HostRuntimeDiscovery>()
            .BuildServiceProvider();
        var sut = new HostsSelectCliCommand { RuntimeId = "other-workspace" };

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            state.ExitCode.ShouldBe(CliExitCode.HostNotFound);
            selectionStore.Read(workspace).ShouldBeNull();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenMcpToolsListIsCalled_ReturnsStableToolCatalog()
    {
        // Arrange
        var originalIn = Console.In;
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        using var reader = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}\n");
        Console.SetIn(reader);
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["mcp"]);

            // Assert
            exitCode.ShouldBe(0);
            var output = writer.ToString();
            output.ShouldContain("\"jsonrpc\":\"2.0\"");
            output.ShouldContain("\"bdk_mcp_status\"");
            output.ShouldContain("\"bdk_mcp_explain_setup\"");
            output.ShouldContain("\"bdk_messages_summary\"");
            output.ShouldContain("\"bdk_queueing_pause_queue\"");
            output.ShouldContain("\"bdk_jobs_purge_runs\"");
            output.ShouldContain("\"bdk_orchestrations_purge\"");
            output.ShouldContain("\"bdk_api_search\"");
            output.ShouldContain("\"bdk_api_get\"");
            output.ShouldContain("\"bdk_project_call\"");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenMcpStatusIsCalledWithoutRuntime_ReturnsAvailableStatus()
    {
        // Arrange
        var originalIn = Console.In;
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        using var reader = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":\"status\",\"method\":\"tools/call\",\"params\":{\"name\":\"bdk_mcp_status\",\"arguments\":{}}}\n");
        Console.SetIn(reader);
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["mcp"]);

            // Assert
            exitCode.ShouldBe(0);
            var output = writer.ToString();
            output.ShouldContain("\"id\":\"status\"");
            output.ShouldContain("\"available\":true");
            output.ShouldContain("\"workspacePath\"");
            output.ShouldContain("\"result\"");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenMcpOperationToolsetIsDisabled_ReturnsUnauthorizedToolset()
    {
        // Arrange
        var originalIn = Console.In;
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        using var reader = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"bdk_jobs_trigger\",\"arguments\":{}}}\n");
        Console.SetIn(reader);
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["mcp"]);

            // Assert
            exitCode.ShouldBe(0);
            var output = writer.ToString();
            output.ShouldContain("\"isError\":true");
            output.ShouldContain("unauthorized_toolset");
            output.ShouldContain("operations");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenProjectCallToolsetIsUnknown_ReturnsOperationFailed()
    {
        // Arrange
        var originalIn = Console.In;
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        using var reader = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"tools/call\",\"params\":{\"name\":\"bdk_project_call\",\"arguments\":{\"operation\":\"weatherfiesta_inspect_city\",\"toolset\":\"danger\",\"arguments\":{}}}}\n");
        Console.SetIn(reader);
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["mcp"]);

            // Assert
            exitCode.ShouldBe(0);
            var output = writer.ToString();
            output.ShouldContain("\"isError\":true");
            output.ShouldContain("unknown toolset");
            output.ShouldContain("diagnostics, operations, or admin");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task CliApplication_WhenProjectCallOperationNameIsNotClientSafe_ReturnsOperationFailed()
    {
        // Arrange
        var originalIn = Console.In;
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        using var reader = new StringReader("{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tools/call\",\"params\":{\"name\":\"bdk_project_call\",\"arguments\":{\"operation\":\"weatherfiestaInspectCity\",\"toolset\":\"diagnostics\",\"arguments\":{}}}}\n");
        Console.SetIn(reader);
        Console.SetOut(writer);

        try
        {
            // Act
            var exitCode = await CliApplication.RunAsync(["mcp"]);

            // Assert
            exitCode.ShouldBe(0);
            var output = writer.ToString();
            output.ShouldContain("\"isError\":true");
            output.ShouldContain("not a valid client-safe operation name");
            output.ShouldContain("lowercase letters, digits, underscores, or hyphens");
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task McpDocumentationTools_WhenSearchIsCalled_ReturnsBoundedOfficialSources()
    {
        // Arrange
        var source = new FakeMcpDocumentationSource([
            new McpDocumentationDocument(
                "docs/features-queueing.md",
                "https://github.com/bridgingit/bitdevkit/blob/main/docs/features-queueing.md",
                "# Queueing\nQueueing retries can be inspected with retained queue messages."),
            new McpDocumentationDocument(
                "docs/features-jobs.md",
                "https://github.com/bridgingit/bitdevkit/blob/main/docs/features-jobs.md",
                "# Jobs\nJob scheduling documentation.")
        ]);
        var sut = new McpDocumentationTools(source);
        var arguments = JsonDocument.Parse("{\"query\":\"queueing retries\",\"limit\":2}").RootElement;

        // Act
        var response = await sut.SearchAsync(arguments, CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        JsonSerializer.Serialize(response.Data).ShouldContain("github.com/bridgingit/bitdevkit");
        JsonSerializer.Serialize(response.Data).ShouldContain("features-queueing.md");
    }

    [Fact]
    public async Task McpApiReferenceTools_WhenSearchIsCalled_RanksExactSymbolFirst()
    {
        // Arrange
        var source = new FakeMcpApiReferenceSource(CreateApiIndex([
            CreateApiIndexSymbol("BridgingIT.DevKit.Common.Result", "Result", "Struct", "results"),
            CreateApiIndexSymbol("BridgingIT.DevKit.Common.ResultMessage", "ResultMessage", "Class", "results")
        ]));
        var sut = new McpApiReferenceTools(source);
        var arguments = JsonDocument.Parse("{\"query\":\"Result\",\"limit\":2}").RootElement;

        // Act
        var response = await sut.SearchAsync(arguments, CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.IndexOf("\"Uid\":\"BridgingIT.DevKit.Common.Result\"", StringComparison.Ordinal)
            .ShouldBeLessThan(json.IndexOf("\"Uid\":\"BridgingIT.DevKit.Common.ResultMessage\"", StringComparison.Ordinal));
        response.Next.Select(next => next.Tool).ShouldContain("bdk_api_get");
    }

    [Fact]
    public async Task McpApiReferenceTools_WhenFiltersAreSupplied_ReturnsMatchingSymbols()
    {
        // Arrange
        var source = new FakeMcpApiReferenceSource(CreateApiIndex([
            CreateApiIndexSymbol("BridgingIT.DevKit.Application.Queues.IQueueHandler", "IQueueHandler", "Interface", "queueing", "BridgingIT.DevKit.Application.Queues"),
            CreateApiIndexSymbol("BridgingIT.DevKit.Application.Messaging.IMessageHandler", "IMessageHandler", "Interface", "messaging", "BridgingIT.DevKit.Application.Messaging")
        ]));
        var sut = new McpApiReferenceTools(source);
        var arguments = JsonDocument.Parse("{\"query\":\"handler\",\"topic\":\"queueing\",\"kind\":\"Interface\",\"namespace\":\"Application.Queues\"}").RootElement;

        // Act
        var response = await sut.SearchAsync(arguments, CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("IQueueHandler");
        json.ShouldNotContain("IMessageHandler");
    }

    [Fact]
    public async Task McpApiReferenceTools_WhenGetIsCalled_ReturnsBoundedSymbolContent()
    {
        // Arrange
        var symbol = new McpApiReferenceSymbol
        {
            Uid = "BridgingIT.DevKit.Common.Result",
            Name = "Result",
            FullName = "BridgingIT.DevKit.Common.Result",
            Kind = "Struct",
            Namespace = "BridgingIT.DevKit.Common",
            Assembly = "BridgingIT.DevKit.Common.Results",
            Summary = "Represents an operation result.",
            Url = "https://bridgingit-gmbh.github.io/bITdevKit/api/obj/api/BridgingIT.DevKit.Common.Result.html",
            Topics = ["results", "common"]
        };
        var source = new FakeMcpApiReferenceSource(
            CreateApiIndex([CreateApiIndexSymbol(symbol.Uid, symbol.Name, symbol.Kind, "results")]),
            new Dictionary<string, McpApiReferenceSymbol>(StringComparer.OrdinalIgnoreCase) { [symbol.Uid] = symbol });
        var sut = new McpApiReferenceTools(source);
        var arguments = JsonDocument.Parse("{\"uid\":\"BridgingIT.DevKit.Common.Result\",\"maxChars\":80}").RootElement;

        // Act
        var response = await sut.GetAsync(arguments, CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        response.Truncated.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("BridgingIT.DevKit.Common.Result");
        json.ShouldContain("truncated");
    }

    [Fact]
    public async Task McpApiReferenceTools_WhenSymbolIsMissing_ReturnsDocumentationUnavailable()
    {
        // Arrange
        var source = new FakeMcpApiReferenceSource(CreateApiIndex([]));
        var sut = new McpApiReferenceTools(source);
        var arguments = JsonDocument.Parse("{\"uid\":\"missing\"}").RootElement;

        // Act
        var response = await sut.GetAsync(arguments, CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.DocumentationUnavailable);
        response.Next.Select(next => next.Tool).ShouldContain("bdk_api_search");
    }

    [Fact]
    public async Task GitHubPagesMcpApiReferenceSource_WhenLocalPageDetailExists_ResolvesRequestedSymbol()
    {
        // Arrange
        var workspacePath = CreateTempDirectory();
        var apiRoot = Path.Combine(workspacePath, ".github", "pages", "api");
        var symbolRoot = Path.Combine(apiRoot, "agent-symbols");
        Directory.CreateDirectory(symbolRoot);

        const string uid = "BridgingIT.DevKit.Common.Result.Bind(System.Action)";
        File.WriteAllText(
            Path.Combine(apiRoot, "agent-index.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                siteUrl = "https://bridgingit-gmbh.github.io/bITdevKit/api/",
                source = "docfx-mref",
                symbols = new[]
                {
                    new
                    {
                        uid,
                        name = "Bind(Action)",
                        fullName = uid,
                        kind = "Method",
                        @namespace = "BridgingIT.DevKit.Common",
                        assembly = "BridgingIT.DevKit.Common.Results",
                        summary = "Creates a Result from an operation.",
                        href = "obj/api/BridgingIT.DevKit.Common.Result.html#Bind",
                        detail = "agent-symbols/result-page.json",
                        topics = new[] { "results", "common" }
                    }
                }
            }));
        File.WriteAllText(
            Path.Combine(symbolRoot, "result-page.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                source = "docfx-mref-page",
                file = "BridgingIT.DevKit.Common.Result.yml",
                symbols = new[]
                {
                    new
                    {
                        uid,
                        name = "Bind(Action)",
                        fullName = uid,
                        kind = "Method",
                        @namespace = "BridgingIT.DevKit.Common",
                        assembly = "BridgingIT.DevKit.Common.Results",
                        summary = "Creates a Result from an operation.",
                        syntax = new { content = "public static Result Bind(Action operation)" },
                        parameters = new[] { new { id = "operation", type = "System.Action", description = "The operation." } },
                        returns = new { type = "BridgingIT.DevKit.Common.Result", description = "The result." },
                        url = "https://bridgingit-gmbh.github.io/bITdevKit/api/obj/api/BridgingIT.DevKit.Common.Result.html#Bind",
                        topics = new[] { "results", "common" }
                    }
                }
            }));

        using var httpClient = new HttpClient();
        var workspace = new WorkspaceResolver().Resolve(workspacePath);
        var context = new CliRuntimeContext(
            workspace,
            new CliOutputSettings { Format = CliOutputFormat.Json },
            new HostRegistryOptions { RuntimePath = CreateTempDirectory(), SelectionPath = CreateTempDirectory() });
        var sut = new GitHubPagesMcpApiReferenceSource(httpClient, context);

        // Act
        var symbol = await sut.GetSymbolAsync(uid, CancellationToken.None);

        // Assert
        symbol.ShouldNotBeNull();
        symbol.Uid.ShouldBe(uid);
        symbol.Parameters.Single().Id.ShouldBe("operation");
        symbol.Returns.Type.ShouldBe("BridgingIT.DevKit.Common.Result");
    }

    [Fact]
    public void McpGuidanceTools_WhenTopicIsKnown_ReturnsGuidanceWithDocsAndNextCalls()
    {
        // Arrange
        var sut = new McpGuidanceTools();
        var arguments = JsonDocument.Parse("{\"topic\":\"jobs\"}").RootElement;

        // Act
        var response = sut.Get(arguments);

        // Assert
        response.Available.ShouldBeTrue();
        response.Next.Select(next => next.Tool).ShouldContain("bdk_docs_search");
        response.Next.Select(next => next.Tool).ShouldContain("bdk_api_search");
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("features-jobs.md");
        json.ShouldContain("bdk_jobs_list");
        json.ShouldContain("bdk_api_search");
    }

    [Fact]
    public void McpGuidanceTools_WhenQueryMentionsNewJob_ReturnsJobsGuidance()
    {
        // Arrange
        var sut = new McpGuidanceTools();
        var arguments = JsonDocument.Parse("{\"query\":\"Use the bdk MCP and give me guidance on how to implement a new job\"}").RootElement;

        // Act
        var response = sut.Get(arguments);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("jobs");
        json.ShouldContain("features-jobs.md");
        json.ShouldContain("bdk_jobs_list");
    }

    [Fact]
    public void McpGuidanceTools_WhenQueryMentionsJobTriggeringOrchestration_ReturnsCombinedGuidance()
    {
        // Arrange
        var sut = new McpGuidanceTools();
        var arguments = JsonDocument.Parse("{\"query\":\"give me guidance on how to implement a new job that triggers an orchestration\"}").RootElement;

        // Act
        var response = sut.Get(arguments);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("features-jobs.md");
        json.ShouldContain("features-orchestrations.md");
        response.Next.Select(next => next.Tool).ShouldContain("bdk_project_summary");
    }

    [Fact]
    public void McpGuidanceTools_WhenListIsCalled_ReturnsMajorFeatureTopics()
    {
        // Arrange
        var sut = new McpGuidanceTools();

        // Act
        var response = sut.List(JsonDocument.Parse("{}").RootElement);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("caching");
        json.ShouldContain("commands_queries");
        json.ShouldContain("domain_events");
        json.ShouldContain("document_storage");
        json.ShouldContain("monitoring");
    }

    [Fact]
    public void McpGuidanceTools_WhenQueryMentionsSpecification_ReturnsSpecificationGuidance()
    {
        // Arrange
        var sut = new McpGuidanceTools();
        var arguments = JsonDocument.Parse("{\"query\":\"give me guidance on adding a reusable repository specification\"}").RootElement;

        // Act
        var response = sut.Get(arguments);

        // Assert
        response.Available.ShouldBeTrue();
        var json = JsonSerializer.Serialize(response.Data);
        json.ShouldContain("features-domain-specifications.md");
        json.ShouldContain("features-domain-repositories.md");
    }

    [Fact]
    public void McpGuidanceTools_WhenTopicIsUnknown_ReturnsAvailableTopicsHint()
    {
        // Arrange
        var sut = new McpGuidanceTools();
        var arguments = JsonDocument.Parse("{\"topic\":\"unknown\"}").RootElement;

        // Act
        var response = sut.Get(arguments);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.FeatureUnavailable);
        response.Next.Select(next => next.Tool).ShouldContain("bdk_guidance_list");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bdk-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteDescriptor(string registryPath, HostRuntimeDescriptor descriptor)
    {
        var path = Path.Combine(registryPath, descriptor.RuntimeId + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(descriptor, CliJson.Options));
    }

    private static HostRuntimeDescriptor CreateMcpDescriptor(string runtimeId, string workspacePath, int processId)
        => new()
        {
            RuntimeId = runtimeId,
            ApplicationName = "WeatherFiesta",
            EnvironmentName = "Development",
            WorkspacePath = workspacePath,
            ContentRootPath = workspacePath,
            ProcessId = processId,
            StartedAt = DateTimeOffset.UtcNow,
            Assembly = new HostRuntimeAssemblyDescriptor { Name = "WeatherFiesta", Version = "1.0.0" },
            Features =
            {
                ["mcp"] = new HostFeatureEndpointMetadata
                {
                    ProtocolVersion = 1,
                    Transport = "named-pipe",
                    Endpoint = runtimeId,
                    Nonce = "nonce"
                }
            }
        };

    private static McpApiReferenceIndex CreateApiIndex(IReadOnlyList<McpApiReferenceIndexSymbol> symbols)
        => new()
        {
            SchemaVersion = 1,
            SiteUrl = "https://bridgingit-gmbh.github.io/bITdevKit/api/",
            Source = "docfx-mref",
            Symbols = symbols
        };

    private static McpApiReferenceIndexSymbol CreateApiIndexSymbol(
        string uid,
        string name,
        string kind,
        string topic,
        string ns = "BridgingIT.DevKit.Common")
        => new()
        {
            Uid = uid,
            Name = name,
            FullName = uid,
            Kind = kind,
            Namespace = ns,
            Assembly = ns.Replace("BridgingIT.DevKit.", "BridgingIT.DevKit."),
            Summary = $"{name} helps with {topic} implementation.",
            Href = $"obj/api/{uid}.html",
            Detail = $"agent-symbols/{uid}.json",
            Topics = [topic]
        };

    private sealed class FakeHostProcessManager : IHostProcessManager
    {
        public List<int> KilledProcessIds { get; } = [];

        public HostProcessKillResult Kill(int processId)
        {
            this.KilledProcessIds.Add(processId);
            return HostProcessKillResult.Success();
        }
    }

    private sealed class FakeMcpDocumentationSource(IReadOnlyList<McpDocumentationDocument> documents) : IMcpDocumentationSource
    {
        public string Name => "official test documentation";

        public Task<IReadOnlyList<McpDocumentationDocument>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(documents);

        public Task<McpDocumentationDocument> GetAsync(string source, CancellationToken cancellationToken)
            => Task.FromResult(documents.FirstOrDefault(document => string.Equals(document.Source, source, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeMcpApiReferenceSource(
        McpApiReferenceIndex index,
        IReadOnlyDictionary<string, McpApiReferenceSymbol> symbols = null) : IMcpApiReferenceSource
    {
        public string Name => "official test API reference";

        public Task<McpApiReferenceIndex> GetIndexAsync(CancellationToken cancellationToken)
            => Task.FromResult(index);

        public Task<McpApiReferenceSymbol> GetSymbolAsync(string uid, CancellationToken cancellationToken)
        {
            if (symbols is not null && symbols.TryGetValue(uid, out var symbol))
            {
                return Task.FromResult(symbol);
            }

            var entry = index.Symbols.FirstOrDefault(symbol => string.Equals(symbol.Uid, uid, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(entry is null
                ? null
                : new McpApiReferenceSymbol
                {
                    Uid = entry.Uid,
                    Name = entry.Name,
                    FullName = entry.FullName,
                    Kind = entry.Kind,
                    Namespace = entry.Namespace,
                    Assembly = entry.Assembly,
                    Summary = entry.Summary,
                    Url = "https://bridgingit-gmbh.github.io/bITdevKit/api/" + entry.Href,
                    Topics = entry.Topics
                });
        }
    }
}