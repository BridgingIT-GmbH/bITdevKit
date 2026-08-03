// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides provider-neutral optimistic property patch helpers for blobs.
/// </summary>
/// <example>
/// <code>
/// await blobs.SetPropertyAsync(new BlobKey("reports", "a.pdf"), "reviewed", true);
/// </code>
/// </example>
public static class BlobPropertyStorageExtensions
{
    /// <summary>
    /// Adds or replaces one custom blob property.
    /// </summary>
    /// <param name="blobClient">The blob client to update.</param>
    /// <param name="blobKey">The blob key to update.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="options">Optional property patch options.</param>
    /// <param name="cancellationToken">A token to cancel the update.</param>
    /// <returns>A result containing updated blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.SetPropertyAsync(key, "status", "approved");
    /// </code>
    /// </example>
    public static Task<Result<BlobInfo>> SetPropertyAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        string propertyName,
        object value,
        BlobPropertyPatchOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreValidationError("Property name cannot be null or whitespace.")));
        }

        return PatchAsync(blobClient, blobKey, properties => properties[propertyName] = value, options, cancellationToken);
    }

    /// <summary>
    /// Removes one custom blob property.
    /// </summary>
    /// <param name="blobClient">The blob client to update.</param>
    /// <param name="blobKey">The blob key to update.</param>
    /// <param name="propertyName">The property name to remove.</param>
    /// <param name="options">Optional property patch options.</param>
    /// <param name="cancellationToken">A token to cancel the update.</param>
    /// <returns>A result containing updated blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.RemovePropertyAsync(key, "temporary");
    /// </code>
    /// </example>
    public static Task<Result<BlobInfo>> RemovePropertyAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        string propertyName,
        BlobPropertyPatchOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreValidationError("Property name cannot be null or whitespace.")));
        }

        return PatchAsync(blobClient, blobKey, properties => properties.Remove(propertyName), options, cancellationToken);
    }

    /// <summary>
    /// Merges custom blob properties into the existing property bag.
    /// </summary>
    /// <param name="blobClient">The blob client to update.</param>
    /// <param name="blobKey">The blob key to update.</param>
    /// <param name="properties">The properties to merge.</param>
    /// <param name="options">Optional property patch options.</param>
    /// <param name="cancellationToken">A token to cancel the update.</param>
    /// <returns>A result containing updated blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.MergePropertiesAsync(key, new PropertyBag { ["reviewed"] = true });
    /// </code>
    /// </example>
    public static Task<Result<BlobInfo>> MergePropertiesAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        PropertyBag properties,
        BlobPropertyPatchOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (properties is null)
        {
            return Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreValidationError("Properties cannot be null.")));
        }

        return PatchAsync(blobClient, blobKey, target => target.Merge(properties), options, cancellationToken);
    }

    private static async Task<Result<BlobInfo>> PatchAsync(
        IBlobStoreClient blobClient,
        BlobKey blobKey,
        Action<PropertyBag> patch,
        BlobPropertyPatchOptions options,
        CancellationToken cancellationToken)
    {
        if (blobClient is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(keyValidation);
        }

        var propertiesResult = await blobClient.GetPropertiesAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (propertiesResult.IsFailure)
        {
            return Result<BlobInfo>.Failure(propertiesResult);
        }

        options ??= new BlobPropertyPatchOptions();
        var current = propertiesResult.Value;
        var properties = current.Properties?.Clone() ?? new PropertyBag();
        patch(properties);

        return await blobClient.UpdatePropertiesAsync(
            new BlobPropertiesUpdate
            {
                Key = blobKey,
                ContentType = options.ContentType ?? current.ContentType,
                ExpiresAt = current.ExpiresAt,
                IfMatchETag = options.IfMatchETag ?? current.ETag,
                Properties = properties
            },
            cancellationToken).ConfigureAwait(false);
    }
}
