// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Globalization;
using System.Text;

/// <summary>Produces bounded, culture-independent names and keys for all cache providers.</summary>
/// <example><code>var key = CacheIdentityNormalizer.Key("Product:ABC");</code></example>
public static class CacheIdentityNormalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Validates and canonicalizes an opaque exact key without interpreting wildcards.</summary>
    public static string Key(string key) => Normalize(key, 4096, nameof(key));

    /// <summary>Validates and canonicalizes a namespace.</summary>
    public static string Namespace(string value) => Normalize(value, 256, nameof(value));

    /// <summary>Validates and canonicalizes a collection or dimension name.</summary>
    public static string Name(string value) => Normalize(value, 128, nameof(value));

    /// <summary>Validates and canonicalizes a stable contract version.</summary>
    public static string ContractVersion(string value) => Normalize(value, 128, nameof(value));

    /// <summary>Canonicalizes a portable string dimension, allowing an explicit empty string.</summary>
    public static string DimensionString(string value) => Normalize(value, 4096, nameof(value), true);

    /// <summary>Formats a key component without culture-dependent numbers or local timestamps.</summary>
    public static string Component(object value) => value switch
    {
        null => throw new ArgumentNullException(nameof(value)),
        DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTime timestamp when timestamp.Kind == DateTimeKind.Utc => timestamp.ToString("O", CultureInfo.InvariantCulture),
        DateTime => throw new ArgumentException("DateTime key components must be UTC.", nameof(value)),
        IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
        string text => text,
        _ => throw new ArgumentException("Key components must be strings or invariantly formattable values.", nameof(value))
    };

    private static string Normalize(string value, int maximumBytes, string parameter, bool allowEmpty = false)
    {
        if (value is null || value.Length > maximumBytes || (!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            StrictUtf8.GetByteCount(value) > maximumBytes || value.Contains('\0'))
        {
            throw new ArgumentException($"A nonempty identifier of at most {maximumBytes} UTF-8 bytes is required.", parameter);
        }

        var canonical = value.ToUpperInvariant();
        if (StrictUtf8.GetByteCount(canonical) > maximumBytes)
        {
            throw new ArgumentException("The normalized identifier exceeds its UTF-8 limit.", parameter);
        }

        return canonical;
    }
}