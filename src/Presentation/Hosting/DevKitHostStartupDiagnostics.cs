namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Describes DevKit host startup diagnostics written for local tooling visibility.
/// </summary>
/// <example>
/// <code>
/// var diagnostics = new DevKitHostStartupDiagnostics("generic", "App", "Development", ".", false, false, null, false, false, false, [], "Disabled.");
/// </code>
/// </example>
public sealed record DevKitHostStartupDiagnostics(
    string HostKind,
    string ApplicationName,
    string EnvironmentName,
    string ContentRootPath,
    bool DescriptorEligible,
    bool DescriptorWriterRegistered,
    string DescriptorPath,
    bool LocalToolingEnabled,
    bool ConsoleCommandsEnabled,
    bool McpEnabled,
    IReadOnlyCollection<string> Features,
    string Reason);