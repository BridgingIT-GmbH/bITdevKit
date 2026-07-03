// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves local file paths for bdk MCP server session descriptors.
/// </summary>
/// <example>
/// <code>
/// var directory = McpServerSessionPath.GetSessionDirectory(runtimeDescriptorDirectory);
/// </code>
/// </example>
public static class McpServerSessionPath
{
    /// <summary>
    /// Gets the MCP server session directory name.
    /// </summary>
    public const string DirectoryName = "mcp-sessions";

    /// <summary>
    /// Gets the MCP server session descriptor directory from the runtime descriptor directory.
    /// </summary>
    /// <param name="runtimeDescriptorDirectory">The runtime descriptor directory.</param>
    /// <returns>The MCP server session descriptor directory.</returns>
    public static string GetSessionDirectory(string runtimeDescriptorDirectory)
    {
        var descriptorDirectory = string.IsNullOrWhiteSpace(runtimeDescriptorDirectory)
            ? Path.GetTempPath()
            : runtimeDescriptorDirectory;
        var hostsDirectory = Directory.GetParent(descriptorDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(hostsDirectory))
        {
            hostsDirectory = Path.GetDirectoryName(runtimeDescriptorDirectory) ?? Path.GetTempPath();
        }

        return Path.Combine(hostsDirectory, DirectoryName);
    }

    /// <summary>
    /// Gets the MCP server session descriptor file path.
    /// </summary>
    /// <param name="sessionDirectory">The session descriptor directory.</param>
    /// <param name="workspaceHash">The workspace hash.</param>
    /// <param name="processId">The MCP server process id.</param>
    /// <returns>The session descriptor file path.</returns>
    public static string GetSessionFilePath(string sessionDirectory, string workspaceHash, int processId)
        => Path.Combine(sessionDirectory, $"{workspaceHash}-{processId}.json");
}
