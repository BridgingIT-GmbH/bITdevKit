// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Prevents operation-owned secret keys from entering JSON serialization pipelines.</summary>
/// <example><code>JsonSerializer.Serialize(key); // throws NotSupportedException</code></example>
public sealed class AuditIntegrityKeyJsonConverter : JsonConverter<AuditIntegrityKey>
{
    /// <summary>Rejects JSON key material; hosts resolve keys through their protected external source.</summary>
    /// <example><code>JsonSerializer.Deserialize&lt;AuditIntegrityKey&gt;(json); // throws</code></example>
    public override AuditIntegrityKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("Audit integrity keys cannot be deserialized.");

    /// <summary>Rejects JSON output without inspecting or emitting key material.</summary>
    /// <example><code>JsonSerializer.Serialize(key); // throws</code></example>
    public override void Write(Utf8JsonWriter writer, AuditIntegrityKey value, JsonSerializerOptions options) => throw new NotSupportedException("Audit integrity keys cannot be serialized.");
}
