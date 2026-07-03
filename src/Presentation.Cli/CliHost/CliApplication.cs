namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Runs the DevKit CLI through the DevKit Console Commands dispatcher.
/// </summary>
public static class CliApplication
{
    /// <summary>
    /// Runs the CLI for the supplied arguments.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var parsed = CliArgumentParser.Parse(args);
        var output = new CliOutputSettings
        {
            Format = parsed.OutputFormat,
            Quiet = parsed.Quiet,
            Verbose = parsed.Verbose,
            NoColor = parsed.NoColor,
            NoLogo = parsed.NoLogo,
            Banner = parsed.Banner,
            NonInteractive = parsed.NonInteractive,
            IsCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
        };
        var cliConsole = new CliConsole(output);

        if (!string.IsNullOrWhiteSpace(parsed.Error))
        {
            cliConsole.Error(parsed.Error, "invalid_arguments", CliExitCode.InvalidArguments);
            return (int)CliExitCode.InvalidArguments;
        }

        var workspace = new WorkspaceResolver().Resolve(parsed.WorkspacePath);
        var registryOptions = HostRegistryPath.GetDefault();
        var executionState = new CliExecutionState();
        var forwardingContext = new HostRunForwardingContext();
        if (parsed.ShowVersion && parsed.CommandArguments.Length > 0)
        {
            cliConsole.Error("--version can only be used at the root command.", "invalid_arguments", CliExitCode.InvalidArguments);
            return (int)CliExitCode.InvalidArguments;
        }

        if (IsMcpCommand(parsed.CommandArguments))
        {
            return await RunMcpAsync(parsed, workspace, output, registryOptions, cliConsole, executionState, forwardingContext, cancellationToken).ConfigureAwait(false);
        }

        var commandArguments = NormalizeCommandArguments(parsed, forwardingContext);

