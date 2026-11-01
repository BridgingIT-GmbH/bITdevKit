// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Pins the export JSON representation independently of stored canonicalization and host response settings.</summary>
/// <example><code>var json = AuditExportJson.CreateOptions();</code></example>
public static class AuditExportJson
{
    /// <summary>Creates a private compact camel-case profile that omits unused entry kinds and source envelopes.</summary>
    /// <example><code>var bytes = JsonSerializer.SerializeToUtf8Bytes(metadata, AuditExportJson.CreateOptions());</code></example>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false, MaxDepth = 64 };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
