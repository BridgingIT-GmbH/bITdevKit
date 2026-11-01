// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Selects meaningful storage reads and optional host-owned logical resources without default path, key, query, or content capture.</summary>
/// <example><code>builder.WithAuditing(new() { SelectRead = operation => operation == "download" });</code></example>
public sealed record StorageAuditOptions
{
    /// <summary>Explicit local read selection, evaluated only when capture is enabled.</summary>
    /// <example><code>var options = new StorageAuditOptions { SelectRead = operation => operation == "get" };</code></example>
    public Func<string, bool> SelectRead { get; init; }

    /// <summary>Projects an approved logical resource from host services, client alias, and operation; raw keys and paths are not supplied.</summary>
    /// <example><code>var options = new StorageAuditOptions { Resource = (services, client, operation) => new("CustomerExport", "42") };</code></example>
    public Func<IServiceProvider, string, string, AuditResourceReference> Resource { get; init; }
}
