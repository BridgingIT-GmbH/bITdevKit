// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;
using System.Text;

/// <summary>Projects approved values through exclusion, redaction, hashing and size rules.</summary>
/// <example><code>var projected = AuditCapturePolicy.Project("count", AuditValue.From(1L), options);</code></example>
public static class AuditCapturePolicy
{
    /// <summary>Applies the shared field policy to an explicitly projected value.</summary>
    /// <example><code>var projected = AuditCapturePolicy.Project("count", AuditValue.From(1L), options);</code></example>
    public static AuditValue Project(string name, AuditValue value, AuditingOptions options)
    {
        var policy = options.CapturePolicy;
        if (MatchesName(policy.ExcludeNames, name)) return AuditValue.Policy("Excluded");
        if (MatchesName(policy.RedactNames, name)) return AuditValue.Policy("Redacted");
        if (value.Marker is "Excluded" or "Redacted" or "Oversized" or "Unavailable") return AuditValue.Policy(value.Marker);
        if (Encoding.UTF8.GetByteCount(value.Data.GetRawText()) > options.Limits.MaxDetailValueBytes) return AuditValue.Policy("Oversized");
        if (!WithinDepth(value.Data, options.Limits.MaxProjectedJsonDepth)) return AuditValue.Policy("Oversized");
        // Validate duplicate names and JSON shape before this value can poison
        // an otherwise valid root at finalization.
        var canonical = AuditCanonicalizerV1.Encode(value.Data);
        var names = Names(value.Data).ToArray();
        if (names.Any(n => MatchesName(policy.ExcludeNames, n))) return AuditValue.Policy("Excluded");
        if (names.Any(n => MatchesName(policy.RedactNames, n))) return AuditValue.Policy("Redacted");
        return value.Marker is null && (MatchesName(policy.HashNames, name) || names.Any(n => MatchesName(policy.HashNames, n)))
            ? AuditValue.Policy("Hashed", AuditCanonicalizerV1.Hash(canonical)) : value;
    }

    /// <summary>Matches a complete approved name or a property-path segment under the same sensitive-name policy.</summary>
    /// <example><code>var excluded = AuditCapturePolicy.MatchesName(options.CapturePolicy.ExcludeNames, "Address.Email");</code></example>
    public static bool MatchesName(System.Collections.Immutable.ImmutableHashSet<string> configured, string name) => name is not null && (configured.Contains(name) ||
        name.Split(['.', '/', '[', ']'], StringSplitOptions.RemoveEmptyEntries).Any(configured.Contains));

    private static bool WithinDepth(System.Text.Json.JsonElement value, int maximum)
    {
        var pending = new Stack<(System.Text.Json.JsonElement Value, int Depth)>();
        pending.Push((value, 1));
        while (pending.TryPop(out var current))
        {
            if (current.Depth > maximum) return false;
            if (current.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var property in current.Value.EnumerateObject()) pending.Push((property.Value, current.Depth + 1));
            else if (current.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var item in current.Value.EnumerateArray()) pending.Push((item, current.Depth + 1));
        }

        return true;
    }
    private static IEnumerable<string> Names(System.Text.Json.JsonElement value)
    {
        if (value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in Names(property.Value)) yield return nested;
            }
        }
        else if (value.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) foreach (var nested in Names(item)) yield return nested;
        }
    }
}
