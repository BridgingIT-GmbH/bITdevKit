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
    public const string ApplicationName = "bdk:ApplicationName";

    /// <summary>
    /// The content root path property key.
    /// </summary>
    public const string ContentRootPath = "bdk:ContentRootPath";

    /// <summary>
    /// The host environment property key.
    /// </summary>
    public const string HostEnvironment = "bdk:HostEnvironment";

    /// <summary>
    /// The generic host builder property key.
    /// </summary>
    public const string HostBuilder = "bdk:HostBuilder";

    /// <summary>
    /// The host application builder property key.
    /// </summary>
    public const string HostApplicationBuilder = "bdk:HostApplicationBuilder";

    /// <summary>
    /// The logging builder property key.
    /// </summary>
    public const string LoggingBuilder = "bdk:LoggingBuilder";

    /// <summary>
    /// The workspace path property key.
    /// </summary>
    public const string WorkspacePath = "bdk:WorkspacePath";

    /// <summary>
    /// The property key of the <see cref="Action"/> that runs once when configuration composition is complete. See
    /// <see cref="DevKitApplicationBuilderConfigurationExtensions.CompleteConfiguration{TBuilder}"/>.
    /// </summary>
    public const string ConfigurationCompletion = "bdk:ConfigurationCompletion";
}
