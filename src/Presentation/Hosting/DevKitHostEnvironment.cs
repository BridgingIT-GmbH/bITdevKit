namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Adapts a generic host environment to the common DevKit host environment abstraction.
/// </summary>
/// <param name="inner">The generic host environment.</param>
/// <example>
/// <code>
/// var environment = new DevKitHostEnvironment(builder.Environment);
/// </code>
/// </example>
public sealed class DevKitHostEnvironment(IHostEnvironment inner) : IDevKitHostEnvironment
{
    /// <summary>
    /// Gets the application name.
    /// </summary>
    public string ApplicationName => inner.ApplicationName;

    /// <summary>
    /// Gets the environment name.
    /// </summary>
    public string EnvironmentName => inner.EnvironmentName;

    /// <summary>
    /// Gets the content root path.
    /// </summary>
    public string ContentRootPath => inner.ContentRootPath;
}