// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Reads local bdk MCP stdio server session heartbeats for the current web host.
/// </summary>
/// <example>
/// <code>
/// var snapshot = reader.GetSnapshot();
/// </code>
/// </example>
public sealed class McpServerSessionReader(HostDescriptorOptions options)
{
    /// <summary>
    /// Gets the duration after which a server heartbeat is treated as stale.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the current MCP server session snapshot.
    /// </summary>
    /// <returns>The current MCP server session snapshot.</returns>
    public McpServerSessionSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var workspaceHash = ComputeHash(options.WorkspacePath);
        var sessionDirectory = McpServerSessionPath.GetSessionDirectory(options.RegistryPath);
        var sessions = Directory.Exists(sessionDirectory)
            ? Directory.EnumerateFiles(sessionDirectory, "*.json")
                .Select(ReadSession)
                .Where(session => session is not null)
                .Where(session => string.Equals(session.WorkspaceHash, workspaceHash, StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : [];
        var activeSessions = sessions
            .Where(session => session.SchemaVersion == McpServerSessionDescriptorSchema.CurrentVersion)
            .Where(session => now - session.LastSeenAt <= StaleAfter)
            .OrderByDescending(session => session.LastSeenAt)
            .ToArray();
        var targetedSessions = activeSessions
            .Where(session => string.Equals(session.SelectedRuntimeId, options.RuntimeId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new McpServerSessionSnapshot
        {
            SessionDirectory = sessionDirectory,
            WorkspaceHash = workspaceHash,
            ActiveSessions = activeSessions,
            TargetedSessions = targetedSessions,
            StaleSessions = sessions.Except(activeSessions).ToArray(),
            CurrentRuntimeId = options.RuntimeId,
            IsConnected = targetedSessions.Length > 0,
            Status = targetedSessions.Length > 0
                ? "connected"
                : activeSessions.Length > 0
                    ? "running"
                    : "offline"
        };
    }

    private static McpServerSessionDescriptor ReadSession(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<McpServerSessionDescriptor>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

/// <summary>
/// Describes MCP server session state for the current web host.
/// </summary>
/// <example>
/// <code>
/// if (snapshot.IsConnected) { }
/// </code>
/// </example>
public sealed class McpServerSessionSnapshot
{
    /// <summary>
    /// Gets the session descriptor directory.
    /// </summary>
    public string SessionDirectory { get; init; }

    /// <summary>
    /// Gets the current workspace hash.
    /// </summary>
    public string WorkspaceHash { get; init; }

    /// <summary>
    /// Gets the current runtime id.
    /// </summary>
    public string CurrentRuntimeId { get; init; }

    /// <summary>
    /// Gets the active MCP server sessions for this workspace.
    /// </summary>
    public McpServerSessionDescriptor[] ActiveSessions { get; init; } = [];

    /// <summary>
    /// Gets active MCP server sessions targeting this runtime.
    /// </summary>
    public McpServerSessionDescriptor[] TargetedSessions { get; init; } = [];

    /// <summary>
    /// Gets stale MCP server sessions for this workspace.
    /// </summary>
    public McpServerSessionDescriptor[] StaleSessions { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether an active MCP server targets this runtime.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Gets the MCP server status.
    /// </summary>
    public string Status { get; init; }
}
