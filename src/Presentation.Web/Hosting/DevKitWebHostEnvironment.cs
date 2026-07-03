namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting;

/// <summary>
/// Adapts an ASP.NET Core web host environment to the common DevKit host environment abstraction.
/// </summary>
/// <param name="inner">The ASP.NET Core web host environment.</param>
/// <example>
/// <code>
/// var environment = new DevKitWebHostEnvironment(app.Environment);
/// </code>
/// </example>
public sealed class DevKitWebHostEnvironment(IWebHostEnvironment inner) : IDevKitHostEnvironment
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