// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Dispatches retained documents to supported versioned readers under the configured size bound.</summary>
/// <remarks>The first release supports schema 1. Future readers require explicit implementation and golden fixtures.</remarks>
/// <example><code>var original = registry.Read(bytes);</code></example>
public sealed class AuditRecordReaderRegistry(AuditingOptions options)
{
    private static readonly IReadOnlyList<int> versions = Array.AsReadOnly(new[] { 1 });

    /// <summary>Schema versions with a delivered original-document reader.</summary>
    /// <example><code>var supported = registry.SupportedVersions;</code></example>
    public IReadOnlyList<int> SupportedVersions => versions;

    /// <summary>Rejects oversized or incompatible input before returning a retained original graph.</summary>
    /// <example><code>var record = registry.Read(bytes);</code></example>
    public Result<AuditRecord> Read(ReadOnlyMemory<byte> bytes) => bytes.Length > options.Limits.MaxRecordBytes
        ? Result<AuditRecord>.Failure(new AuditError("AuditRecordTooLarge")) : AuditRecordCodec.Decode(bytes);
}
