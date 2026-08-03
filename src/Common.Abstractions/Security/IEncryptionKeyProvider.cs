// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves active and historical encryption keys by identifier.
/// </summary>
/// <example>
/// <code>
/// var active = await provider.GetActiveKeyAsync(cancellationToken);
/// var historical = await provider.GetKeyAsync("2026-07", cancellationToken);
/// </code>
/// </example>
public interface IEncryptionKeyProvider
{
    /// <summary>
    /// Gets the key used for new encryption operations.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel resolution.</param>
    /// <returns>The active key, or null when no active key is available.</returns>
    /// <example>
    /// <code>
    /// var key = await provider.GetActiveKeyAsync(cancellationToken);
    /// </code>
    /// </example>
    ValueTask<EncryptionKeyMaterial> GetActiveKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a key by its persisted identifier.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="cancellationToken">The token used to cancel resolution.</param>
    /// <returns>The matching key, or null when it is unavailable.</returns>
    /// <example>
    /// <code>
    /// var key = await provider.GetKeyAsync("2026-07", cancellationToken);
    /// </code>
    /// </example>
    ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes identified encryption key material.
/// </summary>
/// <example>
/// <code>
/// var material = new EncryptionKeyMaterial("primary", keyBytes);
/// </code>
/// </example>
public sealed class EncryptionKeyMaterial
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionKeyMaterial" /> class.
    /// </summary>
    /// <param name="keyId">The stable key identifier.</param>
    /// <param name="key">The key bytes.</param>
    /// <example>
    /// <code>
    /// var material = new EncryptionKeyMaterial("primary", keyBytes);
    /// </code>
    /// </example>
    public EncryptionKeyMaterial(string keyId, ReadOnlyMemory<byte> key)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("Encryption key identifier must not be null or whitespace.", nameof(keyId));
        }

        if (key.IsEmpty)
        {
            throw new ArgumentException("Encryption key material must not be empty.", nameof(key));
        }

        this.KeyId = keyId;
        this.Key = key.ToArray();
    }

    /// <summary>
    /// Gets the stable key identifier.
    /// </summary>
    /// <example>
    /// <code>
    /// var id = material.KeyId;
    /// </code>
    /// </example>
    public string KeyId { get; }

    /// <summary>
    /// Gets a copy of the key material.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = material.Key;
    /// </code>
    /// </example>
    public ReadOnlyMemory<byte> Key { get; }
}
