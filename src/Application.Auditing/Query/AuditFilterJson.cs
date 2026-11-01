// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Reads the shared FilterModel grammar without invoking client-selected CLR types.</summary>
/// <example><code>var filter = AuditFilterJson.Parse(bodyBytes);</code></example>
public static class AuditFilterJson
{
    /// <summary>Creates isolated filter options retaining arbitrary scalar values as JSON elements.</summary>
    /// <remarks>No enumeration-type or arbitrary-object converter is installed.</remarks>
    /// <example><code>var options = AuditFilterJson.CreateOptions();</code></example>
    public static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new EnumMemberConverter<FilterOperator>(), new EnumMemberConverter<FilterLogicOperator>(),
            new EnumMemberConverter<FilterCustomType>(), new EnumMemberConverter<OrderDirection>(),
            new AuditSpecificationNodeJsonConverter()
        }
    };

    /// <summary>Rejects malformed, duplicate-key, oversized or structurally unbounded filter bodies.</summary>
    /// <example><code>var filter = AuditFilterJson.Parse(utf8Body);</code></example>
    public static Result<FilterModel> Parse(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > 65_536) return Result<FilterModel>.Failure(new AuditError("AuditQueryInvalid"));
        try
        {
            using var document = JsonDocument.Parse(utf8, new() { MaxDepth = 32 });
            _ = AuditCanonicalizerV1.Encode(document.RootElement);
            var filter = document.RootElement.Deserialize<FilterModel>(CreateOptions());
            return filter is null ? Result<FilterModel>.Failure(new AuditError("AuditQueryInvalid")) : Result<FilterModel>.Success(AuditFilterSnapshot.Copy(filter));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Result<FilterModel>.Failure(new AuditError("AuditQueryInvalid"));
        }
    }
}
