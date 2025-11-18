// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation;
using Spectre.Console;

/// <summary>
/// Extension methods for registering console command services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers console commands for non-interactive usage (single-run invocation in console apps).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder for adding additional commands.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddConsoleCommands(this IServiceCollection services, Action<ConsoleCommandsBuilder> configure)
    {
        services.AddSingleton(_ =>
            AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.Detect, ColorSystem = ColorSystemSupport.Detect }));

        services.AddTransient<IConsoleCommand, HelpConsoleCommand>();
        services.AddTransient<IConsoleCommand, InfoConsoleCommand>();

        if (configure is not null)
        {
            configure(new ConsoleCommandsBuilder(services));
        }

        return services;
    }
}