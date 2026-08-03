// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves encryption keys from an immutable in-memory dictionary.
/// </summary>
/// <example>
/// <code>
/// var provider = new DictionaryEncryptionKeyProvider(
///     "primary",
///     new Dictionary&lt;string, byte[]&gt; { ["primary"] = keyBytes });
/// </code>
/// </example>
public sealed class DictionaryEncryptionKeyProvider : IEncryptionKeyProvider
{
    private readonly string activeKeyId;
    private readonly IReadOnlyDictionary<string, byte[]> keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryEncryptionKeyProvider" /> class.
    /// </summary>
    /// <param name="activeKeyId">The key identifier used for writes.</param>
    /// <param name="keys">The available keys.</param>
    /// <example>
    /// <code>
    /// var provider = new DictionaryEncryptionKeyProvider("primary", keys);
    /// </code>
    /// </example>
    public DictionaryEncryptionKeyProvider(string activeKeyId, IReadOnlyDictionary<string, byte[]> keys)
    {
        if (string.IsNullOrWhiteSpace(activeKeyId))
        {
            throw new ArgumentException("Active key identifier must not be null or whitespace.", nameof(activeKeyId));
        }

        ArgumentNullException.ThrowIfNull(keys);
        this.keys = keys.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.ToArray() ?? throw new ArgumentException($"Encryption key '{pair.Key}' cannot be null.", nameof(keys)),
            StringComparer.Ordinal);
        if (!this.keys.ContainsKey(activeKeyId))
        {
            throw new ArgumentException($"Active encryption key '{activeKeyId}' is not present.", nameof(activeKeyId));
        }

        this.activeKeyId = activeKeyId;
    }

    /// <inheritdoc />
    public ValueTask<EncryptionKeyMaterial> GetActiveKeyAsync(CancellationToken cancellationToken = default) =>
        this.GetKeyAsync(this.activeKeyId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<EncryptionKeyMaterial> GetKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            !string.IsNullOrWhiteSpace(keyId) && this.keys.TryGetValue(keyId, out var key)
                ? new EncryptionKeyMaterial(keyId, key)
                : null);
    }
}
