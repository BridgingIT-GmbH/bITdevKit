namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Resolves user-local host descriptor registry paths.
/// </summary>
/// <example>
/// <code>
/// var path = HostDescriptorPath.GetDefaultRegistryPath();
/// </code>
/// </example>
public static class HostDescriptorPath
{
    /// <summary>
    /// Gets the default runtime descriptor registry path.
    /// </summary>
    /// <returns>The registry path.</returns>
    public static string GetDefaultRegistryPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "bdk", "hosts", "runtimes");
        }

        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return Path.Combine(runtimeDirectory, "bdk", "hosts", "runtimes");
        }

        return Path.Combine(Path.GetTempPath(), "bdk", "hosts", "runtimes");
    }

    /// <summary>
    /// Gets the descriptor file path for a runtime.
    /// </summary>
    /// <param name="options">The descriptor options.</param>
    /// <returns>The descriptor file path.</returns>
    public static string GetDescriptorPath(HostDescriptorOptions options)
        => Path.Combine(options.RegistryPath, $"{options.RuntimeId}-{Environment.ProcessId}.json");
}