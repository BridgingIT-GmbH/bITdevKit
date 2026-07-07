// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;
using System.Text.Json;

/// <summary>
/// Serializes and deserializes opaque document-store continuation tokens.
/// </summary>
/// <example>
/// <code>
/// var serialized = DocumentContinuationTokenSerializer.Serialize(token);
/// var deserialized = DocumentContinuationTokenSerializer.Deserialize(serialized.Value);
/// </code>
/// </example>
public static class DocumentContinuationTokenSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes a continuation token envelope to an opaque string.
    /// </summary>
    /// <param name="token">The continuation token envelope to serialize.</param>
    /// <returns>A result containing the opaque continuation token string.</returns>
    /// <example>
    /// <code>
    /// var result = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
    /// {
    ///     Provider = "cosmos",
    ///     QueryHash = queryHash,
    ///     NativeToken = nativeToken
    /// });
    /// </code>
    /// </example>
    public static Result<string> Serialize(DocumentContinuationToken token)
    {
        if (token is null)
        {
            return Result<string>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(token.Provider) ||
            string.IsNullOrWhiteSpace(token.QueryHash))
        {
            return Result<string>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token is missing required envelope values."));
        }

        var json = JsonSerializer.Serialize(token, JsonOptions);
        return Result<string>.Success(ToBase64Url(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>
    /// Deserializes an opaque continuation token.
    /// </summary>
    /// <param name="token">The opaque continuation token string to deserialize.</param>
    /// <returns>A result containing the continuation token envelope.</returns>
    /// <example>
    /// <code>
    /// var result = DocumentContinuationTokenSerializer.Deserialize(page.Value.ContinuationToken);
    /// </code>
    /// </example>
    public static Result<DocumentContinuationToken> Deserialize(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<DocumentContinuationToken>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token must not be null or whitespace."));
        }

        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(token));
            var envelope = JsonSerializer.Deserialize<DocumentContinuationToken>(json, JsonOptions);
            if (envelope is null || envelope.Version != 1)
            {
                return Result<DocumentContinuationToken>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token version is not supported."));
            }

            if (string.IsNullOrWhiteSpace(envelope.Provider) ||
                string.IsNullOrWhiteSpace(envelope.QueryHash))
            {
                return Result<DocumentContinuationToken>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token envelope is invalid."));
            }

            return Result<DocumentContinuationToken>.Success(envelope);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return Result<DocumentContinuationToken>.Failure(new DocumentStoreInvalidContinuationTokenError("Continuation token is invalid.", ex));
        }
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');

        return Convert.FromBase64String(base64);
    }
}
