namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines the supported stages for feature-owned DevKit builder hooks.
/// </summary>
/// <example>
/// <code>
/// var stage = DevKitFeatureHookStage.ConfigureServices;
/// </code>
/// </example>
public enum DevKitFeatureHookStage
{
    /// <summary>
    /// The hook runs while application services are being configured.
    /// </summary>
    ConfigureServices,

    /// <summary>
    /// The hook runs after services are configured but before the application is built.
    /// </summary>
    BeforeBuild,

    /// <summary>
    /// The hook runs after the application is built.
    /// </summary>
    AfterBuild
}