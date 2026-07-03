namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides the DevKit-aware entry point for ASP.NET Core web applications.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args);
/// var app = builder.Build();
/// app.Run();
/// </code>
/// </example>
public static class DevKitWebApplication
{
    /// <summary>
    /// Creates a DevKit-aware web application builder.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The DevKit web application builder.</returns>
    public static DevKitWebApplicationBuilder CreateBuilder(string[] args)
        => CreateBuilder(args, null);

    /// <summary>
    /// Creates a DevKit-aware web application builder with explicit options.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configure">The options configuration callback.</param>
    /// <returns>The DevKit web application builder.</returns>
    public static DevKitWebApplicationBuilder CreateBuilder(
        string[] args,
        Action<DevKitWebApplicationOptionsBuilder> configure)
    {
        var webBuilder = WebApplication.CreateBuilder(args);
        var options = new DevKitWebApplicationOptions();
        configure?.Invoke(new DevKitWebApplicationOptionsBuilder(options));

        return new DevKitWebApplicationBuilder(webBuilder, options);
    }
}