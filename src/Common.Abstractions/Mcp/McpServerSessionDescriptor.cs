// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes a running bdk MCP stdio server session for local dashboards.
/// </summary>
/// <example>
/// <code>
/// var descriptor = new McpServerSessionDescriptor { WorkspaceHash = "abc123", Status = "running" };
/// </code>
/// </example>
public sealed class McpServerSessionDescriptor
{
    /// <summary>
    /// Gets or sets the descriptor schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = McpServerSessionDescriptorSchema.CurrentVersion;

    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the MCP server process id.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the workspace path used by the MCP server.
    /// </summary>
    public string WorkspacePath { get; set; }

    /// <summary>
    /// Gets or sets the stable workspace hash.
    /// </summary>
    public string WorkspaceHash { get; set; }

    /// <summary>
    /// Gets or sets the runtime id supplied through command-line options, when present.
    /// </summary>
    public string ExplicitRuntimeId { get; set; }

    /// <summary>
    /// Gets or sets the runtime id currently targeted by the MCP server.
    /// </summary>
    public string SelectedRuntimeId { get; set; }

    /// <summary>
    /// Gets or sets the selected runtime source.
    /// </summary>
    public string SelectionSource { get; set; }

    /// <summary>
    /// Gets or sets the enabled MCP toolsets.
    /// </summary>
    public string[] Toolsets { get; set; } = [];

    /// <summary>
    /// Gets or sets the session start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the last heartbeat timestamp.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>
    /// Gets or sets the session status.
    /// </summary>
    public string Status { get; set; }
}

/// <summary>
/// Contains MCP server session descriptor schema constants.
/// </summary>
/// <example>
/// <code>
/// if (descriptor.SchemaVersion == McpServerSessionDescriptorSchema.CurrentVersion) { }
/// </code>
/// </example>
public static class McpServerSessionDescriptorSchema
{
    /// <summary>
    /// Gets the current MCP server session descriptor schema version.
    /// </summary>
    public const int CurrentVersion = 1;
}
