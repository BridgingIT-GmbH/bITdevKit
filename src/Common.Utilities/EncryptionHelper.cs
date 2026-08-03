// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>
/// Provides AES encryption helpers for stream-first workflows.
/// </summary>
/// <example>
/// <code>
/// var key = EncryptionHelper.GenerateAesKey();
/// var iv = EncryptionHelper.GenerateAesCbcInitializationVector();
/// await using var encryptor = EncryptionHelper.CreateAesCbcEncryptionStream(target, key, iv);
/// await source.CopyToAsync(encryptor, cancellationToken);
/// </code>
/// </example>
public static class EncryptionHelper
{
    /// <summary>
    /// Gets the algorithm label used by the AES-CBC helper methods.
    /// </summary>
    /// <example>
    /// <code>
    /// var algorithm = EncryptionHelper.AesCbcPkcs7Algorithm;
    /// </code>
    /// </example>
    public const string AesCbcPkcs7Algorithm = "aes-cbc-pkcs7";

    /// <summary>
    /// Gets the AES block size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var iv = RandomNumberGenerator.GetBytes(EncryptionHelper.AesBlockSizeBytes);
    /// </code>
    /// </example>
    public const int AesBlockSizeBytes = 16;

    /// <summary>
    /// Gets the default AES-256 key size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = EncryptionHelper.GenerateAesKey(EncryptionHelper.Aes256KeySizeBytes);
    /// </code>
    /// </example>
    public const int Aes256KeySizeBytes = 32;

