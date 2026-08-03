// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Protects continuation tokens with purpose-bound HMAC-SHA256 signatures.
/// </summary>
/// <example>
/// <code>
/// var protector = new HmacContinuationTokenProtector(RandomNumberGenerator.GetBytes(32));
/// </code>
/// </example>
public sealed class HmacContinuationTokenProtector : IContinuationTokenProtector
{
    private const int SignatureLength = 32;
    private readonly byte[] key;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacContinuationTokenProtector" /> class.
    /// </summary>
    /// <param name="key">A signing key of at least 32 bytes.</param>
    /// <example>
    /// <code>
    /// var protector = new HmacContinuationTokenProtector(signingKey);
    /// </code>
    /// </example>
    public HmacContinuationTokenProtector(ReadOnlySpan<byte> key)
    {
        if (key.Length < SignatureLength)
        {
            throw new ArgumentException("Continuation-token signing key must contain at least 32 bytes.", nameof(key));
        }

        this.key = key.ToArray();
    }

    /// <inheritdoc />
    public byte[] Protect(string purpose, ReadOnlySpan<byte> payload)
    {
        ValidatePurpose(purpose);
        var signature = this.ComputeSignature(purpose, payload);
        var result = new byte[payload.Length + signature.Length];
        payload.CopyTo(result);
        signature.CopyTo(result.AsSpan(payload.Length));
        return result;
    }

    /// <inheritdoc />
    public bool TryUnprotect(string purpose, ReadOnlySpan<byte> protectedPayload, out byte[] payload)
    {
        ValidatePurpose(purpose);
        payload = null;
        if (protectedPayload.Length <= SignatureLength)
        {
            return false;
        }

        var content = protectedPayload[..^SignatureLength];
        var signature = protectedPayload[^SignatureLength..];
        var expected = this.ComputeSignature(purpose, content);
        if (!CryptographicOperations.FixedTimeEquals(signature, expected))
        {
            return false;
        }

        payload = content.ToArray();
        return true;
    }

    private byte[] ComputeSignature(string purpose, ReadOnlySpan<byte> payload)
    {
        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var input = new byte[purposeBytes.Length + 1 + payload.Length];
        purposeBytes.CopyTo(input, 0);
        payload.CopyTo(input.AsSpan(purposeBytes.Length + 1));
        return HMACSHA256.HashData(this.key, input);
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Continuation-token purpose must not be null or whitespace.", nameof(purpose));
        }
    }
}
