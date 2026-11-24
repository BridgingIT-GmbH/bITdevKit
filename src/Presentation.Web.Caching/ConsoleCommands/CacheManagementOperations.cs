// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Defines the finite metadata and operational surface shared by Console and MCP, without source or payload tools.</summary>
/// <example><code>var required = CacheManagementOperations.RequiredAction("clear");</code></example>
public static class CacheManagementOperations
{
    /// <summary>Gets supported fixed operation names; no runtime operation or loader names are activated.</summary>
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "collections", "inspect", "entries", "status", "diagnostics", "preview", "remove", "invalidate", "clear", "pause", "resume",
        "operation-status", "operation-resume", "operation-cancel"
    });

    /// <summary>Checks whether an operation belongs to the bounded management surface.</summary>
    /// <param name="operation">The fixed operation name.</param>
    /// <returns>True for one supported name.</returns>
    public static bool IsSupported(string operation) => Names.Contains(operation, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the independent read or invalidation authority needed for one operation.</summary>
    /// <param name="operation">A supported fixed operation name.</param>
    /// <returns>The action the host must authorize for the current caller.</returns>
    /// <exception cref="ArgumentException">The operation is not supported.</exception>
    public static CacheAccessActions RequiredAction(string operation) => operation?.ToLowerInvariant() switch
    {
        "collections" or "inspect" or "entries" or "status" or "diagnostics" or "preview" or "operation-status" => CacheAccessActions.Read,
        "remove" or "invalidate" or "clear" or "pause" or "resume" or "operation-resume" or "operation-cancel" => CacheAccessActions.Invalidate,
        _ => throw new ArgumentException("Unknown application cache management operation.", nameof(operation))
    };
}