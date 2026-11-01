// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>A bounded business type and stable identifier, independent of live entity lookup.</summary>
/// <example><code>var resource = new AuditResourceReference("Customer", customerId.ToString());</code></example>
public sealed record AuditResourceReference
{
    /// <summary>Validates a reference without serializing a business object.</summary>
    /// <example><code>var resource = new AuditResourceReference("Customer", "C-123");</code></example>
    public AuditResourceReference(string type, string id, string displayName = null, Type contextType = null)
    {
        this.Type = AuditStructuralValidation.Required(type, 512, nameof(type));
        this.Id = AuditStructuralValidation.Required(id, 256, nameof(id));
        this.DisplayName = displayName;
        this.ContextType = contextType;
    }

    /// <summary>The resource type alias or qualified name.</summary>
    /// <example><code>var type = resource.Type;</code></example>
    public string Type { get; }

    /// <summary>The exact ordinal resource identifier.</summary>
    /// <example><code>var id = resource.Id;</code></example>
    public string Id { get; }

    /// <summary>Optional explicitly approved display text.</summary>
    /// <example><code>var label = resource.DisplayName;</code></example>
    public string DisplayName { get; }

    /// <summary>Optional derived type context, resolved and discarded before freezing.</summary>
    /// <example><code>var type = resource.ContextType;</code></example>
    [JsonIgnore]
    public Type ContextType { get; }
}

/// <summary>Shares structural checks for audit keys, names and resource identities.</summary>
/// <example><code>var key = AuditStructuralValidation.Required("customers.rename", 128, "key");</code></example>
public static class AuditStructuralValidation
{
    private static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase)
    { "AuditId", "ActionId", "Actor", "UserId", "Outcome", "CorrelationId", "OriginAuditId", "CausationAuditId", "SchemaVersion", "IntegrityProtection", "ContentHash" };

    /// <summary>Validates bounded nonempty Unicode metadata without echoing supplied values.</summary>
    /// <example><code>var key = AuditStructuralValidation.Required("customers.rename", 128, "key");</code></example>
    public static string Required(string value, int length, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > length || value.Any(char.IsControl))
        {
            throw new ArgumentException("Audit metadata has an invalid structure.", name);
        }

        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (System.Text.Rune.DecodeFromUtf16(remaining, out _, out var consumed) != System.Buffers.OperationStatus.Done)
                throw new ArgumentException("Audit metadata has invalid Unicode.", name);
            remaining = remaining[consumed..];
        }

        return value;
    }

    /// <summary>Rejects invalid or reserved detail names.</summary>
    /// <example><code>var name = AuditStructuralValidation.DetailKey("count");</code></example>
    public static string DetailKey(string value)
    {
        Required(value, 128, nameof(value));
        return reserved.Contains(value) ? throw new ArgumentException("Reserved audit detail key.", nameof(value)) : value;
    }
}
