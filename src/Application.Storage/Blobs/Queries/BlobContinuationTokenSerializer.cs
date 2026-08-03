// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Serializes and deserializes opaque blob-store continuation tokens.
/// </summary>
/// <example>
/// <code>
/// var serialized = BlobContinuationTokenSerializer.Serialize(token);
/// var deserialized = BlobContinuationTokenSerializer.Deserialize(serialized.Value);
/// </code>
/// </example>
public static class BlobContinuationTokenSerializer
{
    private const string Purpose = "blob-storage";

    /// <summary>
    /// Serializes a continuation token envelope to an opaque string.
    /// </summary>
    /// <param name="token">The continuation token envelope to serialize.</param>
    /// <returns>A result containing the opaque continuation token string.</returns>
    /// <example>
    /// <code>
    /// var result = BlobContinuationTokenSerializer.Serialize(token);
    /// </code>
    /// </example>
    public static Result<string> Serialize(
        BlobContinuationToken token,
        IContinuationTokenProtector protector = null)
    {
        if (token is null)
        {
            return Result<string>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(token.Provider) ||
            string.IsNullOrWhiteSpace(token.QueryHash))
        {
            return Result<string>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token is missing required envelope values."));
        }

        return Result<string>.Success(OpaqueContinuationTokenCodec.Serialize(token, Purpose, protector));
    }

    /// <summary>
    /// Deserializes an opaque continuation token.
    /// </summary>
    /// <param name="token">The opaque continuation token string to deserialize.</param>
    /// <returns>A result containing the continuation token envelope.</returns>
    /// <example>
    /// <code>
    /// var result = BlobContinuationTokenSerializer.Deserialize(page.ContinuationToken);
    /// </code>
    /// </example>
    public static Result<BlobContinuationToken> Deserialize(
        string token,
        IContinuationTokenProtector protector = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<BlobContinuationToken>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token must not be null or whitespace."));
        }

        try
        {
            var envelope = OpaqueContinuationTokenCodec.Deserialize<BlobContinuationToken>(token, Purpose, protector);
            if (envelope is null || envelope.Version != 1)
            {
                return Result<BlobContinuationToken>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token version is not supported."));
            }

            if (string.IsNullOrWhiteSpace(envelope.Provider) ||
                string.IsNullOrWhiteSpace(envelope.QueryHash))
            {
                return Result<BlobContinuationToken>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token envelope is invalid."));
            }

            return Result<BlobContinuationToken>.Success(envelope);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return Result<BlobContinuationToken>.Failure(new BlobStoreInvalidContinuationTokenError("Continuation token is invalid.", ex));
        }
    }

}
