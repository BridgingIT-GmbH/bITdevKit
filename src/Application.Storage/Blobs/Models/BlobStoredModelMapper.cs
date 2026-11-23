// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

internal static class BlobStoredModelMapper
{
    internal static BlobUpload Snapshot(BlobUpload value) => value is null ? null : new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        Content = value.Content,
        ContentType = value.ContentType,
        ExpectedContentHash = value.ExpectedContentHash,
        ExpiresAt = value.ExpiresAt,
        OverwriteMode = value.OverwriteMode,
        IfMatchETag = value.IfMatchETag,
        Properties = BlobPropertyValues.Clone(value.Properties),
        TransformMetadata = BlobPropertyValues.Clone(value.TransformMetadata)
    };

    internal static BlobPropertiesUpdate Snapshot(BlobPropertiesUpdate value) => value is null ? null : new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        ContentType = value.ContentType,
        ExpiresAt = value.ExpiresAt,
        IfMatchETag = value.IfMatchETag,
        Properties = BlobPropertyValues.Clone(value.Properties),
        TransformMetadata = BlobPropertyValues.Clone(value.TransformMetadata)
    };

    internal static StoredBlobUpload ToStored(BlobUpload value) => new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        Content = value.Content,
        ContentType = value.ContentType,
        ExpectedContentHash = value.ExpectedContentHash,
        ExpiresAt = value.ExpiresAt,
        OverwriteMode = value.OverwriteMode,
        IfMatchETag = value.IfMatchETag,
        Properties = BlobPropertyValues.Clone(value.Properties),
        TransformMetadata = BlobPropertyValues.Clone(value.TransformMetadata) ?? new()
    };

    internal static StoredBlobPropertiesUpdate ToStored(BlobPropertiesUpdate value) => new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        ContentType = value.ContentType,
        ExpiresAt = value.ExpiresAt,
        IfMatchETag = value.IfMatchETag,
        Properties = BlobPropertyValues.Clone(value.Properties),
        TransformMetadata = BlobPropertyValues.Clone(value.TransformMetadata)
    };

    internal static BlobInfo ToClient(StoredBlobInfo value) => value is null ? null : new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        Length = value.Length,
        ContentType = value.ContentType,
        ContentHash = value.ContentHash,
        ETag = value.ETag,
        CreatedAt = value.CreatedAt,
        LastModifiedAt = value.LastModifiedAt,
        ExpiresAt = value.ExpiresAt,
        Properties = BlobPropertyValues.Clone(value.Properties) ?? new(),
        TransformMetadata = BlobPropertyValues.Clone(value.TransformMetadata) ?? new()
    };

    internal static BlobInfo CloneInfo(BlobInfo value, PropertyBag properties, PropertyBag transforms) => value is null ? null : new()
    {
        Key = value.Key,
        Dimensions = value.Dimensions,
        Length = value.Length,
        ContentType = value.ContentType,
        ContentHash = value.ContentHash,
        ETag = value.ETag,
        CreatedAt = value.CreatedAt,
        LastModifiedAt = value.LastModifiedAt,
        ExpiresAt = value.ExpiresAt,
        Properties = BlobPropertyValues.Clone(properties) ?? new(),
        TransformMetadata = BlobPropertyValues.Clone(transforms) ?? new()
    };

    internal static BlobDownload ToClient(StoredBlobDownload value) => new()
    {
        Content = value.Content,
        Info = ToClient(value.Info)
    };

    internal static BlobPage ToClient(StoredBlobPage value) => new()
    {
        Items = value.Items.Select(ToClient).ToArray(),
        ContinuationToken = value.ContinuationToken
    };

    internal static Result<TTarget> Map<TSource, TTarget>(Result<TSource> result, Func<TSource, TTarget> map) =>
        result.IsSuccess ? Result<TTarget>.Success(map(result.Value)).WithMessages(result.Messages) : Result<TTarget>.Failure((IResult)result);
}
