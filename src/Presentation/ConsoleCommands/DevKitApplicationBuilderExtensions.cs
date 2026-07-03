namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides DevKit Console Command starter extensions for application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args)
///     .AddConsoleCommands(commands =&gt; commands.AddCommand&lt;SampleConsoleCommand&gt;());
/// </code>
/// </example>
public static class ConsoleCommandsDevKitApplicationBuilderExtensions
{
    /// <summary>
    /// Registers Console Commands for non-interactive usage.
    /// </summary>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="configure">The command configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static TBuilder AddConsoleCommands<TBuilder>(
        this TBuilder builder,
        Action<ConsoleCommandsBuilder> configure)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddConsoleCommands(configure);

        return builder;
    }
}