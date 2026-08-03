// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a provider-neutral diagnostics snapshot for Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var clients = snapshot.Clients;
/// </code>
/// </example>
public sealed class BlobStorageDiagnosticsSnapshot
{
    /// <summary>
    /// Gets the total number of registered blob clients.
    /// </summary>
    /// <example>
    /// <code>
    /// var count = snapshot.ClientCount;
    /// </code>
    /// </example>
    public int ClientCount { get; init; }

    /// <summary>
    /// Gets the number of healthy clients.
    /// </summary>
    /// <example>
    /// <code>
    /// var healthy = snapshot.HealthyClientCount;
    /// </code>
    /// </example>
    public int HealthyClientCount { get; init; }

    /// <summary>
    /// Gets the number of unhealthy clients.
    /// </summary>
    /// <example>
    /// <code>
    /// var failed = snapshot.UnhealthyClientCount;
    /// </code>
    /// </example>
    public int UnhealthyClientCount { get; init; }

    /// <summary>
    /// Gets diagnostics for each registered client.
    /// </summary>
    /// <example>
    /// <code>
    /// var names = snapshot.Clients.Select(client => client.Name);
    /// </code>
    /// </example>
    public IReadOnlyList<BlobStorageClientDiagnostics> Clients { get; init; } = [];
}