    /// <summary>
    /// Encrypts a UTF-8 string and returns a Base64 encoded payload containing the initialization vector and ciphertext.
    /// </summary>
    /// <param name="source">The string to encrypt.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="cancellationToken">The cancellation token used while copying content.</param>
    /// <returns>A Base64 encoded encrypted payload, or null when <paramref name="source" /> is null.</returns>
    /// <example>
    /// <code>
    /// var encrypted = await EncryptionHelper.EncryptAsync("secret", key, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> EncryptAsync(
        string source,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return null;
        }

        var encrypted = await EncryptAsync(Encoding.UTF8.GetBytes(source), key, cancellationToken).AnyContext();

        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a Base64 encoded payload created by <see cref="EncryptAsync(string, byte[], CancellationToken)" />.
    /// </summary>
    /// <param name="source">The Base64 encoded encrypted payload.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="cancellationToken">The cancellation token used while copying content.</param>
    /// <returns>The decrypted UTF-8 string, or null when <paramref name="source" /> is null.</returns>
    /// <example>
    /// <code>
    /// var text = await EncryptionHelper.DecryptAsync(encrypted, key, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> DecryptAsync(
        string source,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return null;
        }

        var decrypted = await DecryptAsync(Convert.FromBase64String(source), key, cancellationToken).AnyContext();

        return Encoding.UTF8.GetString(decrypted);
    }

    /// <summary>
    /// Encrypts bytes and returns a payload containing the initialization vector followed by ciphertext.
    /// </summary>
    /// <param name="source">The bytes to encrypt.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="cancellationToken">The cancellation token used while copying content.</param>
    /// <returns>The encrypted payload, or null when <paramref name="source" /> is null.</returns>
    /// <example>
    /// <code>
    /// var encrypted = await EncryptionHelper.EncryptAsync(bytes, key, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<byte[]> EncryptAsync(
        byte[] source,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return null;
        }

        var initializationVector = GenerateAesCbcInitializationVector();
        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await destinationStream.WriteAsync(initializationVector, cancellationToken).AnyContext();

        await using (var encryptor = CreateAesCbcEncryptionStream(
            destinationStream,
            key,
            initializationVector,
            leaveOpen: true))
        {
            await sourceStream.CopyToAsync(encryptor, cancellationToken).AnyContext();
            encryptor.FlushFinalBlock();
        }

        return destinationStream.ToArray();
    }

    /// <summary>
    /// Decrypts a payload containing the initialization vector followed by ciphertext.
    /// </summary>
    /// <param name="source">The encrypted payload created by <see cref="EncryptAsync(byte[], byte[], CancellationToken)" />.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="cancellationToken">The cancellation token used while copying content.</param>
    /// <returns>The decrypted bytes, or null when <paramref name="source" /> is null.</returns>
    /// <example>
    /// <code>
    /// var bytes = await EncryptionHelper.DecryptAsync(encrypted, key, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<byte[]> DecryptAsync(
        byte[] source,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return null;
        }

        if (source.Length <= AesBlockSizeBytes)
        {
            throw new InvalidDataException("Encrypted payload must contain an initialization vector followed by ciphertext.");
        }

        var initializationVector = source[..AesBlockSizeBytes];
        using var sourceStream = new MemoryStream(source, AesBlockSizeBytes, source.Length - AesBlockSizeBytes, writable: false);
        using var destinationStream = new MemoryStream();

        await using (var decryptor = CreateAesCbcDecryptionStream(
            sourceStream,
            key,
            initializationVector,
            leaveOpen: true))
        {
            await decryptor.CopyToAsync(destinationStream, cancellationToken).AnyContext();
        }

        return destinationStream.ToArray();
    }

    /// <summary>
    /// Creates a writable AES-CBC encryption stream.
    /// </summary>
    /// <param name="destination">The destination stream that receives encrypted bytes.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="initializationVector">The 16-byte initialization vector.</param>
    /// <param name="leaveOpen">True to leave the destination stream open when the crypto stream is disposed.</param>
    /// <returns>A writable encryption stream.</returns>
    /// <example>
    /// <code>
    /// await using var crypto = EncryptionHelper.CreateAesCbcEncryptionStream(destination, key, iv);
    /// </code>
    /// </example>
    public static CryptoStream CreateAesCbcEncryptionStream(
        Stream destination,
        byte[] key,
        byte[] initializationVector,
        bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));
        EnsureValidAesKey(key);
        EnsureValidAesInitializationVector(initializationVector);

        using var aes = CreateAesCbc(key, initializationVector);
        return new CryptoStream(destination, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen);
    }

    /// <summary>
    /// Creates a readable AES-CBC decryption stream.
    /// </summary>
    /// <param name="source">The source stream containing encrypted bytes.</param>
    /// <param name="key">The AES key. Supported sizes are 16, 24, and 32 bytes.</param>
    /// <param name="initializationVector">The 16-byte initialization vector.</param>
    /// <param name="leaveOpen">True to leave the source stream open when the crypto stream is disposed.</param>
    /// <returns>A readable decryption stream.</returns>
    /// <example>
    /// <code>
    /// await using var crypto = EncryptionHelper.CreateAesCbcDecryptionStream(source, key, iv);
    /// </code>
    /// </example>
    public static CryptoStream CreateAesCbcDecryptionStream(
        Stream source,
        byte[] key,
        byte[] initializationVector,
        bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(source, nameof(source));
        EnsureValidAesKey(key);
        EnsureValidAesInitializationVector(initializationVector);

        using var aes = CreateAesCbc(key, initializationVector);
        return new CryptoStream(source, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen);
    }

    /// <summary>
    /// Generates a random AES key.
    /// </summary>
    /// <param name="keySizeBytes">The key size in bytes. Supported sizes are 16, 24, and 32.</param>
    /// <returns>The generated key bytes.</returns>
    /// <example>
    /// <code>
    /// var key = EncryptionHelper.GenerateAesKey();
    /// </code>
    /// </example>
    public static byte[] GenerateAesKey(int keySizeBytes = Aes256KeySizeBytes)
    {
        if (!IsValidAesKeySize(keySizeBytes))
        {
            throw new ArgumentOutOfRangeException(nameof(keySizeBytes), "AES key size must be 16, 24, or 32 bytes.");
        }

        return RandomNumberGenerator.GetBytes(keySizeBytes);
    }

    /// <summary>
    /// Generates a random AES-CBC initialization vector.
    /// </summary>
    /// <returns>The generated 16-byte initialization vector.</returns>
    /// <example>
    /// <code>
    /// var iv = EncryptionHelper.GenerateAesCbcInitializationVector();
    /// </code>
    /// </example>
    public static byte[] GenerateAesCbcInitializationVector() =>
        RandomNumberGenerator.GetBytes(AesBlockSizeBytes);

    /// <summary>
    /// Determines whether the supplied key is valid for AES.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <returns><c>true</c> when the key size is 16, 24, or 32 bytes.</returns>
    /// <example>
    /// <code>
    /// var valid = EncryptionHelper.IsValidAesKey(key);
    /// </code>
    /// </example>
    public static bool IsValidAesKey(byte[] key) => key is not null && IsValidAesKeySize(key.Length);

    /// <summary>
    /// Determines whether the supplied key size is valid for AES.
    /// </summary>
    /// <param name="keySizeBytes">The key size in bytes.</param>
    /// <returns><c>true</c> when the key size is 16, 24, or 32 bytes.</returns>
    /// <example>
    /// <code>
    /// var valid = EncryptionHelper.IsValidAesKeySize(32);
    /// </code>
    /// </example>
    public static bool IsValidAesKeySize(int keySizeBytes) => keySizeBytes is 16 or 24 or 32;

    /// <summary>
    /// Determines whether the supplied initialization vector is valid for AES-CBC.
    /// </summary>
    /// <param name="initializationVector">The initialization vector to validate.</param>
    /// <returns><c>true</c> when the vector is 16 bytes.</returns>
    /// <example>
    /// <code>
    /// var valid = EncryptionHelper.IsValidAesInitializationVector(iv);
    /// </code>
    /// </example>
    public static bool IsValidAesInitializationVector(byte[] initializationVector) =>
        initializationVector is { Length: AesBlockSizeBytes };

    private static Aes CreateAesCbc(byte[] key, byte[] initializationVector)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = initializationVector;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        return aes;
    }

    private static void EnsureValidAesKey(byte[] key)
    {
        if (!IsValidAesKey(key))
        {
            throw new ArgumentException("AES key must be 16, 24, or 32 bytes.", nameof(key));
        }
    }

    private static void EnsureValidAesInitializationVector(byte[] initializationVector)
    {
        if (!IsValidAesInitializationVector(initializationVector))
        {
            throw new ArgumentException("AES-CBC initialization vector must be 16 bytes.", nameof(initializationVector));
        }
    }
}
