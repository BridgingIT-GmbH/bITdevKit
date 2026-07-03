namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides well-known property keys used by DevKit builder extensions.
/// </summary>
/// <example>
/// <code>
/// builder.Properties[DevKitBuilderProperties.ApplicationName] = "WeatherFiesta";
/// </code>
/// </example>
public static class DevKitBuilderProperties
{
    /// <summary>
    /// The application name property key.
    /// </summary>
    public const string ApplicationName = "DevKit:ApplicationName";

    /// <summary>
    /// The content root path property key.
    /// </summary>
    public const string ContentRootPath = "DevKit:ContentRootPath";

    /// <summary>
    /// The generic host builder property key.
    /// </summary>
    public const string HostBuilder = "DevKit:HostBuilder";

    /// <summary>
    /// The host application builder property key.
    /// </summary>
    public const string HostApplicationBuilder = "DevKit:HostApplicationBuilder";

    /// <summary>
    /// The logging builder property key.
    /// </summary>
    public const string LoggingBuilder = "DevKit:LoggingBuilder";

    /// <summary>
    /// The workspace path property key.
    /// </summary>
    public const string WorkspacePath = "DevKit:WorkspacePath";
}