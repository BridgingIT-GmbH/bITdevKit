// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>An immutable projected JSON value with an explicit policy marker.</summary>
/// <example><code>scope.SetDetail("reason", AuditValue.From("Customer correction"));</code></example>
public sealed record AuditValue
{
    /// <summary>Creates a copied value or policy marker.</summary>
    /// <exception cref="ArgumentException">The marker is unsupported or a supplied hash is not a hexadecimal SHA-256 digest.</exception>
    /// <example><code>var value = new AuditValue(JsonSerializer.SerializeToElement(3));</code></example>
    [JsonConstructor]
    public AuditValue(JsonElement data, string marker = null)
    {
        this.Data = data.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement((string)null) : data.Clone();
        if (marker is not null && marker is not ("Redacted" or "Excluded" or "Hashed" or "Oversized" or "Unavailable"))
        {
            throw new ArgumentException("Unsupported audit value marker.", nameof(marker));
        }

        this.Marker = marker;
        if (marker == "Hashed" && this.Data.ValueKind != JsonValueKind.Null &&
            (this.Data.ValueKind != JsonValueKind.String || this.Data.GetString() is not { Length: 64 } digest ||
             digest.Any(character => !char.IsAsciiHexDigit(character))))
        {
            throw new ArgumentException("An audit hash must be a SHA-256 hexadecimal digest or an omitted digest.", nameof(data));
        }
    }

    /// <summary>Copied JSON data; a marker can distinguish hidden values from JSON null.</summary>
    /// <example><code>var json = value.Data.GetRawText();</code></example>
    public JsonElement Data { get; }

    /// <summary>The policy marker, or null for an ordinary projected value.</summary>
    /// <example><code>var wasRedacted = value.Marker == "Redacted";</code></example>
    public string Marker { get; }

    /// <summary>Copies projected JSON without retaining its document lease.</summary>
    /// <example><code>var value = AuditValue.Json(document.RootElement);</code></example>
    public static AuditValue Json(JsonElement value) => new(value);

    /// <summary>Captures text or an actual null.</summary>
    /// <example><code>var value = AuditValue.From("Approved");</code></example>
    public static AuditValue From(string value) => new(JsonSerializer.SerializeToElement(value));

    /// <summary>Captures an integer.</summary>
    /// <example><code>var value = AuditValue.From(3L);</code></example>
    public static AuditValue From(long value) => new(JsonSerializer.SerializeToElement(value));

    /// <summary>Captures a decimal with invariant JSON encoding.</summary>
    /// <example><code>var value = AuditValue.From(12.5m);</code></example>
    public static AuditValue From(decimal value) => new(JsonSerializer.SerializeToElement(value));

    /// <summary>Captures a finite floating point number.</summary>
    /// <example><code>var value = AuditValue.From(1.5d);</code></example>
    public static AuditValue From(double value) => double.IsFinite(value) ? new(JsonSerializer.SerializeToElement(value)) : throw new ArgumentOutOfRangeException(nameof(value));

    /// <summary>Captures a boolean.</summary>
    /// <example><code>var value = AuditValue.From(true);</code></example>
    public static AuditValue From(bool value) => new(JsonSerializer.SerializeToElement(value));

    /// <summary>Captures a timestamp normalized to UTC.</summary>
    /// <example><code>var value = AuditValue.From(DateTimeOffset.UtcNow);</code></example>
    public static AuditValue From(DateTimeOffset value) => From(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Copies an explicitly projected string array.</summary>
    /// <example><code>var value = AuditValue.From(new[] { "accepted" });</code></example>
    public static AuditValue From(string[] values) => new(JsonSerializer.SerializeToElement(values?.ToArray()));

    /// <summary>Creates a policy marker, optionally retaining a 64-character hexadecimal SHA-256 digest.</summary>
    /// <exception cref="ArgumentException">The marker or digest is invalid.</exception>
    /// <example><code>var hidden = AuditValue.Policy("Redacted");</code></example>
    public static AuditValue Policy(string marker, string digest = null) => new(JsonSerializer.SerializeToElement(digest), marker);
}
