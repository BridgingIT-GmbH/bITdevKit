namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides helpers for resolving and normalizing repository workspace paths.
/// </summary>
/// <example>
/// <code>
/// var workspacePath = WorkspacePathUtilities.ResolveWorkspaceRoot(Environment.CurrentDirectory);
/// </code>
/// </example>
public static class WorkspacePathUtilities
{
    /// <summary>
    /// Walks up from a start directory and returns the nearest workspace root.
    /// </summary>
    /// <param name="startPath">The directory to start from.</param>
    /// <returns>The nearest directory containing a solution file or Git metadata; otherwise, the normalized start path.</returns>
    /// <example>
    /// <code>
    /// var workspaceRoot = WorkspacePathUtilities.ResolveWorkspaceRoot(contentRootPath);
    /// </code>
    /// </example>
    public static string ResolveWorkspaceRoot(string startPath)
    {
        var fallbackPath = Normalize(startPath);
        var current = new DirectoryInfo(fallbackPath);
        while (current is not null)
        {
            if (IsWorkspaceRoot(current))
            {
                return Normalize(current.FullName);
            }

            current = current.Parent;
        }

        return fallbackPath;
    }

    /// <summary>
    /// Normalizes a workspace path for stable comparisons and hashing.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The full normalized path.</returns>
    /// <example>
    /// <code>
    /// var normalized = WorkspacePathUtilities.Normalize(workspacePath);
    /// </code>
    /// </example>
    public static string Normalize(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    private static bool IsWorkspaceRoot(DirectoryInfo directory)
        => directory.EnumerateFiles("*.slnx").Any() ||
            directory.EnumerateFiles("*.sln").Any() ||
            Directory.Exists(Path.Combine(directory.FullName, ".git"));
}