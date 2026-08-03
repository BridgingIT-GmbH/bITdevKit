// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Encodes versioned opaque continuation-token payloads with optional tamper protection.
/// </summary>
/// <example>
/// <code>
/// var token = OpaqueContinuationTokenCodec.Serialize(payload, "blob-storage", protector);
/// var restored = OpaqueContinuationTokenCodec.Deserialize&lt;Payload&gt;(token, "blob-storage", protector);
/// </code>
/// </example>
public static class OpaqueContinuationTokenCodec
{
    private const string UnprotectedPrefix = "u1.";
    private const string ProtectedPrefix = "p1.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes a payload into an opaque token.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="payload">The payload.</param>
    /// <param name="purpose">The feature purpose.</param>
    /// <param name="protector">The optional tamper protector.</param>
    /// <returns>The opaque token.</returns>
    /// <example>
    /// <code>
    /// var token = OpaqueContinuationTokenCodec.Serialize(payload, "blob-storage");
    /// </code>
    /// </example>
    public static string Serialize<T>(T payload, string purpose, IContinuationTokenProtector protector = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidatePurpose(purpose);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        return protector is null
            ? UnprotectedPrefix + Base64UrlHelper.Encode(bytes)
            : ProtectedPrefix + Base64UrlHelper.Encode(protector.Protect(purpose, bytes));
    }

    /// <summary>
    /// Deserializes and validates an opaque token.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="token">The opaque token.</param>
    /// <param name="purpose">The feature purpose.</param>
    /// <param name="protector">The optional tamper protector.</param>
    /// <returns>The decoded payload.</returns>
    /// <exception cref="FormatException">The token is malformed or does not satisfy the configured protection mode.</exception>
    /// <example>
    /// <code>
    /// var payload = OpaqueContinuationTokenCodec.Deserialize&lt;Payload&gt;(token, "blob-storage");
    /// </code>
    /// </example>
    public static T Deserialize<T>(string token, string purpose, IContinuationTokenProtector protector = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new FormatException("Continuation token must not be null or whitespace.");
        }

        ValidatePurpose(purpose);
        byte[] payload;
        try
        {
            if (token.StartsWith(UnprotectedPrefix, StringComparison.Ordinal))
            {
                if (protector is not null)
                {
                    throw new FormatException("Unprotected continuation tokens are not accepted when protection is configured.");
                }

                payload = Base64UrlHelper.Decode(token[UnprotectedPrefix.Length..]);
            }
            else if (token.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            {
                if (protector is null)
                {
                    throw new FormatException("A continuation-token protector is required for this token.");
                }

                var protectedPayload = Base64UrlHelper.Decode(token[ProtectedPrefix.Length..]);
                if (!protector.TryUnprotect(purpose, protectedPayload, out payload))
                {
                    throw new FormatException("Continuation-token protection validation failed.");
                }
            }
            else
            {
                throw new FormatException("Continuation-token format is not supported.");
            }

            return JsonSerializer.Deserialize<T>(payload, JsonOptions)
                ?? throw new FormatException("Continuation-token payload is empty.");
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            throw new FormatException("Continuation token is invalid.", exception);
        }
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Continuation-token purpose must not be null or whitespace.", nameof(purpose));
        }
    }

}
