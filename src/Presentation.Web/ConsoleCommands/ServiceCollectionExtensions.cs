// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Spectre.Console;

/// <summary>
/// Extension methods for registering console command services.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers interactive console commands and supporting runtime services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder for additional commands.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddConsoleCommandsInteractive(this IServiceCollection services, Action<ConsoleCommandsBuilder> configure = null)
    {
        services.AddSingleton(_ => AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.Detect, ColorSystem = ColorSystemSupport.Detect }));
        services.AddSingleton<ConsoleCommandInteractiveRuntimeStats>();

        // built-in commands
        services.AddTransient<IConsoleCommand, StatusConsoleCommand>();
        services.AddTransient<IConsoleCommand, MemoryConsoleCommand>();
        services.AddTransient<IConsoleCommand, EnvConsoleCommand>();
        services.AddTransient<IConsoleCommand, KestrelPortsConsoleCommand>();
        services.AddTransient<IConsoleCommand, ClearConsoleCommand>();
        services.AddTransient<IConsoleCommand, HelpConsoleCommand>();
        services.AddTransient<IConsoleCommand, QuitConsoleCommand>();
        services.AddTransient<IConsoleCommand, MetricsConsoleCommand>();
        services.AddTransient<IConsoleCommand, InfoConsoleCommand>();
        services.AddTransient<IConsoleCommand, GcCollectConsoleCommand>();
        services.AddTransient<IConsoleCommand, ThreadsConsoleCommand>();
        services.AddTransient<IConsoleCommand, RestartConsoleCommand>();
        services.AddTransient<IConsoleCommand, SampleConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistoryListConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistoryClearConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistorySearchConsoleCommand>();
        services.AddTransient<IConsoleCommand, EchoConsoleCommand>();
        services.AddTransient<IConsoleCommand, BrowseConsoleCommand>();
        services.AddTransient<IConsoleCommand, RequesterInfoConsoleCommand>();

        // extra command registrations
        if (configure is not null)
        {
            configure(new ConsoleCommandsBuilder(services));
        }

        // diag group (interactive)
        services.AddTransient<IConsoleCommand, DiagGcConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagThreadsConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagMemConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagPerfConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagEnvConsoleCommand>();

        return services;
    }
}
