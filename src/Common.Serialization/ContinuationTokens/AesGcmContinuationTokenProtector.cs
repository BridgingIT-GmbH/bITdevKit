// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;
using System.Text;

/// <summary>Encrypts and authenticates bounded continuation payloads without exposing their contents.</summary>
/// <remarks>Use a stable secret shared by participating nodes and a fixed feature-specific purpose. Static operations never retain or modify caller inputs.</remarks>
/// <example><code>
/// var token = AesGcmContinuationTokenProtector.Protect(cursorBytes, secret, "catalog.cursor.v1");
/// var restored = AesGcmContinuationTokenProtector.Unprotect(token, secret, "catalog.cursor.v1");
/// </code></example>
public sealed class AesGcmContinuationTokenProtector : IContinuationTokenProtector
{
    /// <summary>The required caller-owned AES-256 key length in bytes.</summary>
    public const int KeySize = 32;

    /// <summary>The greatest accepted plaintext continuation length, in bytes.</summary>
    public const int MaximumPlaintextBytes = 16 * 1024;

    /// <summary>The greatest protected continuation length, including format, nonce, and authentication tag.</summary>
    public const int MaximumProtectedBytes = MaximumPlaintextBytes + 29;

    private const byte Format = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 1 + NonceSize + TagSize;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private readonly byte[] key;

    /// <summary>Creates a purpose-bound token protector with a defensive copy of an AES-256 secret.</summary>
    /// <param name="key">Exactly 32 secret bytes, shared by all readers and writers of the protected tokens.</param>
    /// <example><code>IContinuationTokenProtector protector = new AesGcmContinuationTokenProtector(secret);</code></example>
    public AesGcmContinuationTokenProtector(ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        this.key = key.ToArray();
    }

    /// <inheritdoc />
    /// <example><code>var protectedPayload = protector.Protect("catalog.cursor.v1", payload);</code></example>
    public byte[] Protect(string purpose, ReadOnlySpan<byte> payload) => Protect(payload, this.key, purpose);

    /// <inheritdoc />
    /// <remarks>Malformed or unauthenticated token bytes return false. Invalid purpose configuration throws an argument exception.</remarks>
    /// <example><code>if (protector.TryUnprotect("catalog.cursor.v1", token, out var payload)) { ReadCursor(payload); }</code></example>
    public bool TryUnprotect(string purpose, ReadOnlySpan<byte> protectedPayload, out byte[] payload)
    {
        Span<byte> associatedData = stackalloc byte[128];
        _ = WritePurpose(purpose, associatedData);
        payload = null;
        try
        {
            payload = Unprotect(protectedPayload, this.key, purpose);
            return true;
        }
        catch (InvalidDataException) { return false; }
        catch (CryptographicException) { return false; }
    }

    /// <summary>Produces randomized authenticated ciphertext for one bounded continuation.</summary>
    /// <param name="plaintext">The complete bounded cursor representation.</param>
    /// <param name="storeSecret">Exactly 32 caller-owned secret bytes, shared by the store's participating nodes.</param>
    /// <param name="purpose">A fixed code-owned feature purpose of at most 128 UTF-8 bytes.</param>
    /// <returns>Format, random nonce, authentication tag, and encrypted continuation bytes.</returns>
    /// <exception cref="ArgumentException">The secret, purpose, or plaintext bound is invalid.</exception>
    /// <example><code>var token = AesGcmContinuationTokenProtector.Protect(cursorBytes, secret, "catalog.cursor.v1");</code></example>
    public static byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> storeSecret, string purpose)
    {
        ValidateKey(storeSecret);
        if (plaintext.Length > MaximumPlaintextBytes) { throw new ArgumentException("The continuation exceeds its plaintext bound.", nameof(plaintext)); }

        Span<byte> associatedData = stackalloc byte[128];
        var purposeLength = WritePurpose(purpose, associatedData);
        var result = new byte[HeaderSize + plaintext.Length];
        result[0] = Format;
        var nonce = result.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var cipher = new AesGcm(storeSecret, TagSize);
        cipher.Encrypt(nonce, plaintext, result.AsSpan(HeaderSize), result.AsSpan(1 + NonceSize, TagSize), associatedData[..purposeLength]);
        return result;
    }

    /// <summary>Authenticates and decrypts one bounded continuation before any progress fields are interpreted.</summary>
    /// <param name="protectedData">The complete protected format/nonce/tag/ciphertext bytes.</param>
    /// <param name="storeSecret">The same 32 caller-owned secret bytes used for protection.</param>
    /// <param name="purpose">The same fixed code-owned feature purpose used for protection.</param>
    /// <returns>A newly owned plaintext buffer, which callers may clear after interpreting the cursor.</returns>
    /// <exception cref="ArgumentException">The configured secret or purpose is invalid.</exception>
    /// <exception cref="InvalidDataException">The protected format or size is invalid.</exception>
    /// <exception cref="CryptographicException">The secret, purpose, nonce, ciphertext, or authentication tag does not match.</exception>
    /// <example><code>var cursorBytes = AesGcmContinuationTokenProtector.Unprotect(token, secret, "catalog.cursor.v1");</code></example>
    public static byte[] Unprotect(ReadOnlySpan<byte> protectedData, ReadOnlySpan<byte> storeSecret, string purpose)
    {
        ValidateKey(storeSecret);
        if (protectedData.Length < HeaderSize || protectedData.Length > MaximumProtectedBytes || protectedData[0] != Format)
        {
            throw new InvalidDataException("The protected continuation format or size is invalid.");
        }

        Span<byte> associatedData = stackalloc byte[128];
        var purposeLength = WritePurpose(purpose, associatedData);
        var result = new byte[protectedData.Length - HeaderSize];
        using var cipher = new AesGcm(storeSecret, TagSize);
        try
        {
            cipher.Decrypt(protectedData.Slice(1, NonceSize), protectedData[HeaderSize..], protectedData.Slice(1 + NonceSize, TagSize), result,
                associatedData[..purposeLength]);
            return result;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(result);
            throw;
        }
    }

    private static void ValidateKey(ReadOnlySpan<byte> storeSecret)
    {
        if (storeSecret.Length != KeySize) { throw new ArgumentException("A 32-byte caller-owned continuation secret is required.", nameof(storeSecret)); }
    }

    private static int WritePurpose(string purpose, Span<byte> destination)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 128 || Utf8.GetByteCount(purpose) > destination.Length)
        {
            throw new ArgumentException("A fixed nonempty feature purpose of at most 128 UTF-8 bytes is required.", nameof(purpose));
        }

        return Utf8.GetBytes(purpose, destination);
    }
}