        try
        {
            await using var services = CreateServices(workspace, output, registryOptions, cliConsole, executionState, forwardingContext);
            var executor = services.GetRequiredService<ConsoleCommandExecutor>();
            var commandLine = BuildCommandLine(commandArguments);
            if (!TryPreflightCommand(commandLine, services, out var preflightError))
            {
                cliConsole.Error(preflightError, "invalid_arguments", CliExitCode.InvalidArguments);
                return (int)CliExitCode.InvalidArguments;
            }

            if (TryWriteJsonHelp(commandArguments, output, cliConsole, services))
            {
                return (int)CliExitCode.Success;
            }

            await CliBannerService.DisplayIfEnabledAsync(output, typeof(CliApplication).Assembly, cancellationToken).ConfigureAwait(false);

            WriteStartupHostSummary(commandArguments, output, services);

            await executor.ExecuteAsync(
                commandLine,
                cliConsole.ExecutorConsole,
                services,
                ConsoleCommandExecutionSource.Terminal,
                cancellationToken).ConfigureAwait(false);

            return (int)executionState.ExitCode;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            cliConsole.Error(exception.Message, "internal_error", CliExitCode.InternalError);
            return (int)CliExitCode.InternalError;
        }
    }

    private static ServiceProvider CreateServices(
        CliWorkspaceContext workspace,
        CliOutputSettings output,
        HostRegistryOptions registryOptions,
        CliConsole cliConsole,
        CliExecutionState executionState,
        HostRunForwardingContext forwardingContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new CliRuntimeContext(workspace, output, registryOptions));
        services.AddSingleton(cliConsole);
        services.AddSingleton(executionState);
        services.AddSingleton(forwardingContext);
        services.AddSingleton(registryOptions);
        services.AddSingleton<HostRuntimeDiscovery>();
        services.AddSingleton<IHostProcessManager, HostProcessManager>();
        services.AddSingleton<HostSelectionStore>();
        services.AddSingleton<HostCommandClient>();
        services.AddSingleton<ConsoleCommandExecutor>();

        var modules = CreateModules();
        var moduleContext = new CliCommandModuleContext
        {
            Workspace = workspace,
            Output = output,
            HostRegistry = registryOptions,
            Environment = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value?.ToString(), StringComparer.OrdinalIgnoreCase)
        };
        foreach (var module in modules)
        {
            module.RegisterServices(services, moduleContext);
        }

        services.AddSingleton<IReadOnlyList<CliModuleInfo>>(modules.Select(module => new CliModuleInfo(module.Name, module.Description)).ToArray());
        services.AddTransient<IConsoleCommand, HelpConsoleCommand>();

        var registry = new CliCommandRegistry();
        foreach (var module in modules)
        {
            module.RegisterCommands(registry);
        }

        services.AddSingleton<IReadOnlyList<CliCommandGroupInfo>>(registry.Groups.ToArray());
        registry.RegisterCommands(services);

        return services.BuildServiceProvider();
    }

    private static bool TryWriteJsonHelp(string[] commandArguments, CliOutputSettings output, CliConsole cliConsole, IServiceProvider services)
    {
        if (!output.IsJson || commandArguments.Length == 0 || !string.Equals(commandArguments[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commands = services.GetServices<IConsoleCommand>().ToArray();
        var groups = services.GetRequiredService<IReadOnlyList<CliCommandGroupInfo>>();
        var groupNames = groups.Select(group => group.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rootCommands = commands
            .Where(command => command is not IGroupedConsoleCommand)
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(command => new
            {
                name = command.Name,
                aliases = command.Aliases.Where(alias => !string.Equals(alias, command.Name, StringComparison.OrdinalIgnoreCase)).ToArray(),
                description = command.Description,
                kind = "command"
            });
        var groupedCommands = groups
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Name,
                aliases = commands.OfType<IGroupedConsoleCommand>()
                    .Where(command => string.Equals(command.GroupName, group.Name, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(command => command.GroupAliases ?? [])
                    .Where(alias => !string.Equals(alias, group.Name, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                description = group.Description,
                kind = "group",
                subcommands = commands.OfType<IGroupedConsoleCommand>()
                    .Where(command => string.Equals(command.GroupName, group.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(command => new { name = command.Name, description = command.Description })
                    .ToArray()
            });

        cliConsole.WriteJson(new
        {
            commands = rootCommands.Cast<object>().Concat(groupedCommands.Cast<object>()).Where(command => command is not null),
            modules = services.GetRequiredService<IReadOnlyList<CliModuleInfo>>().Where(module => !groupNames.Contains(module.Name)).ToArray(),
            exitCode = 0
        });
        return true;
    }

    private static IReadOnlyList<ICliCommandModule> CreateModules()
        =>
        [
            new CoreCliCommandModule(),
            new DocsCliCommandModule(),
            new HostsCliCommandModule(),
            new HostForwardingCliCommandModule(),
            new McpCliCommandModule()
        ];

    private static async Task<int> RunMcpAsync(
        ParsedCommandLine parsed,
        CliWorkspaceContext workspace,
        CliOutputSettings output,
        HostRegistryOptions registryOptions,
        CliConsole cliConsole,
        CliExecutionState executionState,
        HostRunForwardingContext forwardingContext,
        CancellationToken cancellationToken)
    {
        var mcpOptions = McpCliOptions.Parse(parsed.CommandArguments.Skip(1).ToArray(), out var error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            return (int)CliExitCode.InvalidArguments;
        }

        await using var services = CreateServices(workspace, output, registryOptions, cliConsole, executionState, forwardingContext);
        var server = services.GetRequiredService<StdioMcpServer>();
        var sessionPublisher = services.GetRequiredService<McpServerSessionPublisher>();
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sessionTask = sessionPublisher.RunAsync(mcpOptions, sessionCancellation.Token);

        try
        {
            return await server.RunAsync(Console.In, Console.Out, Console.Error, mcpOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await sessionCancellation.CancelAsync().ConfigureAwait(false);
            await sessionTask.ConfigureAwait(false);
        }
    }

    private static void WriteStartupHostSummary(string[] commandArguments, CliOutputSettings output, IServiceProvider services)
    {
        if (output.IsJson || output.Quiet || output.IsCi || commandArguments.Length == 0 || !ShouldShowStartupHostSummary(commandArguments, output))
        {
            return;
        }

        var context = services.GetRequiredService<CliRuntimeContext>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var hosts = discovery.Discover(context.Workspace)
            .Where(host => host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        if (hosts.Length == 0)
        {
            if (IsHostForwardingCommand(commandArguments))
            {
                cliConsole.MarkupLine("[yellow]No running DevKit hosts were found for this workspace.[/]");
                cliConsole.MarkupLine("[grey]Start a local DevKit webapp to enable host commands, MCP runtime tools, and runtime diagnostics.[/]");
            }

            return;
        }

        cliConsole.MarkupLine("[bold]Hosts[/]");
        HostRuntimeTable.Write(cliConsole.Console, hosts);
        cliConsole.WriteLine();
    }

    private static bool ShouldShowStartupHostSummary(IReadOnlyList<string> commandArguments, CliOutputSettings output)
        => IsHostForwardingCommand(commandArguments) || (output.Verbose && !IsCommand(commandArguments, "hosts"));

    private static bool IsHostForwardingCommand(IReadOnlyList<string> commandArguments)
        => IsCommand(commandArguments, "host") && commandArguments.Count >= 2 && string.Equals(commandArguments[1], "run", StringComparison.OrdinalIgnoreCase);

    private static bool IsMcpCommand(IReadOnlyList<string> commandArguments)
        => commandArguments.Count > 0 && string.Equals(commandArguments[0], "mcp", StringComparison.OrdinalIgnoreCase);

    private static bool TryPreflightCommand(string commandLine, IServiceProvider services, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return true;
        }

        var tokens = ConsoleCommandExecutor.SplitArgs(commandLine);
        if (tokens.Length == 0)
        {
            return true;
        }

        var primary = tokens[0];
        var commands = services.GetServices<IConsoleCommand>().ToList();
        var consumed = 1;
        IConsoleCommand command = null;
        var groupedCandidates = commands.OfType<IGroupedConsoleCommand>()
            .Where(candidate => string.Equals(candidate.GroupName, primary, StringComparison.OrdinalIgnoreCase) ||
                candidate.GroupAliases?.Any(alias => string.Equals(alias, primary, StringComparison.OrdinalIgnoreCase)) == true)
            .ToArray();

        if (groupedCandidates.Length != 0)
        {
            if (tokens.Length == 1)
            {
                return true;
            }

            var subcommand = tokens[1];
            consumed = 2;
            command = groupedCandidates.FirstOrDefault(candidate => candidate.Matches(subcommand));
            if (command is null)
            {
                error = $"Unknown subcommand '{subcommand}' for group '{primary}'.";
                return false;
            }
        }
        else
        {
            command = commands.FirstOrDefault(candidate => candidate is not IGroupedConsoleCommand && candidate.Matches(primary));
        }

        if (command is null)
        {
            error = $"Unknown command '{primary}'.";
            return false;
        }

        var (ok, errors) = ConsoleCommandBinder.TryBind(command, tokens.Skip(consumed).ToArray());
        if (ok)
        {
            return true;
        }

        error = errors.FirstOrDefault() ?? "Command arguments are invalid.";
        return false;
    }

    private static string[] NormalizeCommandArguments(ParsedCommandLine parsed, HostRunForwardingContext forwardingContext)
    {
        var commandArguments = parsed.CommandArguments.ToList();

        if (parsed.ShowVersion)
        {
            return ["version"];
        }

        if (parsed.ShowHelp)
        {
            return ["help", .. commandArguments];
        }

        if (commandArguments.Count == 0)
        {
            return ["help"];
        }

        if (IsCommand(commandArguments, "hosts") && commandArguments.Count == 1)
        {
            return ["hosts", "list"];
        }

        if (IsCommand(commandArguments, "host") && commandArguments.Count >= 2 && string.Equals(commandArguments[1], "run", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeHostRunArguments(commandArguments, forwardingContext);
        }

        return commandArguments.ToArray();
    }

    private static string[] NormalizeHostRunArguments(IReadOnlyList<string> arguments, HostRunForwardingContext forwardingContext)
    {
        var localArguments = new List<string> { "host", "run" };
        var forwardingTokens = new List<string>();
        var forwardingBoundaryReached = false;

        for (var index = 2; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (forwardingBoundaryReached)
            {
                forwardingTokens.Add(argument);
                continue;
            }

            if (argument == "--")
            {
                forwardingBoundaryReached = true;
                continue;
            }

            if (argument is "--host" or "--runtime-id")
            {
                localArguments.Add("--host");
                if (index + 1 < arguments.Count)
                {
                    index++;
                    localArguments.Add(arguments[index]);
                }

                continue;
            }

            forwardingTokens.Add(argument);
        }

        forwardingContext.Tokens = forwardingTokens.ToArray();
        return localArguments.ToArray();
    }

    private static bool IsCommand(IReadOnlyList<string> commandArguments, string command)
        => commandArguments.Count > 0 && string.Equals(commandArguments[0], command, StringComparison.OrdinalIgnoreCase);

    private static string BuildCommandLine(IEnumerable<string> tokens)
        => string.Join(' ', tokens.Select(QuoteToken));

    private static string QuoteToken(string token)
        => token.Any(char.IsWhiteSpace) ? $"\"{token.Replace("\"", string.Empty)}\"" : token;
}
