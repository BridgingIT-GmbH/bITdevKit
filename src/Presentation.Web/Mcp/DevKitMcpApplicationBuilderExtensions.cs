namespace BridgingIT.DevKit.Presentation.Web;

using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides DevKit MCP registration extensions for web application builders.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args)
///     .AddMcp(mcp => mcp.WithHandler&lt;CommerceMcpHandler&gt;());
/// </code>
/// </example>
public static class DevKitMcpApplicationBuilderExtensions
{
    /// <summary>
    /// Registers app-side MCP handlers using the DevKit web application builder.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The DevKit application builder.</param>
    /// <param name="configure">The MCP registration callback.</param>
    /// <returns>The same builder for chaining.</returns>
    public static TBuilder AddMcp<TBuilder>(
        this TBuilder builder,
        Action<DevKitMcpRegistrationBuilder> configure)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var mcpBuilder = new DevKitMcpRegistrationBuilder(builder.Services, IsMcpEnabledByDefault(builder));
        configure(mcpBuilder);
        mcpBuilder.Apply();

        return builder;
    }

    /// <summary>
    /// Registers app-side MCP handlers using the DevKit web application builder.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The DevKit application builder.</param>
    /// <returns>The same builder for chaining.</returns>
    public static TBuilder AddMcp<TBuilder>(this TBuilder builder)
        where TBuilder : IDevKitApplicationBuilder
        => builder.AddMcp(_ => { });

    private static bool IsMcpEnabledByDefault(IDevKitApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(DevKitWebApplicationBuilderProperties.LocalToolingDecision, out var value) &&
            value is DevKitLocalToolingDecision decision)
        {
            return decision.McpEnabled;
        }

        return string.Equals(builder.Environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Builds app-side MCP handler registrations.
/// </summary>
/// <example>
/// <code>
/// mcp.Enabled(true)
///     .WithHandler&lt;CommerceMcpHandler&gt;()
///     .WithHandlersFromAssembly&lt;CommerceModule&gt;();
/// </code>
/// </example>
public sealed class DevKitMcpRegistrationBuilder
{
    private readonly List<Action<IServiceCollection>> registrations = [];
    private readonly IServiceCollection services;
    private bool enabled;

    internal DevKitMcpRegistrationBuilder(IServiceCollection services, bool enabled)
    {
        this.services = services;
        this.enabled = enabled;
    }

    /// <summary>
    /// Enables or disables project MCP handler registration.
    /// </summary>
    /// <param name="enabled">A value indicating whether project MCP handlers should be registered.</param>
    /// <returns>The same builder for chaining.</returns>
    public DevKitMcpRegistrationBuilder Enabled(bool enabled = true)
    {
        this.enabled = enabled;

        return this;
    }

    /// <summary>
    /// Registers one app-side MCP handler.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    public DevKitMcpRegistrationBuilder WithHandler<THandler>()
        where THandler : class, IMcpHandler
    {
        this.registrations.Add(services => services.AddMcpHandler<THandler>());

        return this;
    }

    /// <summary>
    /// Registers app-side MCP handlers from the assembly containing <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">The marker type.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    public DevKitMcpRegistrationBuilder WithHandlersFromAssembly<TMarker>()
        => this.WithHandlersFromAssembly(typeof(TMarker).Assembly);

    /// <summary>
    /// Registers app-side MCP handlers from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The same builder for chaining.</returns>
    public DevKitMcpRegistrationBuilder WithHandlersFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        this.registrations.Add(services => services.AddMcpHandlersFromAssembly(assembly));

        return this;
    }

    internal void Apply()
    {
        if (!this.enabled)
        {
            return;
        }

        foreach (var registration in this.registrations)
        {
            registration(this.services);
        }
    }
}
