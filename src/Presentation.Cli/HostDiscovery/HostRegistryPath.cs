namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Resolves OS user-local host registry paths.
/// </summary>
public static class HostRegistryPath
{
    /// <summary>
    /// Gets the default host registry paths for the current operating system.
    /// </summary>
    /// <returns>The default host registry options.</returns>
    public static HostRegistryOptions GetDefault()
    {
        var root = GetRootPath();
        return new HostRegistryOptions
        {
            RuntimePath = Path.Combine(root, "hosts", "runtimes"),
            SelectionPath = Path.Combine(root, "hosts", "selections")
        };
    }

    private static string GetRootPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, "bdk");
            }
        }

        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir))
        {
            return Path.Combine(runtimeDir, "bdk");
        }

        var tempDir = Environment.GetEnvironmentVariable("TMPDIR");
        return Path.Combine(string.IsNullOrWhiteSpace(tempDir) ? Path.GetTempPath() : tempDir, "bdk");
    }
}
