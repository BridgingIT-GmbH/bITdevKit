// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Encodes and decodes canonical unpadded Base64Url values.
/// </summary>
/// <example>
/// <code>
/// var encoded = Base64UrlHelper.Encode("payload"u8);
/// var decoded = Base64UrlHelper.Decode(encoded);
/// </code>
/// </example>
public static class Base64UrlHelper
{
    /// <summary>
    /// Encodes bytes as canonical unpadded Base64Url text.
    /// </summary>
    /// <param name="value">The bytes to encode.</param>
    /// <returns>The canonical unpadded Base64Url value.</returns>
    /// <example>
    /// <code>
    /// var encoded = Base64UrlHelper.Encode("payload"u8);
    /// </code>
    /// </example>
    public static string Encode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Decodes canonical unpadded Base64Url text.
    /// </summary>
    /// <param name="value">The canonical unpadded Base64Url value.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    /// <exception cref="FormatException">The value is malformed, padded, or not canonical Base64Url.</exception>
    /// <example>
    /// <code>
    /// var decoded = Base64UrlHelper.Decode("cGF5bG9hZA");
    /// </code>
    /// </example>
    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        var bytes = Convert.FromBase64String(base64);
        if (!string.Equals(Encode(bytes), value, StringComparison.Ordinal))
        {
            throw new FormatException("Value is not canonical unpadded Base64Url.");
        }

        return bytes;
    }
}
