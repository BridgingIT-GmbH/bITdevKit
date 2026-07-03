namespace BridgingIT.DevKit.Cli;

using System.Security.Cryptography;
using BridgingIT.DevKit.Common;

/// <summary>
/// Resolves the workspace used by workspace-aware CLI commands.
/// </summary>
public sealed class WorkspaceResolver
{
    /// <summary>
    /// Resolves a workspace from an explicit path or the current directory.
    /// </summary>
    /// <param name="explicitPath">The optional explicit workspace path.</param>
    /// <returns>The resolved workspace context.</returns>
    public CliWorkspaceContext Resolve(string explicitPath)
    {
        var workspacePath = !string.IsNullOrWhiteSpace(explicitPath)
            ? WorkspacePathUtilities.Normalize(explicitPath)
            : WorkspacePathUtilities.ResolveWorkspaceRoot(Directory.GetCurrentDirectory());

        return new CliWorkspaceContext
        {
            Path = workspacePath,
            Hash = ComputeHash(workspacePath)
        };
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
