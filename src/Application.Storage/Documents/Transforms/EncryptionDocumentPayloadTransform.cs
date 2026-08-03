// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Applies reversible AES encryption with active-write and historical-read key resolution.</summary>
/// <param name="keyProvider">
/// The key provider used to resolve the active key for writes and the persisted key identifier for reads.
/// </param>
/// <remarks>
/// Encryption delegates cryptographic operations to <see cref="EncryptionHelper" />. Key material is never persisted in
/// transform metadata; only the non-secret key identifier is stored so rotated historical keys remain readable.
/// </remarks>
/// <example><code>var transform = new EncryptionDocumentPayloadTransform(keyProvider);</code></example>
public sealed class EncryptionDocumentPayloadTransform(IEncryptionKeyProvider keyProvider) : IDocumentPayloadTransform
{
    /// <inheritdoc />
    public string Identifier => "aes-cbc-pkcs7";
    /// <inheritdoc />
    public async ValueTask<byte[]> WriteAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        var material = await keyProvider.GetActiveKeyAsync(cancellationToken) ?? throw new InvalidOperationException("No active document encryption key is available.");
        metadata.Set("bdk_encryption_key_id", material.KeyId);
        return await EncryptionHelper.EncryptAsync(content, material.Key.ToArray(), cancellationToken);
    }
    /// <inheritdoc />
    public async ValueTask<byte[]> ReadAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
    {
        var keyId = metadata.Get<string>("bdk_encryption_key_id");
        var material = await keyProvider.GetKeyAsync(keyId, cancellationToken) ?? throw new InvalidOperationException($"Document encryption key '{keyId}' is unavailable.");
        return await EncryptionHelper.DecryptAsync(content, material.Key.ToArray(), cancellationToken);
    }
}
