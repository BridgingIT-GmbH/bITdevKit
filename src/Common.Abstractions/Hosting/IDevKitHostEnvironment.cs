namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides host environment information without exposing concrete ASP.NET Core hosting types.
/// </summary>
/// <example>
/// <code>
/// if (builder.Environment.EnvironmentName == "Development")
/// {
///     builder.Services.AddSingleton&lt;DevelopmentOnlyService&gt;();
/// }
/// </code>
/// </example>
public interface IDevKitHostEnvironment
{
    /// <summary>
    /// Gets the application name.
    /// </summary>
    string ApplicationName { get; }

    /// <summary>
    /// Gets the environment name.
    /// </summary>
    string EnvironmentName { get; }

    /// <summary>
    /// Gets the content root path.
    /// </summary>
    string ContentRootPath { get; }
}