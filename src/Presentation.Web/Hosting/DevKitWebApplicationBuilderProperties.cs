namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides well-known property keys for the DevKit web application builder.
/// </summary>
/// <example>
/// <code>
/// var decision = builder.Properties[DevKitWebApplicationBuilderProperties.LocalToolingDecision];
/// </code>
/// </example>
public static class DevKitWebApplicationBuilderProperties
{
    /// <summary>
    /// The property key for the evaluated local tooling decision.
    /// </summary>
    public const string LocalToolingDecision = "DevKit:LocalToolingDecision";
}